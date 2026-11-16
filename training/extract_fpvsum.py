"""Extract frozen-CLIP frame embeddings (+ importance cues) for FPVSum videos that are NOT in the scored manifest.

   python training/extract_fpvsum.py --videos E:/samsung_temp/TRAIN/data/fpvsum --out E:/samsung_temp/TRAIN/feats_fpvsum
The category comes from the file name (<Category>_<YouTubeID>.mp4), as in the public FPVSum list; manifest videos are refused.
"""
import argparse
import csv
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "code"))
from vistara.config import Config  # noqa: E402
from vistara.model import ClipEncoder  # noqa: E402
from vistara.video_io import probe, sample_video  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--videos", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    root = Path(__file__).resolve().parents[1]
    manifest = {r["video_id"] for r in csv.DictReader(open(root / "data" / "manifest.csv", encoding="utf-8"))}
    out = Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    cfg = Config()
    enc = ClipEncoder(cfg)
    for vp in sorted(Path(a.videos).glob("*.mp4")):
        vid = vp.stem
        if vid in manifest:
            print(f"[refuse] {vid} is in the evaluation manifest")
            continue
        if (out / f"{vid}.npz").exists():
            continue
        try:
            info = probe(str(vp))
            s = sample_video(str(vp), info, cfg.sample_fps, cfg.motion_gap_s, cfg.flow_width)
            emb = enc.encode_images(s.frames)
        except Exception as e:  # noqa: BLE001
            print(f"[skip] {vid}: {e}")
            continue
        np.savez_compressed(out / f"{vid}.npz", emb=emb.astype(np.float16), t=s.times,
                            hand=s.hand_motion, local=s.local_motion, glob=s.global_motion,
                            category=vid.split("_")[0].lower(), duration=info.duration)
        print(f"[ok] {vid}: {len(emb)} frames")


if __name__ == "__main__":
    main()
