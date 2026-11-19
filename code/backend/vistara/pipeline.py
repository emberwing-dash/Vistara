"""End-to-end analysis pipeline. Independent of Unity: used by run.sh (CLI) and by the HTTP server."""
from __future__ import annotations
import logging
import hashlib
import os
import time
from collections import defaultdict
from pathlib import Path
from typing import Callable, Optional
import numpy as np

from . import __version__
from .config import Config, DEFAULT, for_domain, WEIGHTS_DIR

from .highlights import select_highlights, retained_fraction, budget_seconds
from .importance import compute_importance, action_conf_curve, activity_signal
from .model import ClipEncoder
from .features import motion_matrix
from .recognizer import Recognizer
from .schema import validate, hl_item
from .video_io import probe, sample_video

log = logging.getLogger("vistara.pipeline")


def feature_cache_file(video_path: str, cfg: Config):
    """Dev-only cache location of the decoded features for (video, backbone, sampling); None when VISTARA_FEATURE_CACHE is unset."""
    d = os.environ.get("VISTARA_FEATURE_CACHE")
    if not d:
        return None
    stem = Path(video_path).stem
    hits = sorted(Path(d).glob(f"{stem}_*.npz")) if Path(d).exists() else []
    if hits:                                           # an existing cache entry wins (the video file itself may already be deleted)
        return hits[0]
    key = hashlib.sha1(f"{stem}|{cfg.clip_model}|{cfg.clip_pretrained}|{cfg.sample_fps}|{cfg.flow_width}".encode()).hexdigest()[:12]
    return Path(d) / f"{stem}_{key}.npz"
Progress = Optional[Callable[[str, float, str], None]]


class VistaraPipeline:
    def __init__(self, cfg: Config = DEFAULT):
        self.cfg = cfg
        self.enc = ClipEncoder(cfg)
        self.rec = Recognizer(self.enc, cfg)
        self.avion = None
        ap = WEIGHTS_DIR / "avion_vitl_ek100_fp16.pt"
        if ap.exists() and getattr(cfg, "use_avion", True):
            try:
                from .avion import AvionRecognizer
                self.avion = AvionRecognizer(ap, self.enc.device)
                log.info("AVION action recogniser loaded")
            except Exception as e:  # noqa: BLE001
                log.warning("could not load AVION (%s); using the CLIP action head", e)

    def _cache_file(self, video_path: str):
        return feature_cache_file(video_path, self.cfg)

    # ------------------------------------------------------------------
    def analyse(self, video_path: str, video_id: str | None = None, progress: Progress = None) -> dict:
        """Returns {"output": <official JSON dict>, "analysis": <extra data for the Unity UI>}."""
        cfg = self.cfg
        cb = progress or (lambda stage, frac, msg: None)
        t_start = time.time()
        vid = video_id or Path(video_path).stem
        timings = {}

        cb("probe", 0.0, "Reading video metadata")
        cache = self._cache_file(video_path)               # optional dev cache of decoded features (VISTARA_FEATURE_CACHE); never used by run.sh
        zc = np.load(cache) if (cache is not None and cache.exists()) else None
        if zc is not None and "duration" in zc.files:      # features were extracted earlier: no need for the video file any more
            from .video_io import VideoInfo
            info = VideoInfo(path=video_path, fps=float(zc["fps"]), n_frames=int(zc["n_frames"]), duration=float(zc["duration"]), width=int(zc["width"]), height=int(zc["height"]))
        else:
            info = probe(video_path)
        dur = round(info.duration, 2)

        if zc is not None:
            z = zc
            from .video_io import Samples
            s = Samples(times=z["times"], frames=None, global_motion=z["glob"], local_motion=z["local"], hand_motion=z["hand"], sharpness=z["sharp"])
            emb = z["emb"].astype(np.float32); timings["decode_s"] = timings["embed_s"] = 0.0
        else:
            t = time.time()
            cb("decode", 0.0, "Sampling frames and measuring motion")
            s = sample_video(video_path, info, cfg.sample_fps, cfg.motion_gap_s, cfg.flow_width,
                             progress=lambda f: cb("decode", f, "Sampling frames and measuring motion"))
            timings["decode_s"] = round(time.time() - t, 2)

            t = time.time()
            cb("embed", 0.0, "Encoding frames with CLIP")
            emb = self.enc.encode_images(s.frames, progress=lambda f: cb("embed", f, "Encoding frames with CLIP"))
            timings["embed_s"] = round(time.time() - t, 2)
            del s.frames  # free RAM
            if cache is not None:
                cache.parent.mkdir(parents=True, exist_ok=True)
                np.savez_compressed(cache, times=s.times, glob=s.global_motion, local=s.local_motion, hand=s.hand_motion, sharp=s.sharpness, emb=emb.astype(np.float16),
                                    duration=info.duration, fps=info.fps, n_frames=info.n_frames, width=info.width, height=info.height)

        t = time.time()
        cb("recognise", 0.0, "Classifying video and recognising actions")
        label, lscore, probs = self.rec.classify_video(emb)
        p_cook = probs.get("cooking", 0.0)
        if p_cook >= 0.35:                       # EPIC videos are all kitchen footage; FPVSum has no cooking class
            label, lscore = "cooking", max(lscore, p_cook) if label == "cooking" else p_cook
        # Actions are only scored on EPIC (kitchen) videos and are ignored for FPVSum, so recognise actions whenever
        # kitchen-ness is plausible (guards against a CLIP mis-label silently zeroing Action mAP); importance weights
        # switch to the kitchen profile at a slightly lower bar than the label itself.
        domain = "cooking" if (label == "cooking" or p_cook >= 0.20) else "sport"
        do_actions = label == "cooking" or p_cook >= 0.10
        act = activity_signal(s)

        wins, actions = [], []
        if do_actions:
            if self.avion is not None and Path(video_path).exists():
                wins = self.rec.recognise_avion(self.avion, video_path, s.times, act, info.duration,
                                                progress=lambda f: cb("recognise", 0.1 + 0.85 * f, "Recognising actions with AVION"))
            else:
                wins = self.rec.recognise(s.times, emb, act, info.duration,
                                          motion_matrix(s.hand_motion, s.local_motion, s.global_motion))
            actions = self.rec.merge(wins, s.times, act, info.duration)
        cb("recognise", 1.0, f"{len(actions)} actions localised")
        timings["recognise_s"] = round(time.time() - t, 2)

        t = time.time()
        cb("importance", 0.0, "Scoring segment importance")
        conf = action_conf_curve(s.times, wins) if wins else np.zeros(len(s.times))
        imp = compute_importance(s, emb, conf, domain, cfg)

        pair_at = [None] * len(s.times)
        pair_conf = np.zeros(len(s.times))
        for w in wins:
            m = np.where((s.times >= w.start) & (s.times < w.end))[0]
            for j in m:
                if w.confidence >= pair_conf[j]:
                    pair_at[j] = (w.pairs[0][0], w.pairs[0][1])
                    pair_conf[j] = w.confidence
        cb("importance", 1.0, "Importance curve ready")

        cb("highlights", 0.0, "Selecting highlight segments")
        sel_imp = imp["importance"]
        if domain == "cooking" and cfg.cook_importance_mix < 1.0:
            sel_imp = cfg.cook_importance_mix * sel_imp + (1.0 - cfg.cook_importance_mix) * float(sel_imp.mean())
        hl = select_highlights(s.times, sel_imp, emb, info.duration, for_domain(cfg, domain),
                               pair_at if wins else None, pair_conf if wins else None)
        timings["select_s"] = round(time.time() - t, 2)

        output = {
            "team_id": cfg.team_id,
            "video_id": vid,
            "duration_sec": dur,
            "label": {"name": label, "score": round(float(np.clip(lscore, 0, 1)), 4)},
            "actions": actions,
            "highlights": [hl_item(h) for h in hl],
        }
        errs, warns = validate(output)
        if dur < cfg.min_segment_s:
            errs = [e for e in errs if "no highlight segment >= 4.0 s" not in e]
        if errs:
            raise ValueError("generated output failed schema validation: " + "; ".join(errs))
        for w in warns:
            log.warning("schema warning: %s", w)

        analysis = self._analysis(info, s, imp, hl, actions, probs, domain, timings, dur, time.time() - t_start)
        cb("done", 1.0, "Analysis complete")
        internal = {"times": s.times, "emb": emb, "comps": imp["comps"], "blur": imp["blur"], "idle": imp["idle"],
                    "weights": imp["weights"], "fps": imp["fps"], "pair_at": pair_at if wins else None,
                    "pair_conf": pair_conf if wins else None, "duration": info.duration, "domain": domain,
                    "curve_step": max(1, len(s.times) // 400)}
        return {"output": output, "analysis": analysis, "internal": internal}

    # ------------------------------------------------------------------
    def _analysis(self, info, s, imp, hl, actions, probs, domain, timings, dur, total_s) -> dict:
        step = max(1, len(s.times) // 400)               # <= ~400 curve points for the UI
        ix = range(0, len(s.times), step)
        curve = {"t": [round(float(s.times[i]), 2) for i in ix],
                 "importance": [round(float(imp["importance"][i]), 3) for i in ix],
                 "activity": [round(float(imp["activity"][i]), 3) for i in ix]}
        objs = defaultdict(lambda: {"count": 0, "seconds": 0.0, "score": 0.0})
        for a in actions:
            o = objs[a["noun"]]
            o["count"] += 1
            o["seconds"] += a["segment"][1] - a["segment"][0]
            o["score"] = max(o["score"], a["score"])
        objects = sorted(({"noun": k, **{kk: round(vv, 3) for kk, vv in v.items()}} for k, v in objs.items()),
                         key=lambda x: -x["seconds"])[:12]
        verbs = defaultdict(float)
        for a in actions:
            verbs[a["verb"]] += a["segment"][1] - a["segment"][0]
        top_verbs = sorted(({"verb": k, "seconds": round(v, 2)} for k, v in verbs.items()), key=lambda x: -x["seconds"])[:12]
        return {
            "engine_version": __version__,
            "domain": domain,
            "video": {"fps": round(info.fps, 3), "width": info.width, "height": info.height, "duration": dur},
            "label_probs": {k: round(v, 4) for k, v in sorted(probs.items(), key=lambda kv: -kv[1])[:5]},
            "curve": curve,
            "segments": [{"start": h["segment"][0], "end": h["segment"][1], "importance": h["importance"]} for h in hl],
            "retained_fraction": round(retained_fraction(hl, dur), 4),
            "budget_seconds": budget_seconds(dur, self.cfg),
            "objects": objects,
            "verbs": top_verbs,
            "timings": {**timings, "total_s": round(total_s, 2)},
            "device": self.enc.device,
            "model": f"{self.cfg.clip_model}/{self.cfg.clip_pretrained}",
        }
