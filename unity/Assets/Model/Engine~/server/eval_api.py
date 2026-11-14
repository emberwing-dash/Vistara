"""Model information + repeatable held-out evaluation endpoints (used by the Unity 'Test Lab').

GET  /model_info        weights, hashes, labels, device, trained-head metadata
GET  /eval/videos       manifest videos that are present on this machine (the evaluation set)
POST /eval/start        run evaluation/evaluate_heldout.py in a background process (real pipeline, real ground truth)
GET  /eval/status       progress of the running/last evaluation
GET  /eval/results      eval_summary.json + per-video rows (CSV) of the last finished evaluation
Paths come from Config/eval_config.json (Unity: Assets/Model/Config) or the VISTARA_EVAL_CONFIG env var.
"""
from __future__ import annotations
import csv
import hashlib
import json
import os
import subprocess
import sys
from pathlib import Path

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

HERE = Path(__file__).resolve().parent
ENGINE = HERE.parent
router = APIRouter()
_proc: subprocess.Popen | None = None


def _cfg() -> dict:
    p = Path(os.environ.get("VISTARA_EVAL_CONFIG", ENGINE.parent / "Config" / "eval_config.json"))
    if not p.exists():
        raise HTTPException(404, f"evaluation config not found: {p}")
    c = json.load(open(p, encoding="utf-8"))
    c["_dir"] = str(p.parent)
    return c


def _rel(c: dict, key: str):
    v = c.get(key)
    if isinstance(v, list):
        return [str((Path(c["_dir"]) / x).resolve()) if not Path(x).is_absolute() else x for x in v]
    if v is None:
        return None
    return str((Path(c["_dir"]) / v).resolve()) if not Path(v).is_absolute() else v


def _results_dir(c: dict) -> Path:
    return Path(_rel(c, "results_dir") or ENGINE.parent / "Results")


def _sha(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()[:16]


@router.get("/model_info")
def model_info():
    from vistara import __version__
    from vistara.config import CLIP_DIR, WEIGHTS_DIR, Config
    import torch
    cfg = Config()
    info = {"engine_version": __version__, "device": "cuda" if torch.cuda.is_available() else "cpu",
            "backbone": f"{cfg.clip_model} ({cfg.clip_pretrained}) via open_clip", "backbone_dir": str(CLIP_DIR),
            "weights_dir": str(WEIGHTS_DIR), "heads": []}
    for name in ("head.pt", "label_head.pt"):
        p = WEIGHTS_DIR / name
        if p.exists():
            ck = torch.load(p, map_location="cpu")
            meta = dict(ck.get("meta", {}))
            meta.pop("train_videos", None); meta.pop("val_videos", None)
            info["heads"].append({"name": name, "sha256_16": _sha(p), "bytes": p.stat().st_size,
                                  "blend": float(meta.get("blend", 0.0)), "best_epoch": int(meta.get("best_epoch", 0)),
                                  "heldout_zero_shot": float(meta.get("heldout_zero_shot", meta.get("heldout_zero_shot_verb_noun", 0.0))),
                                  "heldout_trained": float(meta.get("heldout_trained", meta.get("heldout_blend_verb_noun", 0.0))),
                                  "n_videos": int(meta.get("n_videos", 0))})
    return info


@router.get("/eval/videos")
def eval_videos():
    c = _cfg()
    man = {r["video_id"]: r for r in csv.DictReader(open(_rel(c, "manifest"), encoding="utf-8"))}
    found, seen = [], set()
    for key, ds in (("epic_root", "epic_kitchens_100"), ("fpv_root", "fpvsum")):
        for root in _rel(c, key) or []:
            for ext in ("*.MP4", "*.mp4"):
                for p in Path(root).rglob(ext):
                    if p.stem in man and man[p.stem]["dataset"] == ds and p.stem not in seen:
                        seen.add(p.stem)
                        found.append({"video_id": p.stem, "dataset": ds, "duration_sec": float(man[p.stem]["duration_sec"]),
                                      "path": str(p)})
    found.sort(key=lambda x: (x["dataset"], x["duration_sec"]))
    return {"count": len(found), "manifest_total": len(man), "items": found}


class EvalReq(BaseModel):
    limit: int = 0


@router.post("/eval/start")
def eval_start(req: EvalReq):
    global _proc
    if _proc is not None and _proc.poll() is None:
        raise HTTPException(409, "evaluation already running")
    c = _cfg()
    out = _results_dir(c)
    out.mkdir(parents=True, exist_ok=True)
    cmd = [sys.executable, str(ENGINE / "evaluation" / "evaluate_heldout.py"), "--manifest", _rel(c, "manifest"),
           "--classes", str(ENGINE / "code" / "vistara" / "supported_classes.json"), "--out", str(out),
           "--epic-ann", _rel(c, "epic_ann"), "--fpv-gt", _rel(c, "fpv_gt"), "--status", str(out / "eval_status.json"),
           "--epic-root", *(_rel(c, "epic_root") or []), "--fpv-root", *(_rel(c, "fpv_root") or [])]
    if req.limit:
        cmd += ["--limit", str(req.limit)]
    json.dump({"state": "starting", "done": 0, "total": 0}, open(out / "eval_status.json", "w"))
    log = open(out / "eval_log.txt", "w", encoding="utf-8")
    _proc = subprocess.Popen(cmd, stdout=log, stderr=subprocess.STDOUT, cwd=str(ENGINE),
                             env={**os.environ, "PYTHONPATH": str(ENGINE / "code"), "PYTHONIOENCODING": "utf-8"})
    return {"started": True, "pid": _proc.pid}


@router.get("/eval/status")
def eval_status():
    c = _cfg()
    p = _results_dir(c) / "eval_status.json"
    st = json.load(open(p, encoding="utf-8")) if p.exists() else {"state": "idle", "done": 0, "total": 0}
    if _proc is not None and _proc.poll() not in (None, 0) and st.get("state") != "done":
        st = {**st, "state": "error", "message": f"evaluation process exited with code {_proc.poll()}"}
    return st


@router.get("/eval/results")
def eval_results():
    c = _cfg()
    d = _results_dir(c)
    sp = d / "eval_summary.json"
    if not sp.exists():
        raise HTTPException(404, "no evaluation results yet")
    rows = []
    cp = d / "eval_per_video.csv"
    if cp.exists():
        def num(x, default=-1.0):
            try:
                return float(x)
            except (TypeError, ValueError):
                return default
        for r in csv.DictReader(open(cp, encoding="utf-8")):
            rows.append({"video_id": r["video_id"], "dataset": r["dataset"], "duration_sec": num(r.get("duration_sec")),
                         "label_pred": r.get("label_pred", ""), "label_true": r.get("label_true", ""),
                         "n_actions": int(num(r.get("n_actions"), 0)), "highlight_frac": num(r.get("highlight_frac")),
                         "infer_sec": num(r.get("infer_sec")), "verb_acc": num(r.get("verb_acc")), "noun_acc": num(r.get("noun_acc")),
                         "pair_acc": num(r.get("pair_acc")), "action_map": num(r.get("video_action_map")),
                         "coverage": num(r.get("video_action_coverage")), "highlight_f1": num(r.get("highlight_f1"))})
    def clean(x):          # Unity JsonUtility cannot read null numbers
        if isinstance(x, dict):
            return {k: clean(v) for k, v in x.items()}
        return -1.0 if x is None else x
    sm = clean(json.load(open(sp, encoding="utf-8")))
    return {"summary": sm, "per_video": rows}
