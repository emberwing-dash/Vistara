"""One-off patch: expose importance components so feedback can re-weight them (see code/vistara/adapt.py)."""
from pathlib import Path

p = Path(__file__).resolve().parents[1] / "code" / "vistara" / "importance.py"
s = p.read_text(encoding="utf-8")
i = s.index("def compute_importance")
new = '''# component order: activity, change, distinctiveness, global motion, action confidence
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
'''
p.write_text(s[:i] + new, encoding="utf-8")

p = Path(__file__).resolve().parents[1] / "code" / "vistara" / "pipeline.py"
s = p.read_text(encoding="utf-8")
if '"internal"' not in s:
    s = s.replace('        return {"output": output, "analysis": analysis}',
                  '''        internal = {"times": s.times, "emb": emb, "comps": imp["comps"], "blur": imp["blur"], "idle": imp["idle"],
                    "weights": imp["weights"], "fps": imp["fps"], "pair_at": pair_at if wins else None,
                    "pair_conf": pair_conf if wins else None, "duration": info.duration, "domain": domain,
                    "curve_step": max(1, len(s.times) // 400)}
        return {"output": output, "analysis": analysis, "internal": internal}''')
    p.write_text(s, encoding="utf-8")
print("patched")
