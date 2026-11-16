"""Window features + verb/noun labels for the action head, built from the dev feature cache (no video decoding).

   python training/windows_from_cache.py --cache E:/.../cache_l14qg --videos E:/.../EPIC-KITCHENS --ann EPIC_100_train.csv --out E:/.../feats_l14_w2 --win 2.0

Same feature definition as vistara.features.window_feature (mean embedding, last-first difference, 3 motion cues). Manifest videos are refused."""
import argparse, csv, sys
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "code"))
from vistara.features import motion_matrix, window_feature  # noqa: E402


def ts(t):
    h, m, s = t.split(":"); return int(h) * 3600 + int(m) * 60 + float(s)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", required=True); ap.add_argument("--videos", required=True); ap.add_argument("--ann", required=True)
    ap.add_argument("--out", required=True); ap.add_argument("--win", type=float, default=4.0); ap.add_argument("--hop", type=float, default=1.0)
    a = ap.parse_args()
    man = {r["video_id"] for r in csv.DictReader(open(ROOT / "data" / "manifest.csv", encoding="utf-8"))}
    ann = {}
    for r in csv.DictReader(open(a.ann, encoding="utf-8")):
        ann.setdefault(r["video_id"], []).append((ts(r["start_timestamp"]), ts(r["stop_timestamp"]), int(r["verb_class"]), int(r["noun_class"])))
    out = Path(a.out); out.mkdir(parents=True, exist_ok=True)
    n = 0
    for vp in sorted(Path(a.videos).rglob("*.MP4")):
        vid = vp.stem
        if vid in man or vid not in ann:
            continue
        hits = sorted(Path(a.cache).glob(f"{vid}_*.npz"))
        if not hits:
            continue
        z = np.load(hits[0]); emb = z["emb"].astype(np.float32); times = z["times"]
        mot = motion_matrix(z["hand"], z["local"], z["glob"])
        dur = times[-1] + 0.5
        X, V, N, T = [], [], [], []
        t0 = 0.0
        while t0 + a.win <= dur:
            f = window_feature(emb, mot, times, t0, t0 + a.win)
            if f is not None:
                best, lab = 0.0, (-1, -1)
                for (s, e, v, nn) in ann[vid]:
                    ov = max(0.0, min(e, t0 + a.win) - max(s, t0)) / min(a.win, max(e - s, 1e-3))
                    if ov > best: best, lab = ov, (v, nn)
                X.append(f); T.append(t0); V.append(lab[0] if best >= 0.5 else -1); N.append(lab[1] if best >= 0.5 else -1)
            t0 += a.hop
        np.savez_compressed(out / f"{vid}.npz", X=np.array(X), verb=np.array(V), noun=np.array(N), t=np.array(T)); n += 1
    print(f"{n} videos -> {out}")


if __name__ == "__main__":
    main()
