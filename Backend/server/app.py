"""Local HTTP API for the Unity front end.

    uvicorn app:app --host 127.0.0.1 --port 8765        (from Backend/server, with PYTHONPATH=../code)

Workflow: POST /analyze (multipart video) -> {job_id}; poll GET /jobs/{id}; fetch result / highlight video.
"""
from __future__ import annotations
import json
import logging
import os
import shutil
import sys
import threading
import time
import uuid
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "code"))

from fastapi import FastAPI, File, HTTPException, Request, UploadFile  # noqa: E402
from fastapi.middleware.cors import CORSMiddleware  # noqa: E402
from fastapi.responses import FileResponse  # noqa: E402
from pydantic import BaseModel  # noqa: E402

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(name)s %(levelname)s %(message)s")
log = logging.getLogger("vistara.server")

JOBS_DIR = HERE.parent / "data" / "jobs"
JOBS_DIR.mkdir(parents=True, exist_ok=True)

app = FastAPI(title="Vistara Engine", version="1.0.0")
app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])

_pipe = None
_pipe_lock = threading.Lock()
_gpu_lock = threading.Lock()          # one analysis at a time on the GPU
_jobs: dict[str, dict] = {}


def pipeline():
    global _pipe
    with _pipe_lock:
        if _pipe is None:
            from vistara.pipeline import VistaraPipeline
            _pipe = VistaraPipeline()
        return _pipe


def _save_meta(job: dict):
    d = JOBS_DIR / job["id"]
    with open(d / "meta.json", "w", encoding="utf-8") as f:
        json.dump(job, f)


def _run_job(job_id: str, src: str):
    job = _jobs[job_id]
    d = JOBS_DIR / job_id

    def progress(stage: str, frac: float, msg: str):
        order = {"probe": 0.0, "decode": 0.02, "embed": 0.55, "recognise": 0.75, "importance": 0.82,
                 "highlights": 0.88, "export": 0.9, "done": 1.0}
        span = {"probe": 0.02, "decode": 0.53, "embed": 0.20, "recognise": 0.07, "importance": 0.06,
                "highlights": 0.02, "export": 0.1, "done": 0.0}
        job.update(stage=stage, message=msg, progress=round(order.get(stage, 0) + span.get(stage, 0) * frac, 4))

    with _gpu_lock:
        try:
            job.update(status="running", started=time.time())
            res = pipeline().analyse(src, video_id=job["video_id"], progress=progress)
            with open(d / "result.json", "w", encoding="utf-8") as f:
                json.dump(res["output"], f, indent=1)
            with open(d / "analysis.json", "w", encoding="utf-8") as f:
                json.dump(res["analysis"], f)
            progress("export", 0.0, "Rendering highlight reel")
            from vistara.export import export_highlight_video
            export_highlight_video(src, [h["segment"] for h in res["output"]["highlights"]], str(d / "highlight.mp4"))
            job.update(status="done", stage="done", progress=1.0, message="Analysis complete", finished=time.time())
        except Exception as e:  # noqa: BLE001
            log.exception("job %s failed", job_id)
            job.update(status="error", message=str(e), error=str(e), finished=time.time())
        finally:
            _save_meta(job)


def _new_job(filename: str) -> tuple[str, Path]:
    jid = uuid.uuid4().hex[:12]
    d = JOBS_DIR / jid
    d.mkdir(parents=True)
    _jobs[jid] = {"id": jid, "video_id": Path(filename).stem, "filename": filename, "status": "queued",
                  "stage": "queued", "progress": 0.0, "message": "Queued", "created": time.time()}
    return jid, d


@app.get("/health")
def health():
    import torch
    return {"status": "ok", "cuda": torch.cuda.is_available(), "model_loaded": _pipe is not None}


@app.post("/analyze")
async def analyze(file: UploadFile = File(...)):
    jid, d = _new_job(file.filename or "video.mp4")
    src = d / ("source" + (Path(file.filename or "video.mp4").suffix or ".mp4"))
    with open(src, "wb") as f:
        shutil.copyfileobj(file.file, f)
    _jobs[jid]["source"] = str(src)
    _save_meta(_jobs[jid])
    threading.Thread(target=_run_job, args=(jid, str(src)), daemon=True).start()
    return {"job_id": jid}


def _download_url(job_id: str, url: str) -> str:
    """Fetch a small (<=100 MB, <=480p) copy of a public video with yt-dlp into the job folder."""
    import subprocess
    d = JOBS_DIR / job_id
    job = _jobs[job_id]
    job.update(status="running", stage="download", message="Fetching a small copy of the video", progress=0.01)
    out_t = str(d / "source.%(ext)s")
    from vistara.export import ffmpeg_exe
    ff = ffmpeg_exe()
    fmt = ("b[height<=480][vcodec^=avc1]/bv*[height<=480][vcodec^=avc1]+ba[ext=m4a]/"
           "b[height<=480]/bv*[height<=480]+ba/w")
    cmd = [sys.executable, "-m", "yt_dlp", "-f", fmt, "--ffmpeg-location", ff, "--merge-output-format", "mp4",
           "--max-filesize", "100M", "--no-playlist", "--no-warnings", "-o", out_t,
           "--print", "after_move:filepath", "--print", "title", url]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=600)
    lines = [l for l in r.stdout.splitlines() if l.strip()]
    if r.returncode != 0 or len(lines) < 2:
        raise RuntimeError("download failed: " + (r.stderr.strip().splitlines() or ["unknown error"])[-1])
    job["video_id"] = "".join(c if c.isalnum() or c in "-_" else "_" for c in lines[-2][:40]) or job["video_id"]
    job["filename"] = lines[0][:80]
    path = Path(lines[-1] if Path(lines[-1]).exists() else lines[-2])
    if path.suffix.lower() != ".mp4":      # Unity's VideoPlayer is happiest with H.264/AAC mp4
        mp4 = path.with_suffix(".mp4")
        job.update(message="Converting to H.264 mp4")
        t = subprocess.run([ff, "-y", "-loglevel", "error", "-i", str(path), "-c:v", "libx264", "-preset", "veryfast",
                            "-crf", "24", "-pix_fmt", "yuv420p", "-c:a", "aac", str(mp4)], capture_output=True, text=True)
        if t.returncode != 0 or not mp4.exists():
            raise RuntimeError("conversion failed: " + t.stderr[-200:])
        path.unlink(missing_ok=True)
        path = mp4
    return str(path)


class UrlReq(BaseModel):
    url: str


@app.post("/analyze_url")
def analyze_url(req: UrlReq):
    if not req.url.lower().startswith(("http://", "https://")):
        raise HTTPException(400, "url must be http(s)")
    jid, d = _new_job("youtube")

    def work():
        try:
            src = _download_url(jid, req.url)
            _jobs[jid]["source"] = src
            _save_meta(_jobs[jid])
            _run_job(jid, src)
        except Exception as e:  # noqa: BLE001
            log.exception("url job failed")
            _jobs[jid].update(status="error", message=str(e), error=str(e))
            _save_meta(_jobs[jid])
    threading.Thread(target=work, daemon=True).start()
    return {"job_id": jid}


@app.post("/analyze_raw")
async def analyze_raw(request: Request, filename: str = "video.mp4"):
    """Streams the raw request body to disk (used by Unity's UploadHandlerFile; no multipart overhead)."""
    jid, d = _new_job(filename)
    src = d / ("source" + (Path(filename).suffix or ".mp4"))
    with open(src, "wb") as f:
        async for chunk in request.stream():
            f.write(chunk)
    _jobs[jid]["source"] = str(src)
    _save_meta(_jobs[jid])
    threading.Thread(target=_run_job, args=(jid, str(src)), daemon=True).start()
    return {"job_id": jid}


class PathReq(BaseModel):
    path: str


@app.post("/analyze_path")
def analyze_path(req: PathReq):
    """Local-machine shortcut (same host as Unity): avoids copying multi-hundred-MB videos."""
    p = Path(req.path)
    if not p.is_file():
        raise HTTPException(404, f"file not found: {req.path}")
    jid, d = _new_job(p.name)
    _jobs[jid]["source"] = str(p)
    _save_meta(_jobs[jid])
    threading.Thread(target=_run_job, args=(jid, str(p)), daemon=True).start()
    return {"job_id": jid}


def _job(jid: str) -> dict:
    if jid in _jobs:
        return _jobs[jid]
    m = JOBS_DIR / jid / "meta.json"
    if m.exists():
        _jobs[jid] = json.load(open(m, encoding="utf-8"))
        return _jobs[jid]
    raise HTTPException(404, "unknown job")


@app.get("/jobs/{jid}")
def job_status(jid: str):
    return _job(jid)


@app.get("/jobs/{jid}/result")
def job_result(jid: str):
    j = _job(jid)
    if j["status"] != "done":
        raise HTTPException(409, f"job is {j['status']}")
    d = JOBS_DIR / jid
    return {"output": json.load(open(d / "result.json", encoding="utf-8")),
            "analysis": json.load(open(d / "analysis.json", encoding="utf-8")),
            "highlight_path": str(d / "highlight.mp4"), "source_path": j.get("source", "")}


@app.get("/jobs/{jid}/highlight")
def job_highlight(jid: str):
    p = JOBS_DIR / jid / "highlight.mp4"
    if not p.exists():
        raise HTTPException(404, "highlight video not ready")
    return FileResponse(p, media_type="video/mp4")


@app.get("/jobs/{jid}/source")
def job_source(jid: str):
    j = _job(jid)
    p = Path(j.get("source", ""))
    if not p.is_file():
        raise HTTPException(404, "source missing")
    return FileResponse(p, media_type="video/mp4")


@app.get("/history")
def history(limit: int = 20):
    items = []
    for m in sorted(JOBS_DIR.glob("*/meta.json"), key=lambda x: x.stat().st_mtime, reverse=True)[:limit]:
        try:
            items.append(json.load(open(m, encoding="utf-8")))
        except Exception:  # noqa: BLE001
            continue
    return {"items": items}


@app.get("/jobs/{jid}/result_json")
def job_result_json(jid: str):
    """The exact official-schema JSON (what run.sh would write)."""
    p = JOBS_DIR / jid / "result.json"
    if not p.exists():
        raise HTTPException(404, "result not ready")
    return FileResponse(p, media_type="application/json")
