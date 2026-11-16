"""Fine-tune the video-label classifier (16 official labels) on frozen CLIP frame embeddings.

Initialised from the zero-shot text prompts and regularised towards them (so it can only improve on CLIP prompts where
data supports it). Evaluated with video-level k-fold: held-out videos only, compared with zero-shot on the SAME videos.
   python training/train_label.py --fpv E:/samsung_temp/TRAIN/feats_fpvsum --epic E:/samsung_temp/TRAIN/feats_epic --out weights/label_head.pt
"""
import argparse
import sys
from pathlib import Path

import numpy as np
import torch
import torch.nn.functional as F

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "code"))
from vistara import vocab  # noqa: E402
from vistara.config import Config  # noqa: E402
from vistara.model import ClipEncoder  # noqa: E402

TEMP = 100.0


def video_acc(W, b, vids, labels):
    ok = 0
    for emb, y in vids:
        p = torch.softmax(torch.from_numpy(emb).float() @ W.T * 1.0 + b, -1).mean(0)
        ok += int(p.argmax()) == y
    return ok / max(1, len(vids))


def fit(vids, W0, epochs=60, lam=5.0, seed=0):
    torch.manual_seed(seed)
    W = W0.clone().requires_grad_(True)
    b = torch.zeros(W0.shape[0], requires_grad=True)
    X = torch.cat([torch.from_numpy(e).float() for e, _ in vids])
    Y = torch.cat([torch.full((len(e),), y) for e, y in vids])
    wts = torch.cat([torch.full((len(e),), 1.0 / len(e)) for e, _ in vids])      # every video counts equally
    wts = wts / wts.sum() * len(vids)
    opt = torch.optim.Adam([W, b], 2e-3)
    for _ in range(epochs):
        perm = torch.randperm(len(X))
        for i in range(0, len(X), 512):
            j = perm[i:i + 512]
            xb = X[j] + 0.01 * torch.randn_like(X[j])
            loss = (F.cross_entropy(xb @ W.T + b, Y[j], reduction="none") * wts[j]).mean() \
                + lam * ((W - W0) ** 2).sum() / W0.shape[0] * 1e-2
            opt.zero_grad()
            loss.backward()
            opt.step()
    return W.detach(), b.detach()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--fpv", required=True)
    ap.add_argument("--epic", default=None, help="EPIC window features dir (used as 'cooking' examples)")
    ap.add_argument("--out", required=True)
    ap.add_argument("--folds", type=int, default=5)
    a = ap.parse_args()
    cfg = Config()
    labels = vocab.labels()
    enc = ClipEncoder(cfg)
    T = torch.from_numpy(enc.encode_text_groups([vocab.LABEL_PROMPTS[l] for l in labels])).float()
    W0 = T * TEMP                                              # zero-shot logits = 100 * sim
    vids = []
    for f in sorted(Path(a.fpv).glob("*.npz")):
        d = np.load(f)
        c = str(d["category"])
        if c in labels:
            e = d["emb"].astype(np.float32)
            vids.append((e[:: max(1, len(e) // 300)], labels.index(c), f.stem))
    if a.epic:
        for f in sorted(Path(a.epic).glob("*.npz"))[:60]:
            X = np.load(f)["X"][:, :512]
            vids.append((X[:: max(1, len(X) // 150)], labels.index("cooking"), f.stem))
    print(f"{len(vids)} videos; classes present: {sorted({labels[y] for _, y, _ in vids})}")
    rng = np.random.RandomState(0)
    order = rng.permutation(len(vids))
    folds = np.array_split(order, a.folds)
    zs_ok = tr_ok = n = 0
    zs0 = (W0, torch.zeros(len(labels)))
    for k, te in enumerate(folds):
        tei = set(te.tolist())
        tr = [(e, y) for i, (e, y, _) in enumerate(vids) if i not in tei]
        te_v = [(vids[i][0], vids[i][1]) for i in te]
        W, b = fit(tr, W0)
        z = video_acc(*zs0, te_v, labels) * len(te_v)
        t = video_acc(W, b, te_v, labels) * len(te_v)
        zs_ok += z; tr_ok += t; n += len(te_v)
        print(f"fold {k}: zero-shot {z / len(te_v):.2f}  trained {t / len(te_v):.2f}  (n={len(te_v)})")
    print(f"HELD-OUT video accuracy: zero-shot {zs_ok / n:.3f} -> trained {tr_ok / n:.3f}")
    W, b = fit([(e, y) for e, y, _ in vids], W0)
    Path(a.out).parent.mkdir(parents=True, exist_ok=True)
    torch.save({"W": W, "b": b, "labels": labels,
                "meta": {"heldout_zero_shot": zs_ok / n, "heldout_trained": tr_ok / n, "n_videos": len(vids),
                         "train_videos": [v for _, _, v in vids]}}, a.out)
    print("saved", a.out)


if __name__ == "__main__":
    main()
