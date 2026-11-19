"""Adapt the AVION action head (3806 EPIC verb:noun classes) on EPIC videos that are on disk.

Stage 1 (--extract): AVION backbone (frozen, fp16) -> one 1024-d embedding per labelled segment (16 frames), cached per video, resumable.
Stage 2 (--train)  : video-level split (held-out videos are NEVER trained on). Fine-tunes the classification head starting from the public
                     weights with an L2 pull back to them (so it cannot forget), early-stops on a validation part of the TRAIN videos, and reports
                     verb / noun / action top-1 on the held-out videos before and after. The adapted head is written only if the held-out action top-1 improves.

usage: python train_avion_head.py --extract --train --ann <EPIC_100_validation.csv> --data D1 D2 ... --cache K:/samsung_temp/AVIONFEAT --max-seg 40
"""
import argparse, csv, glob, os, random, sys, time
from pathlib import Path
import numpy as np

BACKEND = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(BACKEND))


def ts(t):
    h, m, s = t.split(":"); return int(h) * 3600 + int(m) * 60 + float(s)


def find_videos(dirs):
    files = {}
    for d in dirs:
        for f in glob.glob(os.path.join(d, "**", "*.MP4"), recursive=True) + glob.glob(os.path.join(d, "**", "*.mp4"), recursive=True):
            if ".part" not in f: files[os.path.basename(f).split(".")[0]] = f
    return files


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--extract", action="store_true"); ap.add_argument("--train", action="store_true")
    ap.add_argument("--ann", required=True); ap.add_argument("--data", nargs="+", required=True); ap.add_argument("--cache", required=True)
    ap.add_argument("--max-seg", type=int, default=40); ap.add_argument("--holdout", type=float, default=0.25); ap.add_argument("--seed", type=int, default=0); ap.add_argument("--reverse", action="store_true")
    ap.add_argument("--out", default=str(BACKEND / "weights" / "avion_head_adapted.pt"))
    a = ap.parse_args()
    import torch
    from vistara.avion import AvionRecognizer
    dev = "cuda" if torch.cuda.is_available() else "cpu"
    rec = AvionRecognizer(BACKEND / "weights" / "avion_vitl_ek100_fp16.pt", dev)
    act_of = {(int(v), int(n)): i for i, (v, n) in enumerate(zip(rec.verb_of, rec.noun_of))}
    files = find_videos(a.data); cache = Path(a.cache); cache.mkdir(parents=True, exist_ok=True)
    segs = {}
    r = csv.reader(open(a.ann, encoding="utf-8")); next(r)
    for row in r:
        if row[2] in files and (int(row[10]), int(row[12])) in act_of:
            segs.setdefault(row[2], []).append((ts(row[4]), ts(row[5]), act_of[(int(row[10]), int(row[12]))]))
    print(len(files), "videos on disk;", len(segs), "with labelled segments", flush=True)

    if a.extract:
        import cv2
        t0 = time.time()
        for k, (vid, ss) in enumerate(sorted(segs.items(), reverse=a.reverse)):
            out = cache / (vid + ".npz")
            if out.exists(): continue
            random.Random(vid).shuffle(ss); ss = [s for s in ss if s[1] - s[0] >= 0.5][:a.max_seg]; ss.sort()
            cap = cv2.VideoCapture(files[vid]); fps = cap.get(cv2.CAP_PROP_FPS) or 60.0; E, Y = [], []
            for b0 in range(0, len(ss), 6):
                chunk = ss[b0:b0 + 6]
                x = torch.from_numpy(np.stack([rec.read_clip(cap, fps, s, e) for s, e, _ in chunk])).to(dev).to(rec.dt)
                with torch.no_grad():
                    em = rec.visual(x); em = em[0] if isinstance(em, list) else em
                E.append(em.float().cpu().numpy()); Y += [y for _, _, y in chunk]
            cap.release()
            if E: np.savez(out, emb=np.concatenate(E).astype(np.float16), y=np.array(Y))
            print(f"extract {k + 1}/{len(segs)} {vid} {len(Y)} segs  ({time.time() - t0:.0f}s)", flush=True)

    if a.train:
        vids = sorted(v for v in segs if (cache / (v + ".npz")).exists())
        rnd = random.Random(a.seed); rnd.shuffle(vids)
        nh = max(1, int(len(vids) * a.holdout)); held, trn = vids[:nh], vids[nh:]
        nv = max(1, int(len(trn) * 0.15)); val, trn = trn[:nv], trn[nv:]
        def load(vs):
            X, Y = [], []
            for v in vs:
                z = np.load(cache / (v + ".npz")); X.append(z["emb"].astype(np.float32)); Y.append(z["y"])
            return torch.from_numpy(np.concatenate(X)).to(dev), torch.from_numpy(np.concatenate(Y)).to(dev)
        Xt, Yt = load(trn); Xv, Yv = load(val); Xh, Yh = load(held)
        print(f"videos train {len(trn)} / val {len(val)} / held-out {len(held)}; segments {len(Yt)} / {len(Yv)} / {len(Yh)}", flush=True)
        W0 = rec.fc.weight.detach().float().clone(); b0 = rec.fc.bias.detach().float().clone()
        va, na = torch.from_numpy(rec.verb_of).to(dev), torch.from_numpy(rec.noun_of).to(dev)

        def score(W, b, X, Y):
            with torch.no_grad():
                p = torch.softmax(X @ W.T + b, -1); pa = p.argmax(-1)
                pv = torch.zeros(len(X), 97, device=dev).index_add_(1, va, p).argmax(-1); pn = torch.zeros(len(X), 300, device=dev).index_add_(1, na, p).argmax(-1)
                return float((pv == va[Y]).float().mean()), float((pn == na[Y]).float().mean()), float((pa == Y).float().mean())
        base = score(W0, b0, Xh, Yh); print("held-out BEFORE  verb %.3f noun %.3f action %.3f" % base, flush=True)
        W = W0.clone().requires_grad_(True); b = b0.clone().requires_grad_(True)
        opt = torch.optim.AdamW([W, b], lr=2e-4, weight_decay=0.0); best = (score(W0, b0, Xv, Yv)[2], W0.clone(), b0.clone()); lam = 5.0; bad = 0
        for ep in range(60):
            perm = torch.randperm(len(Yt), device=dev)
            for i in range(0, len(perm), 128):
                idx = perm[i:i + 128]; x = Xt[idx]; x = x + 0.02 * x.std() * torch.randn_like(x)
                loss = torch.nn.functional.cross_entropy(x @ W.T + b, Yt[idx], label_smoothing=0.1) + lam * ((W - W0) ** 2).sum() / W0.numel() * 100
                opt.zero_grad(); loss.backward(); opt.step()
            s = score(W.detach(), b.detach(), Xv, Yv)
            print(f"epoch {ep + 1} loss {float(loss):.3f}  val verb {s[0]:.3f} noun {s[1]:.3f} action {s[2]:.3f}", flush=True)
            if s[2] > best[0] + 1e-4: best = (s[2], W.detach().clone(), b.detach().clone()); bad = 0
            else:
                bad += 1
                if bad >= 6: break
        after = score(best[1], best[2], Xh, Yh); print("held-out AFTER   verb %.3f noun %.3f action %.3f" % after, flush=True)
        if after[2] > base[2]:
            torch.save({"weight": best[1].half().cpu(), "bias": best[2].half().cpu(), "held_out_before": base, "held_out_after": after, "train_videos": trn, "val_videos": val, "held_out_videos": held}, a.out)
            print("saved", a.out, "(held-out action top-1 improved %.3f -> %.3f)" % (base[2], after[2]))
        else:
            print("NOT saved: no held-out improvement; the public AVION head stays")


if __name__ == "__main__":
    main()
