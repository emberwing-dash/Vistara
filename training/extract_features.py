"""Extract frozen-CLIP window features + labels from EPIC train videos.

   python training/extract_features.py --videos data/epic/raw --ann data/epic/EPIC_100_train.csv --out data/epic/feats

Videos listed in data/manifest.csv are REFUSED (they are the scored evaluation set).
"""
import argparse
import csv
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "code"))
from vistara.config import Config  # noqa: E402
from vistara.features import motion_matrix, window_feature  # noqa: E402
from vistara.model import ClipEncoder  # noqa: E402
from vistara.video_io import probe, sample_video  # noqa: E402


def ts(t):
    h, m, s = t.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--videos", required=True)
    ap.add_argument("--ann", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--win", type=float, default=4.0)
    ap.add_argument("--hop", type=float, default=1.0)
    a = ap.parse_args()
    root = Path(__file__).resolve().parents[1]
    manifest = {r["video_id"] for r in csv.DictReader(open(root / "data" / "manifest.csv", encoding="utf-8"))}
    ann = {}
    for r in csv.DictReader(open(a.ann, encoding="utf-8")):
        ann.setdefault(r["video_id"], []).append(
            (ts(r["start_timestamp"]), ts(r["stop_timestamp"]), int(r["verb_class"]), int(r["noun_class"])))
    out = Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    cfg = Config()
    enc = ClipEncoder(cfg)
    vids = sorted(set(Path(a.videos).rglob("*.MP4")) | set(Path(a.videos).rglob("*.mp4")))
    for vp in vids:
        vid = vp.stem
        if vid in manifest:
            print(f"[refuse] {vid} is in the evaluation manifest")
            continue
        if vid not in ann or (out / f"{vid}.npz").exists():
            continue
        info = probe(str(vp))
        s = sample_video(str(vp), info, cfg.sample_fps, cfg.motion_gap_s, cfg.flow_width)
        emb = enc.encode_images(s.frames)
        mot = motion_matrix(s.hand_motion, s.local_motion, s.global_motion)
        X, V, N, T = [], [], [], []
        t0 = 0.0
        while t0 + a.win <= info.duration:
            f = window_feature(emb, mot, s.times, t0, t0 + a.win)
            if f is not None:
                best, lab = 0.0, (-1, -1)
                for (gs, ge, v, n) in ann[vid]:
                    ov = max(0.0, min(ge, t0 + a.win) - max(gs, t0)) / min(a.win, max(ge - gs, 1e-3))
                    if ov > best:
                        best, lab = ov, (v, n)
                X.append(f)
                T.append(t0)
                V.append(lab[0] if best >= 0.5 else -1)
                N.append(lab[1] if best >= 0.5 else -1)
            t0 += a.hop
        np.savez_compressed(out / f"{vid}.npz", X=np.array(X), verb=np.array(V), noun=np.array(N), t=np.array(T))
        print(f"[ok] {vid}: {len(X)} windows, {(np.array(V) >= 0).mean():.0%} foreground")


if __name__ == "__main__":
    main()
