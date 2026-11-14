"""Category / video / thumbnail endpoints for the hand-controlled Unity flow (Start -> Category -> Video -> Analyse -> MR HUD).

Categories are derived from the official manifest video ids: EPIC-KITCHENS-100 videos form "Kitchen"; FPVSum ids are
<Category>_<YouTubeID> (Bike, BikePolo, Boxing, Horse, Jumping, Longboarding, Motor, Parkour, Plane, RockClimbing, Scuba, Skate, Ski, Surfing).
GET /categories                 -> 15 categories with manifest counts and how many videos are available on this machine
GET /categories/{id}/videos     -> available (local) manifest videos of one category
GET /thumbnail?path=...         -> JPEG frame of a video (cached); GET /thumbnail/category/{id} -> frame of the category's first local video
"""
from __future__ import annotations
import csv
import hashlib
from pathlib import Path

from fastapi import APIRouter, HTTPException
from fastapi.responses import Response

from eval_api import ENGINE, _cfg, _rel

router = APIRouter()
ORDER = ["Kitchen", "Bike", "BikePolo", "Boxing", "Horse", "Jumping", "Longboarding", "Motor", "Parkour", "Plane",
         "RockClimbing", "Scuba", "Skate", "Ski", "Surfing"]
THUMB_DIR = ENGINE / "data" / "thumbs"


def _category(video_id: str, dataset: str) -> str:
    return "Kitchen" if dataset == "epic_kitchens_100" else video_id.split("_")[0]


def _label(cat: str) -> str:
    return "cooking" if cat == "Kitchen" else cat.lower()


def _local_videos() -> dict[str, Path]:
    c = _cfg()
    out: dict[str, Path] = {}
    for key in ("epic_root", "fpv_root"):
        for root in _rel(c, key) or []:
            for ext in ("*.MP4", "*.mp4"):
                for p in Path(root).rglob(ext):
                    out.setdefault(p.stem, p)
    return out


def _manifest():
    c = _cfg()
    return list(csv.DictReader(open(_rel(c, "manifest"), encoding="utf-8")))


@router.get("/categories")
def categories():
    rows, local = _manifest(), _local_videos()
    info = {k: {"id": k, "name": k, "dataset": "epic_kitchens_100" if k == "Kitchen" else "fpvsum", "label": _label(k),
                "manifest_count": 0, "available_count": 0} for k in ORDER}
    for r in rows:
        k = _category(r["video_id"], r["dataset"])
        if k in info:
            info[k]["manifest_count"] += 1
            info[k]["available_count"] += r["video_id"] in local
    return {"items": [info[k] for k in ORDER], "source": "manifest.csv", "manifest_total": len(rows)}


@router.get("/categories/{cat}/videos")
def category_videos(cat: str):
    if cat not in ORDER:
        raise HTTPException(404, "unknown category")
    local = _local_videos()
    items = []
    for r in _manifest():
        if _category(r["video_id"], r["dataset"]) == cat and r["video_id"] in local:
            items.append({"video_id": r["video_id"], "dataset": r["dataset"], "duration_sec": float(r["duration_sec"]),
                          "path": str(local[r["video_id"]])})
    items.sort(key=lambda x: x["duration_sec"])
    return {"category": cat, "count": len(items), "items": items}


def _frame_jpeg(path: Path, width: int = 480) -> bytes:
    import cv2
    THUMB_DIR.mkdir(parents=True, exist_ok=True)
    key = hashlib.sha1(f"{path}|{path.stat().st_mtime_ns}|{width}".encode()).hexdigest()[:16]
    cached = THUMB_DIR / f"{key}.jpg"
    if cached.exists():
        return cached.read_bytes()
    cap = cv2.VideoCapture(str(path))
    n = cap.get(cv2.CAP_PROP_FRAME_COUNT)
    cap.set(cv2.CAP_PROP_POS_FRAMES, max(0, int(n * 0.25)))
    ok, frame = cap.read()
    cap.release()
    if not ok:
        raise HTTPException(404, "cannot read a frame")
    h, w = frame.shape[:2]
    frame = cv2.resize(frame, (width, max(1, int(h * width / w))))
    ok, buf = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 85])
    cached.write_bytes(buf.tobytes())
    return buf.tobytes()


@router.get("/thumbnail")
def thumbnail(path: str):
    p = Path(path)
    if not p.is_file() or p.stem not in _local_videos():          # only serve frames of known evaluation videos
        raise HTTPException(404, "unknown video")
    return Response(_frame_jpeg(p), media_type="image/jpeg")


@router.get("/thumbnail/category/{cat}")
def category_thumbnail(cat: str):
    vids = category_videos(cat)["items"]
    if not vids:
        raise HTTPException(404, "no local video for this category")
    mid = vids[len(vids) // 2]
    return Response(_frame_jpeg(Path(mid["path"])), media_type="image/jpeg")


PLAYABLE_DIR = ENGINE / "data" / "playable"


@router.get("/playable")
def playable(path: str):
    """A Unity-friendly copy (H.264 yuv420p <= 480p + silent AAC track + faststart) of a known evaluation video.
    Some downloaded clips (video-only YouTube streams) do not prepare in Unity's VideoPlayer; analysis always uses the original file."""
    import subprocess
    from vistara.export import ffmpeg_exe
    p = Path(path)
    if not p.is_file() or p.stem not in _local_videos():
        raise HTTPException(404, "unknown video")
    PLAYABLE_DIR.mkdir(parents=True, exist_ok=True)
    out = PLAYABLE_DIR / f"{p.stem}_unity.mp4"
    if not out.exists() or out.stat().st_size == 0:
        tmp = out.with_suffix(".tmp.mp4")
        cmd = [ffmpeg_exe(), "-y", "-v", "error", "-i", str(p), "-f", "lavfi", "-i", "anullsrc=r=44100:cl=stereo",
               "-map", "0:v:0", "-map", "1:a:0", "-vf", "scale=-2:'min(480,ih)'", "-c:v", "libx264", "-preset", "veryfast",
               "-crf", "24", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "48k", "-shortest", "-movflags", "+faststart", str(tmp)]
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0 or not tmp.exists():
            raise HTTPException(500, "ffmpeg failed: " + r.stderr[-300:])
        tmp.replace(out)
    return {"path": str(out)}
