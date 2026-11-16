"""Produce outputs/<video_id>.json for EVERY video in data/manifest.csv.

   python make_outputs.py --roots <dir with the videos> [<dir> ...] [--features <feature cache>] [--out outputs]

For each manifest video, in this order:
  1. the video file is found under --roots            -> the full pipeline runs on it (same code as ./run.sh, model loaded once)
  2. no file, but a dev feature cache entry exists   -> the same pipeline runs on the cached features (identical computation, features extracted earlier)
  3. neither                                          -> a clearly DEGRADED fallback JSON built from the manifest duration only: label "other" (score 0),
                                                         no actions, evenly spaced 4 s highlight segments inside the 15 % budget. NO model inference is
                                                         claimed for it; ids are listed in outputs_degraded.txt.
Every file is validated (schema, 15 % budget, manifest duration)."""
import argparse, csv, json, logging, os, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent / "code"))
from vistara.config import Config  # noqa: E402
from vistara.highlights import budget_seconds  # noqa: E402
from vistara.schema import validate  # noqa: E402


def degraded(video_id: str, duration: float, cfg: Config) -> dict:
    budget = max(budget_seconds(duration, cfg), cfg.min_segment_s)
    n = max(1, int(budget // cfg.min_segment_s))
    segs = []
    for i in range(n):
        s0 = round(min(max(0.0, duration * (i + 0.5) / n - cfg.min_segment_s / 2), max(0.0, duration - cfg.min_segment_s)), 2)
        segs.append({"segment": [s0, round(min(duration, s0 + cfg.min_segment_s), 2)]})
    return {"team_id": cfg.team_id, "video_id": video_id, "duration_sec": round(duration, 2), "label": {"name": "other", "score": 0.0},
            "actions": [], "highlights": segs}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--roots", nargs="*", default=[]); ap.add_argument("--features", default=None)
    ap.add_argument("--manifest", default=str(Path(__file__).parent / "data" / "manifest.csv"))
    ap.add_argument("--out", default=str(Path(__file__).parent / "outputs")); ap.add_argument("--team-id", default=None)
    ap.add_argument("--redo", action="store_true"); ap.add_argument("--no-features-fallback", action="store_true")
    a = ap.parse_args()
    logging.basicConfig(level=logging.WARNING)
    if a.features:
        os.environ["VISTARA_FEATURE_CACHE"] = a.features
    cfg = Config()
    if a.team_id:
        cfg.team_id = a.team_id
    rows = list(csv.DictReader(open(a.manifest, encoding="utf-8")))
    files = {}
    for r in a.roots:
        for p in Path(r).rglob("*"):
            if p.suffix.lower() == ".mp4":
                files.setdefault(p.stem, p)
    from vistara.pipeline import VistaraPipeline, feature_cache_file
    pipe = None
    out = Path(a.out); out.mkdir(parents=True, exist_ok=True)
    stats = {"video": 0, "features": 0, "degraded": 0, "failed": 0}; degraded_ids = []
    for r in rows:
        vid, dur = r["video_id"], float(r["duration_sec"]); dst = out / f"{vid}.json"
        if dst.exists() and not a.redo:
            continue
        src = files.get(vid)
        cache_hit = False
        if src is None and a.features and not a.no_features_fallback:
            cf = feature_cache_file(f"{vid}.mp4", cfg)
            cache_hit = cf is not None and cf.exists()
        res = None
        if src is not None or cache_hit:
            try:
                if pipe is None:
                    pipe = VistaraPipeline(cfg)
                res = pipe.analyse(str(src) if src is not None else f"{vid}.mp4", video_id=vid)["output"]
                stats["video" if src is not None else "features"] += 1
            except Exception as e:  # noqa: BLE001
                logging.exception("analysis failed for %s (%s)", vid, e); stats["failed"] += 1
        if res is None:
            res = degraded(vid, dur, cfg); stats["degraded"] += 1; degraded_ids.append(vid)
        errs, _ = validate(res, manifest_duration=dur)
        if errs:
            print(f"[INVALID] {vid}: {errs}")
        tmp = str(dst) + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(res, f, indent=1)
        os.replace(tmp, dst)
        print(f"{vid}: {'model' if res['label']['score'] > 0 or res['actions'] else 'degraded'}", flush=True)
    (out.parent / "outputs_degraded.txt").write_text("\n".join(degraded_ids) + ("\n" if degraded_ids else ""), encoding="utf-8")
    print("summary:", stats, "| degraded ids:", len(degraded_ids))


if __name__ == "__main__":
    main()
