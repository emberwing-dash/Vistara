"""Dev feature cache: decode each video ONCE (frames + motion), save the frames, and embed them with one or more CLIP backbones.

   python training/build_cache.py --videos <dir> [<dir> ...] --frames-cache <dir> \
       --backbones ViT-L-14-quickgelu:openai:<clip dir> --cache-dirs <dir> [--shard k/n] [--watch --delete-source]

The per-backbone files hold what the pipeline needs to analyse a video WITHOUT the video file (times, motion cues, embeddings, duration / fps /
size): VISTARA_FEATURE_CACHE=<dir> makes VistaraPipeline.analyse read them. --watch keeps polling for finished downloads and --delete-source removes a
video after its features are safely written (used for the large evaluation videos so disk space is not exhausted).
A development / bulk-processing aid: ./run.sh never uses the cache and always decodes the video itself."""
import argparse, os, sys, time, zlib
from pathlib import Path
import numpy as np
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "code"))
from vistara.config import Config
from vistara.model import ClipEncoder
from vistara.pipeline import feature_cache_file
from vistara.video_io import probe, sample_video

INFO = ("duration", "fps", "n_frames", "width", "height")


def info_fields(i):
    return dict(duration=i.duration, fps=i.fps, n_frames=i.n_frames, width=i.width, height=i.height)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--videos", nargs="+", required=True)
    ap.add_argument("--frames-cache", required=True)
    ap.add_argument("--backbones", nargs="+", required=True, help="model:pretrained:clipdir")
    ap.add_argument("--cache-dirs", nargs="+", required=True, help="one output dir per backbone")
    ap.add_argument("--shard", default="0/1", help="k/n: process every n-th video starting at k (run several in parallel)")
    ap.add_argument("--watch", action="store_true"); ap.add_argument("--delete-source", action="store_true")
    ap.add_argument("--protect", default=None, help="file with video ids that must never be deleted")
    ap.add_argument("--keep-frames", action="store_true", help="also keep the decoded frames (lets another backbone be embedded later)")
    a = ap.parse_args()
    fc = Path(a.frames_cache); fc.mkdir(parents=True, exist_ok=True)
    encs = []
    for spec, cdir in zip(a.backbones, a.cache_dirs):
        model, pre, clipdir = spec.split(":", 2)
        cfg = Config(); cfg.clip_model, cfg.clip_pretrained = model, pre
        encs.append((cfg, ClipEncoder(cfg, cache_dir=clipdir), Path(cdir)))
        print("backbone", model, pre, "dim", encs[-1][1].dim, flush=True)
    protect = set(open(a.protect).read().split()) if a.protect else set()
    cfg0 = Config(); sk, sn = (int(x) for x in a.shard.split("/")); done_ids = set()
    while True:
        vids = []
        for d in a.videos:
            vids += sorted(set(Path(d).rglob("*.MP4")) | set(Path(d).rglob("*.mp4")))
        did = 0
        for i, v in enumerate(vids):
            if zlib.crc32(v.stem.encode()) % sn != sk or v.stem in done_ids:
                continue
            todo = []
            for cfg, enc, cdir in encs:
                os.environ["VISTARA_FEATURE_CACHE"] = str(cdir)
                f = feature_cache_file(str(v), cfg)
                if not f.exists():
                    todo.append((cfg, enc, f))
            if todo:
                try:
                    fp = fc / f"{v.stem}.npz"
                    if fp.exists():
                        z = np.load(fp); s = dict(frames=z["frames"], times=z["times"], glob=z["glob"], local=z["local"], hand=z["hand"], sharp=z["sharp"])
                        s.update({k: z[k] for k in INFO if k in z.files})
                    else:
                        io = probe(str(v))
                        smp = sample_video(str(v), io, cfg0.sample_fps, cfg0.motion_gap_s, cfg0.flow_width)
                        s = dict(frames=smp.frames, times=smp.times, glob=smp.global_motion, local=smp.local_motion, hand=smp.hand_motion, sharp=smp.sharpness, **info_fields(io))
                        np.savez_compressed(fp, **s)
                    if "duration" not in s:                       # older frames cache: take the info from the video
                        s.update(info_fields(probe(str(v))))
                    for cfg, enc, f in todo:
                        emb = enc.encode_images(s["frames"])
                        f.parent.mkdir(parents=True, exist_ok=True)
                        np.savez_compressed(f, times=s["times"], glob=s["glob"], local=s["local"], hand=s["hand"], sharp=s["sharp"], emb=emb.astype(np.float16), **{k: s[k] for k in INFO})
                    did += 1
                    print(f"[{len(done_ids) + 1}] cached {v.stem}  ({s['duration']:.1f} s)", flush=True)
                except Exception as e:  # noqa: BLE001
                    print(f"[skip] {v.stem}: {e}", flush=True)
                    continue
            ok = True
            for cfg, enc, cdir in encs:                         # all feature files present?
                os.environ["VISTARA_FEATURE_CACHE"] = str(cdir)
                ok &= feature_cache_file(str(v), cfg).exists()
            if ok:
                done_ids.add(v.stem)
                if a.delete_source and v.stem not in protect:
                    try:
                        v.unlink(); print(f"      deleted source video {v.name}", flush=True)
                    except OSError as e:
                        print(f"      could not delete {v.name}: {e}", flush=True)
                if not a.keep_frames and a.delete_source:
                    fp = fc / f"{v.stem}.npz"
                    if fp.exists():
                        fp.unlink()
        if not a.watch:
            break
        time.sleep(30)


if __name__ == "__main__":
    main()
