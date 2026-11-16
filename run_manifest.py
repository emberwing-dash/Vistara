"""Batch inference over manifest.csv with the model loaded once.
   python run_manifest.py --videos <dir containing <video_id>.mp4> [--out outputs] [--team-id ID]
Equivalent to calling ./run.sh per video, but without reloading CLIP each time."""
import argparse, csv, json, logging, sys, traceback
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent / "code"))
from vistara.config import Config
from vistara.pipeline import VistaraPipeline


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--videos", required=True)
    ap.add_argument("--out", default=str(Path(__file__).parent / "outputs"))
    ap.add_argument("--manifest", default=str(Path(__file__).parent / "data" / "manifest.csv"))
    ap.add_argument("--team-id", default=None)
    ap.add_argument("--redo", action="store_true", help="re-run videos that already have an output")
    a = ap.parse_args()
    logging.basicConfig(level=logging.WARNING)
    cfg = Config()
    if a.team_id:
        cfg.team_id = a.team_id
    pipe = VistaraPipeline(cfg)
    out = Path(a.out); out.mkdir(parents=True, exist_ok=True)
    rows = list(csv.DictReader(open(a.manifest, encoding="utf-8")))
    done = failed = 0
    for r in rows:
        vid = r["video_id"]; dst = out / f"{vid}.json"
        if dst.exists() and not a.redo:
            continue
        src = next((p for e in ("mp4", "MP4", "mkv", "webm", "mov") if (p := Path(a.videos) / f"{vid}.{e}").exists()), None)
        if src is None:
            print(f"[skip] {vid}: video file not found"); failed += 1; continue
        try:
            res = pipe.analyse(str(src), video_id=vid)
            if abs(res["output"]["duration_sec"] - float(r["duration_sec"])) > 0.5:
                print(f"[warn] {vid}: decoded duration {res['output']['duration_sec']} vs manifest {r['duration_sec']}")
            json.dump(res["output"], open(dst, "w", encoding="utf-8"), indent=1)
            done += 1; print(f"[ok] {vid} ({done}/{len(rows)})")
        except Exception:
            failed += 1; traceback.print_exc()
    print(f"done={done} failed_or_missing={failed}")


if __name__ == "__main__":
    main()
