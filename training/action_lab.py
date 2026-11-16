"""Window-level verb / noun recognition lab on cached features of EPIC TRAIN videos (annotations: EPIC_100_train.csv).

Compares backbones, prior strengths and window lengths for the zero-shot recogniser (no training involved, so every train video is a fair test),
and trains the head with participant-held-out validation.

   python training/action_lab.py --cache E:/.../cache_l14qg --model ViT-L-14-quickgelu --clip-dir E:/.../backbones --train-videos E:/.../EPIC-KITCHENS
"""
import argparse, csv, sys, os
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "code"))
os.environ.setdefault("VISTARA_WEIGHTS", "E:/samsung_temp/EXP/nohead")
from vistara.config import Config  # noqa: E402
from vistara.model import ClipEncoder  # noqa: E402
from vistara.recognizer import Recognizer  # noqa: E402


def ts(t):
    h, m, s = t.split(":"); return int(h) * 3600 + int(m) * 60 + float(s)


def load_ann(path, vids):
    ann = {}
    for r in csv.DictReader(open(path, encoding="utf-8")):
        if r["video_id"] in vids:
            ann.setdefault(r["video_id"], []).append((ts(r["start_timestamp"]), ts(r["stop_timestamp"]), int(r["verb_class"]), int(r["noun_class"])))
    return ann


def windows(emb, times, ann, W, hop=1.0):
    X, V, N, T = [], [], [], []
    dur = times[-1] + 0.5
    t0 = 0.0
    while t0 + W <= dur:
        idx = np.where((times >= t0) & (times < t0 + W))[0]
        if len(idx):
            m = emb[idx].mean(0); m /= np.linalg.norm(m) + 1e-9
            best, lab = 0.0, (-1, -1)
            for (s, e, v, n) in ann:
                ov = max(0.0, min(e, t0 + W) - max(s, t0)) / min(W, max(e - s, 1e-3))
                if ov > best: best, lab = ov, (v, n)
            if best >= 0.5:
                X.append(m); V.append(lab[0]); N.append(lab[1]); T.append(t0)
        t0 += hop
    return np.array(X), np.array(V), np.array(N), np.array(T)


def topk(logits, y, k):
    top = np.argsort(-logits, 1)[:, :k]
    return float((top == y[:, None]).any(1).mean())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", required=True); ap.add_argument("--model", default="ViT-B-32-quickgelu"); ap.add_argument("--clip-dir", default=None)
    ap.add_argument("--train-videos", required=True)
    ap.add_argument("--ann", default="E:/samsung_temp/epic-kitchens-100-annotations/EPIC_100_train.csv")
    a = ap.parse_args()
    cfg = Config(); cfg.clip_model = a.model; cfg.use_data_prior = False
    enc = ClipEncoder(cfg, cache_dir=a.clip_dir)
    rec = Recognizer(enc, cfg)
    vids = sorted(p.stem for p in Path(a.train_videos).rglob("*.MP4"))
    cache = Path(a.cache)
    data = {}
    for v in vids:
        hits = sorted(cache.glob(f"{v}_*.npz"))
        if hits:
            z = np.load(hits[0]); data[v] = (z["times"], z["emb"].astype(np.float32))
    ann = load_ann(a.ann, set(data))
    print(f"backbone {a.model}: {len(data)} cached train videos with annotations: {len(ann)}")
    # priors from the OTHER train videos only (leave-these-out), so the evaluation stays unbiased
    sys.path.insert(0, str(ROOT / "training"))
    import subprocess
    pri = "E:/samsung_temp/EXP/prior_excl.npz"
    subprocess.run([sys.executable, str(ROOT / "training" / "build_priors.py"), "--ann", a.ann, "--out", pri, "--exclude", ",".join(data)], check=True, capture_output=True)
    z = np.load(pri)
    nl = z["noun_logp"] - z["noun_logp"].max(); vl = z["verb_logp"] - z["verb_logp"].max()
    nl = np.concatenate([nl, [nl.min()]]); vl = np.concatenate([vl, [vl.min()]])
    pen_n = rec.noun_logprior - (-0.35 * np.log(np.arange(len(rec.nouns)) + 8))        # keep the hand-written penalties, drop the rank prior
    pen_v = rec.verb_logprior - (-0.35 * np.log(np.arange(len(rec.verbs)) + 8))
    for W in (2.0, 4.0):
        Xs, Vs, Ns = [], [], []
        for v in data:
            if v not in ann: continue
            X, V, N, _ = windows(data[v][1], data[v][0], ann[v], W)
            if len(X): Xs.append(X); Vs.append(V); Ns.append(N)
        X = np.concatenate(Xs); V = np.concatenate(Vs); N = np.concatenate(Ns)
        print(f"\n== window {W:.0f}s: {len(X)} labelled windows ==")
        ln = X @ rec.noun_emb[:300].T * 100.0; lv = X @ rec.verb_emb[:97].T * 100.0
        rank_n = rec.noun_logprior[:300]; rank_v = rec.verb_logprior[:97]
        print(f"  zero-shot, rank prior (old)      noun top1 {topk(ln + rank_n, N, 1):.3f} top5 {topk(ln + rank_n, N, 5):.3f} | verb top1 {topk(lv + rank_v, V, 1):.3f} top5 {topk(lv + rank_v, V, 5):.3f}")
        print(f"  zero-shot, no prior              noun top1 {topk(ln, N, 1):.3f} top5 {topk(ln, N, 5):.3f} | verb top1 {topk(lv, V, 1):.3f} top5 {topk(lv, V, 5):.3f}")
        for g in (0.05, 0.1, 0.15, 0.25, 0.4, 0.6):
            sn = ln + g * nl[:300] / 0.15 * 0.15 * 10 + pen_n[:300] * 0; sv = lv + g * vl[:97] * 10
            print(f"  zero-shot, data prior x{g:<4}       noun top1 {topk(sn, N, 1):.3f} top5 {topk(sn, N, 5):.3f} | verb top1 {topk(sv, V, 1):.3f} top5 {topk(sv, V, 5):.3f}")
        # frequency-only baselines
        print(f"  prior only (most frequent)        noun top1 {topk(np.tile(nl[:300], (len(N), 1)), N, 1):.3f} | verb top1 {topk(np.tile(vl[:97], (len(V), 1)), V, 1):.3f}")


if __name__ == "__main__":
    main()
