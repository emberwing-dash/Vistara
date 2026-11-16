"""Residual action head: learns a correction ON TOP of the zero-shot (CLIP text similarity + data prior) logits.

   logits_verb = 100 * sim(window, verb text) + log-prior + head_verb(window feature)       (same for nouns)

The head starts at zero (= the zero-shot recogniser) and is trained with cross-entropy on EPIC TRAIN windows. Validation uses whole held-out
PARTICIPANTS (unseen kitchens) and the priors used for validation are built WITHOUT those participants' videos, so nothing leaks.
Manifest (scored) videos are never used. Shipping rule: the checkpoint is only useful if it beats the zero-shot baseline on the held-out kitchens.

   python training/train_head2.py --feats <windows dir> --model ViT-L-14-quickgelu --clip-dir <dir> --ann EPIC_100_train.csv \
       --holdout P02,P09,P12 --out weights/head.pt [--final]      (--final: train on ALL videos with all-video priors)
"""
import argparse, os, subprocess, sys
from pathlib import Path
import numpy as np
import torch
import torch.nn.functional as F

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "code"))
os.environ.setdefault("VISTARA_WEIGHTS", "E:/samsung_temp/EXP/nohead")
from vistara.config import Config  # noqa: E402
from vistara.head import ActionHead  # noqa: E402
from vistara.model import ClipEncoder  # noqa: E402


def zero_shot_assets(model, clip_dir, prior_path, w_v, w_n):
    """Text embeddings + log-priors exactly as the engine's Recognizer builds them (with the data prior from prior_path)."""
    import shutil, tempfile
    wd = Path(tempfile.mkdtemp()); shutil.copy(prior_path, wd / "pair_prior.npz")
    os.environ["VISTARA_WEIGHTS"] = str(wd)
    import importlib, vistara.config as C
    importlib.reload(C)
    import vistara.recognizer as R
    importlib.reload(R)
    cfg = C.Config(); cfg.clip_model = model; cfg.use_data_prior = True; cfg.prior_verb_w = w_v; cfg.prior_noun_w = w_n
    enc = ClipEncoder(cfg, cache_dir=clip_dir)
    rec = R.Recognizer(enc, cfg)
    return rec


def load(feats, files):
    d = [np.load(f) for f in files]
    return (np.concatenate([x["X"] for x in d]), np.concatenate([x["verb"] for x in d]), np.concatenate([x["noun"] for x in d]))


def topk(lg, y, k):
    top = np.argsort(-lg, 1)[:, :k]
    return float((top == y[:, None]).any(1).mean())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--feats", required=True); ap.add_argument("--model", required=True); ap.add_argument("--clip-dir", default=None)
    ap.add_argument("--ann", required=True); ap.add_argument("--out", required=True)
    ap.add_argument("--holdout", default="P02,P09,P12"); ap.add_argument("--final", action="store_true")
    ap.add_argument("--epochs", type=int, default=40); ap.add_argument("--wv", type=float, default=1.2); ap.add_argument("--wn", type=float, default=1.2)
    ap.add_argument("--hidden", type=int, default=512); ap.add_argument("--drop", type=float, default=0.5); ap.add_argument("--wd", type=float, default=0.1)
    a = ap.parse_args()
    files = sorted(Path(a.feats).glob("*.npz"))
    held = set(a.holdout.split(","))
    val = [f for f in files if f.stem.split("_")[0] in held]
    trn = files if a.final else [f for f in files if f.stem.split("_")[0] not in held]
    prior = Path(a.out).with_suffix(".prior.npz")
    ex = ",".join(f.stem for f in (val if not a.final else []))
    subprocess.run([sys.executable, str(ROOT / "training" / "build_priors.py"), "--ann", a.ann, "--out", str(prior), "--exclude", ex], check=True, capture_output=True)
    rec = zero_shot_assets(a.model, a.clip_dir, prior, a.wv, a.wn)
    D = rec.noun_emb.shape[1]
    dev = "cuda" if torch.cuda.is_available() else "cpu"
    NV, NN = 97, 300
    ve = torch.from_numpy(rec.verb_emb[:NV]).float().to(dev); ne = torch.from_numpy(rec.noun_emb[:NN]).float().to(dev)
    vp = torch.from_numpy(rec.verb_logprior[:NV]).float().to(dev); npri = torch.from_numpy(rec.noun_logprior[:NN]).float().to(dev)

    def zs(X):
        m = X[:, :D]
        return m @ ve.T * 100.0 + vp, m @ ne.T * 100.0 + npri

    Xt, Vt, Nt = load(a.feats, trn)
    print(f"train windows {len(Xt)} from {len(trn)} videos | validation: {len(val)} videos of {sorted(held)} | backbone {a.model} (D={D})")
    Xt_t = torch.from_numpy(Xt).float().to(dev); Vt_t = torch.from_numpy(Vt).long().to(dev); Nt_t = torch.from_numpy(Nt).long().to(dev)
    fg = (Vt_t >= 0).float()
    m = ActionHead(dim=Xt.shape[1], hidden=a.hidden, p=a.drop).to(dev)
    for lin in (m.verb, m.noun):                                  # start exactly at the zero-shot recogniser
        torch.nn.init.zeros_(lin.weight); torch.nn.init.zeros_(lin.bias)
    opt = torch.optim.AdamW(m.parameters(), 5e-4, weight_decay=a.wd)
    if val:
        Xv, Vv, Nv = load(a.feats, val)
        Xv_t = torch.from_numpy(Xv).float().to(dev); k = Vv >= 0
    def evaluate():
        m.eval()
        with torch.no_grad():
            zv, zn = zs(Xv_t); _, hv, hn = m(Xv_t)
            lv = (zv + hv).cpu().numpy()[k]; ln = (zn + hn).cpu().numpy()[k]
            z0v, z0n = zv.cpu().numpy()[k], zn.cpu().numpy()[k]
        return dict(v1=topk(lv, Vv[k], 1), v5=topk(lv, Vv[k], 5), n1=topk(ln, Nv[k], 1), n5=topk(ln, Nv[k], 5)), \
            dict(v1=topk(z0v, Vv[k], 1), v5=topk(z0v, Vv[k], 5), n1=topk(z0n, Nv[k], 1), n5=topk(z0n, Nv[k], 5))
    best = (-1.0, None, 0)
    if val:
        base = evaluate()[1]
        print(f"zero-shot + prior (held-out kitchens): verb top1 {base['v1']:.3f} top5 {base['v5']:.3f} | noun top1 {base['n1']:.3f} top5 {base['n5']:.3f}")
    for ep in range(a.epochs):
        m.train(); perm = torch.randperm(len(Xt_t), device=dev)
        for i in range(0, len(perm), 256):
            b = perm[i:i + 256]
            x = Xt_t[b] + 0.01 * torch.randn_like(Xt_t[b])
            zv, zn = zs(Xt_t[b]); fgl, hv, hn = m(x)
            loss = F.binary_cross_entropy_with_logits(fgl, fg[b]); kk = fg[b] > 0
            if kk.any():
                loss = loss + F.cross_entropy((zv + hv)[kk], Vt_t[b][kk], label_smoothing=0.1) + F.cross_entropy((zn + hn)[kk], Nt_t[b][kk], label_smoothing=0.1)
            opt.zero_grad(); loss.backward(); opt.step()
        if val and (ep % 4 == 3 or ep == a.epochs - 1):
            r, _ = evaluate()
            sc = r["v1"] + r["n1"]
            print(f"ep {ep + 1:3d} | held-out verb top1 {r['v1']:.3f} top5 {r['v5']:.3f} | noun top1 {r['n1']:.3f} top5 {r['n5']:.3f}")
            if sc > best[0]: best = (sc, {kx: vx.detach().clone() for kx, vx in m.state_dict().items()}, ep + 1)
    if val and best[1] is not None and not a.final:
        m.load_state_dict(best[1]); print(f"best epoch {best[2]}")
    base = evaluate()[1] if val else {}
    meta = {"mode": "residual", "backbone": a.model, "weights_prior_verb": a.wv, "weights_prior_noun": a.wn, "best_epoch": best[2], "final": bool(a.final),
            "train_videos": [f.stem for f in trn], "heldout": sorted(held) if not a.final else [],
            "heldout_residual": evaluate()[0] if val and not a.final else None, "heldout_zero_shot": base if not a.final else None}
    Path(a.out).parent.mkdir(parents=True, exist_ok=True)
    torch.save({"state_dict": m.state_dict(), "dim": int(Xt.shape[1]), "hidden": a.hidden, "meta": meta}, a.out)
    print("saved", a.out)


if __name__ == "__main__":
    main()
