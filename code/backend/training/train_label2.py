"""Video category head (16 official labels): zero-shot CLIP prompts + a small prompt-anchored linear correction, ensembled.

   p(video) = (1 - alpha) * mean_frames softmax(100 * frame . text)  +  alpha * softmax(W . video_mean_embedding + b)

W starts from the text embeddings (zero-shot) and is fitted on video-mean embeddings of FPVSum videos that are NOT in the manifest (+ EPIC training
videos as 'cooking'), anchored to the prompts with strength `lam`. lam and alpha are chosen by LEAVE-ONE-OUT on the training videos only; the
manifest FPVSum videos are only used for the final held-out report.

   python training/train_label2.py --cache <cache dir> --model ViT-L-14-quickgelu --clip-dir <dir> --fpv-train <dir> --epic-train <dir> --fpv-test <dir> --out weights/label_head.pt
"""
import argparse, csv, os, sys
from pathlib import Path
import numpy as np
import torch
import torch.nn.functional as F

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from vistara import vocab  # noqa: E402
from vistara.config import Config  # noqa: E402
from vistara.model import ClipEncoder  # noqa: E402

LAB = vocab.labels()


def softmax(x, t=1.0):
    z = x * t; z = z - z.max(-1, keepdims=True); e = np.exp(z); return e / e.sum(-1, keepdims=True)


def load(cache, vdir, man, want_manifest, label_fn):
    out = []
    for f in sorted(Path(vdir).rglob("*")):
        if f.suffix.lower() != ".mp4":
            continue
        vid = f.stem
        if (vid in man) != want_manifest:
            continue
        hits = sorted(Path(cache).glob(f"{vid}_*.npz"))
        lab = label_fn(vid)
        if hits and lab in LAB:
            z = np.load(hits[0]); out.append((vid, lab, z["emb"].astype(np.float32)))
    return out


def fit(X, y, W0, lam, l2=1e-4):
    Xt = torch.from_numpy(X).float(); yt = torch.from_numpy(y).long(); ref = torch.from_numpy(W0).float()
    W = ref.clone().requires_grad_(True); b = torch.zeros(len(W0), requires_grad=True)
    opt = torch.optim.LBFGS([W, b], lr=1.0, max_iter=80, line_search_fn="strong_wolfe")

    def closure():
        opt.zero_grad()
        loss = F.cross_entropy(Xt @ W.T + b, yt) + lam * ((W - ref) ** 2).sum() + l2 * (b ** 2).sum()
        loss.backward(); return loss
    opt.step(closure)
    return W.detach().numpy(), b.detach().numpy()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", required=True); ap.add_argument("--model", required=True); ap.add_argument("--clip-dir", default=None)
    ap.add_argument("--fpv-train", required=True); ap.add_argument("--epic-train", required=True); ap.add_argument("--fpv-test", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    man = {r["video_id"] for r in csv.DictReader(open(ROOT / "data" / "manifest.csv", encoding="utf-8"))}
    cfg = Config(); cfg.clip_model = a.model
    enc = ClipEncoder(cfg, cache_dir=a.clip_dir)
    T = enc.encode_text_groups([vocab.LABEL_PROMPTS[l] for l in LAB])
    idx = {l: i for i, l in enumerate(LAB)}
    tr = load(a.cache, a.fpv_train, man, False, lambda v: v.split("_")[0].lower())
    tr += load(a.cache, a.epic_train, man, False, lambda v: "cooking")[:40]
    te = load(a.cache, a.fpv_test, man, True, lambda v: v.split("_")[0].lower())
    print(f"training videos {len(tr)} (FPVSum + EPIC as cooking) | held-out manifest FPVSum videos {len(te)}")
    ytr = np.array([idx[v[1]] for v in tr]); yte = np.array([idx[v[1]] for v in te])

    def mean_emb(vids): m = np.stack([v[2].mean(0) for v in vids]); return m / np.linalg.norm(m, axis=1, keepdims=True)
    def zs(vids): return np.stack([softmax(v[2] @ T.T, 100.0).mean(0) for v in vids])
    Xtr, Xte = mean_emb(tr), mean_emb(te); Ztr, Zte = zs(tr), zs(te); W0 = T * 100.0
    acc = lambda P, y: float((P.argmax(1) == y).mean())
    print(f"zero-shot prompts: train {acc(Ztr, ytr):.3f}  held-out {acc(Zte, yte):.3f}")
    best = None
    for lam in (1e-4, 3e-4, 1e-3, 1e-2):
        P_lr = np.zeros((len(tr), len(LAB)))
        for i in range(len(tr)):                                               # leave-one-out on the TRAINING videos
            m = np.ones(len(tr), bool); m[i] = False
            W, b = fit(Xtr[m], ytr[m], W0, lam)
            P_lr[i] = softmax(Xtr[i] @ W.T + b)
        for alpha in (0.0, 0.3, 0.5, 0.7, 1.0):
            s = acc((1 - alpha) * Ztr + alpha * P_lr, ytr)
            print(f"  LOO train: lam={lam:g} alpha={alpha:.1f} -> {s:.3f}")
            if best is None or s > best[0] + 1e-9 or (abs(s - best[0]) < 1e-9 and alpha > best[2]):
                best = (s, lam, alpha)
    print(f"chosen on the training videos only: lam={best[1]:g} alpha={best[2]:.1f} (LOO {best[0]:.3f})")
    W, b = fit(Xtr, ytr, W0, best[1])
    P = (1 - best[2]) * Zte + best[2] * softmax(Xte @ W.T + b)
    print(f"HELD-OUT manifest FPVSum videos: zero-shot {acc(Zte, yte):.3f} -> final {acc(P, yte):.3f}")
    wrong = [(te[i][0], LAB[int(P[i].argmax())]) for i in range(len(te)) if P[i].argmax() != yte[i]]
    print("errors:", wrong)
    Path(a.out).parent.mkdir(parents=True, exist_ok=True)
    torch.save({"W": torch.from_numpy(W), "b": torch.from_numpy(b), "labels": LAB,
                "meta": {"mode": "video_mean_ensemble", "alpha": best[2], "lam": best[1], "backbone": a.model, "loo_train_acc": best[0],
                         "heldout_zero_shot": acc(Zte, yte), "heldout_final": acc(P, yte), "n_train_videos": len(tr), "train_videos": [v[0] for v in tr]}}, a.out)
    print("saved", a.out)


if __name__ == "__main__":
    main()
