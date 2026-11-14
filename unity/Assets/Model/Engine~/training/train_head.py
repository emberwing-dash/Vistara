"""Train the action head on extracted features. Whole VIDEOS are held out for validation.

   python training/train_head.py --feats data/epic/feats --out weights/head.pt
"""
import argparse
import sys
from pathlib import Path

import numpy as np
import torch
import torch.nn.functional as F

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "code"))
from vistara.head import ActionHead  # noqa: E402


def load(files):
    d = [np.load(f) for f in files]
    return (np.concatenate([x["X"] for x in d]), np.concatenate([x["verb"] for x in d]),
            np.concatenate([x["noun"] for x in d]))


def zero_shot_baseline(Xv, Vv, Nv, dev):
    """Zero-shot CLIP prompts with the engine's own priors, on the same held-out windows."""
    from vistara.config import Config
    from vistara.model import ClipEncoder
    from vistara.recognizer import Recognizer
    r = Recognizer(ClipEncoder(Config()), Config())
    r.head = None
    v = Xv[:, :512] / (np.linalg.norm(Xv[:, :512], axis=1, keepdims=True) + 1e-9)
    ln = v @ r.noun_emb.T * 100.0 + r.noun_logprior
    lv = v @ r.verb_emb.T * 100.0 + r.verb_logprior
    pn = np.exp(ln - ln.max(1, keepdims=True)); pn /= pn.sum(1, keepdims=True)
    pv = np.exp(lv - lv.max(1, keepdims=True)); pv /= pv.sum(1, keepdims=True)
    pn, pv = pn[:, :N_NOUN_], pv[:, :N_VERB_]
    k = Vv >= 0
    return {"pv": pv, "pn": pn, "v": float((pv[k].argmax(1) == Vv[k]).mean()), "n": float((pn[k].argmax(1) == Nv[k]).mean())}


N_VERB_, N_NOUN_ = 97, 300


@torch.no_grad()
def blend_acc(m, zs, Xv, Vv, Nv, dev, a):
    m.eval()
    _, hv, hn = m(torch.from_numpy(Xv).to(dev))
    hv, hn = torch.softmax(hv, -1).cpu().numpy(), torch.softmax(hn, -1).cpu().numpy()
    pv = (1 - a) * zs["pv"] + a * hv
    pn = (1 - a) * zs["pn"] + a * hn
    k = Vv >= 0
    return float((pv[k].argmax(1) == Vv[k]).mean()), float((pn[k].argmax(1) == Nv[k]).mean())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--feats", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--epochs", type=int, default=40)
    ap.add_argument("--val-frac", type=float, default=0.2)
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--blend", type=float, default=0.5, help="head weight when blended with zero-shot (inference uses meta.blend)")
    a = ap.parse_args()
    files = sorted(Path(a.feats).glob("*.npz"))
    rng = np.random.RandomState(a.seed)
    parts = sorted({f.stem.split("_")[0] for f in files})          # hold out whole PARTICIPANTS (unseen kitchens)
    rng.shuffle(parts)
    held = set(parts[:max(1, int(len(parts) * a.val_frac))])
    val = [f for f in files if f.stem.split("_")[0] in held]
    tr = [f for f in files if f.stem.split("_")[0] not in held]
    print(f"{len(tr)} train videos, {len(val)} held-out videos from unseen participants {sorted(held)}")
    Xt, Vt, Nt = load(tr)
    Xv, Vv, Nv = load(val)
    dev = "cuda" if torch.cuda.is_available() else "cpu"
    m = ActionHead(dim=Xt.shape[1], p=0.5).to(dev)
    opt = torch.optim.AdamW(m.parameters(), 5e-4, weight_decay=0.1)
    zs = zero_shot_baseline(Xv, Vv, Nv, dev)
    best = (-1.0, None, 0)

    def T(x, dt=torch.float32):
        return torch.from_numpy(x).to(dev, dt)

    xt, vt, nt = T(Xt), T(Vt, torch.long), T(Nt, torch.long)
    fgt = (vt >= 0).float()
    for ep in range(a.epochs):
        m.train()
        perm = torch.randperm(len(xt), device=dev)
        tot = 0.0
        for i in range(0, len(xt), 256):
            b = perm[i:i + 256]
            fg, v, n = m(xt[b] + 0.02 * torch.randn_like(xt[b]))
            loss = F.binary_cross_entropy_with_logits(fg, fgt[b])
            k = fgt[b] > 0
            if k.any():
                loss = loss + F.cross_entropy(v[k], vt[b][k], label_smoothing=0.1) \
                    + F.cross_entropy(n[k], nt[b][k], label_smoothing=0.1)
            opt.zero_grad()
            loss.backward()
            opt.step()
            tot += float(loss)
        if ep % 5 == 4 or ep == a.epochs - 1:
            m.eval()
            with torch.no_grad():
                fg, v, n = m(T(Xv))
                k = T(Vv, torch.long) >= 0
                va = (v[k].argmax(-1) == T(Vv, torch.long)[k]).float().mean().item() if k.any() else 0.0
                na = (n[k].argmax(-1) == T(Nv, torch.long)[k]).float().mean().item() if k.any() else 0.0
                fa = ((torch.sigmoid(fg) > 0.5) == k).float().mean().item()
                bl = blend_acc(m, zs, Xv, Vv, Nv, dev, a.blend)
            print(f"ep {ep + 1:3d} loss {tot:.2f} | held-out head verb@1 {va:.3f} noun@1 {na:.3f} fg-acc {fa:.3f} | "
                  f"blend verb@1 {bl[0]:.3f} noun@1 {bl[1]:.3f} (zero-shot {zs['v']:.3f}/{zs['n']:.3f})")
            if bl[0] + bl[1] > best[0]:
                best = (bl[0] + bl[1], {k: v.detach().clone() for k, v in m.state_dict().items()}, ep + 1)
    m.load_state_dict(best[1])
    print(f"best epoch {best[2]}: blend verb+noun {best[0]:.3f} vs zero-shot {zs['v'] + zs['n']:.3f}")
    Path(a.out).parent.mkdir(parents=True, exist_ok=True)
    torch.save({"state_dict": m.state_dict(), "dim": int(Xt.shape[1]), "hidden": 1024,
                "meta": {"blend": a.blend, "heldout_blend_verb_noun": best[0], "heldout_zero_shot_verb_noun": zs["v"] + zs["n"],
                         "best_epoch": best[2], "train_videos": [f.stem for f in tr], "val_videos": [f.stem for f in val]}}, a.out)
    print("saved", a.out)


if __name__ == "__main__":
    main()
