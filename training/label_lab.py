"""Video-category classifier lab (FPVSum): compares classifiers on cached CLIP frame embeddings.

TRAIN = FPVSum videos that are NOT in the manifest (category from the public file name), TEST = the manifest FPVSum videos present on disk
(never used for fitting or model selection; model selection uses cross-validation on TRAIN only).

   python training/label_lab.py --cache E:/samsung_temp/TRAIN/cache_b32 --train-dir E:/.../TRAIN/data/fpvsum --test-dir E:/.../EVAL/fpvsum [--save weights/label_head.pt]
"""
import argparse
import csv
import re
import sys
from pathlib import Path

import numpy as np
import torch
import torch.nn.functional as F

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "code"))
from vistara import vocab  # noqa: E402
from vistara.config import Config  # noqa: E402
from vistara.model import ClipEncoder  # noqa: E402

LAB = [l for l in vocab.labels()]
SPORT = [l for l in LAB if l not in ("cooking", "other")]


def load(cache: Path, vid_dir: Path, man: set, want_manifest: bool):
    out = []
    for f in sorted(vid_dir.glob("*.mp4")):
        vid = f.stem
        if (vid in man) != want_manifest:
            continue
        hits = sorted(cache.glob(f"{vid}_*.npz"))
        if not hits:
            continue
        z = np.load(hits[0])
        out.append((vid, vid.split("_")[0].lower(), z["emb"].astype(np.float32), z["hand"], z["local"], z["glob"]))
    return out


def video_feats(emb, glob):
    m = emb.mean(0); m /= np.linalg.norm(m) + 1e-9
    return m


def softmax(x, t=1.0):
    z = x * t; z = z - z.max(-1, keepdims=True); e = np.exp(z); return e / e.sum(-1, keepdims=True)


def fit_lr(X, y, n_cls, l2=1e-2, epochs=400, W0=None, lam0=0.0, seed=0):
    torch.manual_seed(seed)
    X = torch.from_numpy(X).float(); y = torch.from_numpy(y).long()
    W = (torch.zeros(n_cls, X.shape[1]) if W0 is None else torch.from_numpy(W0).float().clone()).requires_grad_(True)
    b = torch.zeros(n_cls, requires_grad=True)
    ref = None if W0 is None else torch.from_numpy(W0).float()
    opt = torch.optim.LBFGS([W, b], lr=1.0, max_iter=epochs, line_search_fn="strong_wolfe")

    def closure():
        opt.zero_grad()
        loss = F.cross_entropy(X @ W.T + b, y) + l2 * (W ** 2).sum()
        if ref is not None:
            loss = loss + lam0 * ((W - ref) ** 2).sum()
        loss.backward(); return loss
    opt.step(closure)
    return W.detach().numpy(), b.detach().numpy()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", required=True); ap.add_argument("--train-dir", required=True); ap.add_argument("--test-dir", required=True)
    ap.add_argument("--save", default=None)
    a = ap.parse_args()
    man = {r["video_id"] for r in csv.DictReader(open(ROOT / "data" / "manifest.csv", encoding="utf-8"))}
    cache = Path(a.cache)
    tr = [v for v in load(cache, Path(a.train_dir), man, False) if v[1] in SPORT]
    te = [v for v in load(cache, Path(a.test_dir), man, True) if v[1] in SPORT]
    print(f"train videos {len(tr)}  test videos {len(te)}  classes {sorted({v[1] for v in tr})}")
    enc = ClipEncoder(Config())
    T = enc.encode_text_groups([vocab.LABEL_PROMPTS[l] for l in SPORT])           # zero-shot class embeddings (sport classes only)
    idx = {l: i for i, l in enumerate(SPORT)}
    ytr = np.array([idx[v[1]] for v in tr]); yte = np.array([idx[v[1]] for v in te])
    Xtr = np.stack([video_feats(v[2], v[5]) for v in tr]); Xte = np.stack([video_feats(v[2], v[5]) for v in te])

    def acc(P, y): return float((P.argmax(1) == y).mean())
    # 1) zero-shot: per-frame posterior averaged (what the engine did originally)
    def zs(vids): return np.stack([softmax(v[2] @ T.T, 100.0).mean(0) for v in vids])
    print(f"zero-shot prompts          test {acc(zs(te), yte):.3f}   (train {acc(zs(tr), ytr):.3f})")
    # 2) logistic regression on the video-level mean embedding (L2 chosen by leave-one-out on TRAIN only)
    best = None
    for l2 in (1e-4, 1e-3, 1e-2, 1e-1):
        hits = 0
        for i in range(len(tr)):
            m = np.ones(len(tr), bool); m[i] = False
            W, b = fit_lr(Xtr[m], ytr[m], len(SPORT), l2=l2, epochs=60)
            hits += int((Xtr[i] @ W.T + b).argmax() == ytr[i])
        print(f"  LR l2={l2:g}: leave-one-out train acc {hits / len(tr):.3f}")
        if best is None or hits > best[0]: best = (hits, l2)
    W, b = fit_lr(Xtr, ytr, len(SPORT), l2=best[1])
    Plr = softmax(Xte @ W.T + b)
    print(f"logistic regression (video mean) l2={best[1]:g}  test {acc(Plr, yte):.3f}")
    # 3) text-prior regularised: start from the zero-shot weights and stay close to them
    W0 = T * 100.0
    for lam in (1e-4, 1e-3, 1e-2):
        W2, b2 = fit_lr(Xtr, ytr, len(SPORT), l2=0.0, W0=W0, lam0=lam)
        print(f"  prompt-anchored LR lam={lam:g}: test {acc(softmax(Xte @ W2.T + b2), yte):.3f}")
    # 4) ensemble zero-shot + LR
    Pz = zs(te)
    for w in (0.3, 0.5, 0.7):
        print(f"ensemble zero-shot*{1 - w:.1f} + LR*{w:.1f}      test {acc((1 - w) * Pz + w * Plr, yte):.3f}")
    # 5) kNN on video means
    S = Xte @ Xtr.T
    for k in (1, 3):
        pred = []
        for i in range(len(te)):
            nn = np.argsort(-S[i])[:k]; votes = np.zeros(len(SPORT))
            for j in nn: votes[ytr[j]] += S[i, j]
            pred.append(votes.argmax())
        print(f"kNN k={k}                    test {float((np.array(pred) == yte).mean()):.3f}")
    wrong = [(te[i][0], SPORT[int((0.5 * Pz + 0.5 * Plr)[i].argmax())]) for i in range(len(te)) if (0.5 * Pz + 0.5 * Plr)[i].argmax() != yte[i]]
    print("errors of the 50/50 ensemble:", wrong)


if __name__ == "__main__":
    main()
