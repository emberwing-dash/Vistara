"""Command line entry: python -m vistara.cli <video.mp4> <out.json> [--team-id ID] [--highlight-video out.mp4]"""
from __future__ import annotations
import argparse
import json
import logging
import sys
from pathlib import Path


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(prog="vistara")
    ap.add_argument("video")
    ap.add_argument("out_json")
    ap.add_argument("--team-id", default=None)
    ap.add_argument("--highlight-video", default=None, help="optionally render the highlight reel (needs ffmpeg)")
    ap.add_argument("--analysis", default=None, help="optionally write the extended analysis JSON used by the Unity UI")
    ap.add_argument("-q", "--quiet", action="store_true")
    a = ap.parse_args(argv)
    logging.basicConfig(level=logging.WARNING if a.quiet else logging.INFO,
                        format="%(asctime)s %(name)s %(levelname)s %(message)s", stream=sys.stderr)
    from .config import Config
    from .pipeline import VistaraPipeline
    cfg = Config()
    if a.team_id:
        cfg.team_id = a.team_id
    if not Path(a.video).is_file():
        print(f"video not found: {a.video}", file=sys.stderr)
        return 2
    pipe = VistaraPipeline(cfg)
    res = pipe.analyse(a.video, progress=None if a.quiet else
                       (lambda st, f, m: print(f"[{st:<10}] {f * 100:5.1f}%  {m}", file=sys.stderr, end="\r" if f < 1 else "\n")))
    Path(a.out_json).parent.mkdir(parents=True, exist_ok=True)
    with open(a.out_json, "w", encoding="utf-8") as f:
        json.dump(res["output"], f, indent=1)
    if a.analysis:
        with open(a.analysis, "w", encoding="utf-8") as f:
            json.dump(res["analysis"], f)
    if a.highlight_video:
        from .export import export_highlight_video
        export_highlight_video(a.video, [h["segment"] for h in res["output"]["highlights"]], a.highlight_video)
    return 0


if __name__ == "__main__":
    sys.exit(main())
