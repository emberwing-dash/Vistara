"""Compile the highlight reel from the ORIGINAL footage (re-encoded cuts only, no synthesis)."""
from __future__ import annotations
import logging
import os
import subprocess
import tempfile
from pathlib import Path

log = logging.getLogger("vistara.export")


def ffmpeg_exe() -> str:
    import shutil
    exe = shutil.which("ffmpeg")
    if exe:
        return exe
    import imageio_ffmpeg
    return imageio_ffmpeg.get_ffmpeg_exe()


def export_highlight_video(src: str, segments: list[list[float]], out_path: str, height: int = 540) -> str:
    """Cut each [start,end] from src, re-encode to H.264/AAC (Unity VideoPlayer friendly) and concatenate."""
    ff = ffmpeg_exe()
    out = Path(out_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as td:
        parts = []
        for i, (s, e) in enumerate(segments):
            p = os.path.join(td, f"seg{i:03d}.mp4")
            cmd = [ff, "-y", "-loglevel", "error", "-ss", f"{s:.3f}", "-i", src, "-t", f"{e - s:.3f}",
                   "-vf", f"scale=-2:{height},format=yuv420p", "-c:v", "libx264", "-preset", "veryfast", "-crf", "24",
                   "-c:a", "aac", "-b:a", "128k", "-ac", "2", "-movflags", "+faststart", p]
            r = subprocess.run(cmd, capture_output=True, text=True)
            if r.returncode != 0 or not os.path.exists(p):
                log.warning("ffmpeg failed for segment %s: %s", (s, e), r.stderr[-300:])
                continue
            parts.append(p)
        if not parts:
            raise RuntimeError("highlight export produced no segments")
        lst = os.path.join(td, "list.txt")
        with open(lst, "w", encoding="utf-8") as f:
            for p in parts:
                f.write("file '" + p.replace("\\", "/") + "'\n")
        cmd = [ff, "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", lst, "-c", "copy",
               "-movflags", "+faststart", str(out)]
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0:
            raise RuntimeError("ffmpeg concat failed: " + r.stderr[-300:])
    return str(out)
