"""Frame-level importance scoring: activity + change + distinctiveness (+ action confidence), idle penalty."""
from __future__ import annotations
import numpy as np
from .config import Config
from .video_io import Samples


def robust_norm(x: np.ndarray, lo: float = 5, hi: float = 95) -> np.ndarray:
    a, b = np.percentile(x, [lo, hi])
    if b - a < 1e-9:
        return np.zeros_like(x, dtype=float)
    return np.clip((x - a) / (b - a), 0, 1)


def gaussian_smooth(x: np.ndarray, sigma_samples: float) -> np.ndarray:
    if sigma_samples <= 0.3:
        return x
    r = max(1, int(3 * sigma_samples))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma_samples) ** 2)
    k /= k.sum()
    return np.convolve(np.pad(x, r, mode="edge"), k, mode="valid")


def activity_signal(s: Samples) -> np.ndarray:
    """Hand/object interaction proxy in [0,1]: residual (non-camera) motion, biased to the lower frame half."""
    return np.clip(0.6 * robust_norm(s.hand_motion) + 0.4 * robust_norm(s.local_motion), 0, 1)


def change_signal(emb: np.ndarray, lag: int) -> np.ndarray:
    """Cosine distance of each frame to the mean of the preceding `lag` frames (scene / activity change)."""
    n = len(emb)
    out = np.zeros(n)
    csum = np.cumsum(np.pad(emb, ((1, 0), (0, 0))), axis=0)
    for i in range(1, n):
        j = max(0, i - lag)
        m = (csum[i] - csum[j]) / (i - j)
        m /= (np.linalg.norm(m) + 1e-9)
        out[i] = 1.0 - float(emb[i] @ m)
    return robust_norm(out)


def action_conf_curve(times: np.ndarray, wins) -> np.ndarray:
    conf = np.zeros(len(times))
    for w in wins:
        m = (times >= w.start) & (times < w.end)
        conf[m] = np.maximum(conf[m], w.confidence)
    return robust_norm(conf, 0, 99) if conf.max() > 0 else conf


# component order: activity, change, distinctiveness, global motion, action confidence
BASE_WEIGHTS = {"cooking": [0.40, 0.15, 0.15, 0.00, 0.30], "sport": [0.20, 0.25, 0.15, 0.40, 0.00]}


def importance_from(comps: np.ndarray, w: np.ndarray, blur: np.ndarray, idle: np.ndarray, fps: float, sigma_s: float) -> np.ndarray:
    imp = (comps @ np.asarray(w)) * blur * idle
    return robust_norm(gaussian_smooth(imp, sigma_s * fps), 2, 98)


def compute_importance(s: Samples, emb: np.ndarray, action_conf: np.ndarray, domain: str, cfg: Config) -> dict:
    fps = 1.0 / float(s.times[1] - s.times[0]) if len(s.times) > 1 else cfg.sample_fps
    act = activity_signal(s)
    glob = robust_norm(s.global_motion)
    change = change_signal(emb, lag=max(1, int(8 * fps)))
    mean = emb.mean(0)
    mean /= np.linalg.norm(mean)
    distinct = robust_norm(1.0 - emb @ mean)
    blur = 0.5 + 0.5 * robust_norm(s.sharpness, 2, 60)       # mild penalty for blurry frames
    comps = np.stack([act, change, distinct, glob, action_conf], axis=1)
    # kitchen footage: idle (no hand/object activity) is down-weighted; sports footage: speed matters instead
    idle = (0.30 + 0.70 * np.clip(act / 0.30, 0, 1)) if domain == "cooking" else np.ones_like(act)
    w = np.array(BASE_WEIGHTS["cooking" if domain == "cooking" else "sport"])
    imp = importance_from(comps, w, blur, idle, fps, cfg.smooth_sigma_s)
    return {"importance": imp, "activity": act, "change": change, "distinct": distinct, "global": glob,
            "comps": comps, "blur": blur, "idle": idle, "weights": w, "fps": fps}
