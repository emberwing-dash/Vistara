"""Output-schema validation mirroring the official student-kit rules (TEAM_README.md section 3/4).

The organisers' validate.py was listed in the kit but not present in the provided package, so these
checks are re-implemented from the documented constraints.
"""
from __future__ import annotations
import math

from . import vocab


def _num(x) -> bool:
    return isinstance(x, (int, float)) and not isinstance(x, bool) and math.isfinite(x)


def validate(d: dict, manifest_duration: float | None = None) -> tuple[list[str], list[str]]:
    errors, warns = [], []
    for k in ("team_id", "video_id", "duration_sec", "label", "actions", "highlights"):
        if k not in d:
            errors.append(f"missing key: {k}")
    if errors:
        return errors, warns
    if not isinstance(d["team_id"], str) or not d["team_id"]:
        errors.append("team_id must be a non-empty string")
    if not isinstance(d["video_id"], str) or not d["video_id"]:
        errors.append("video_id must be a non-empty string")
    dur = d["duration_sec"]
    if not _num(dur) or dur <= 0:
        errors.append("duration_sec must be a positive number")
        return errors, warns
    if manifest_duration is not None and abs(dur - manifest_duration) > 0.5:
        errors.append(f"duration_sec {dur} differs from manifest {manifest_duration} by > 0.5 s")
    lab = d["label"]
    if not isinstance(lab, dict) or not isinstance(lab.get("name"), str) or not _num(lab.get("score", None)) \
            or not 0 <= lab["score"] <= 1:
        errors.append("label must be {name:str, score:[0,1]}")
    elif lab["name"] not in vocab.labels():
        warns.append(f"label '{lab['name']}' not in supported labels")
    if not isinstance(d["actions"], list) or len(d["actions"]) > 2000:
        errors.append("actions must be a list with <= 2000 items")
    else:
        V, N = set(vocab.load_classes()["verbs"]), set(vocab.load_classes()["nouns"])
        for i, a in enumerate(d["actions"]):
            seg = a.get("segment") if isinstance(a, dict) else None
            if not (isinstance(seg, list) and len(seg) == 2 and all(_num(x) for x in seg)):
                errors.append(f"actions[{i}].segment invalid"); continue
            if not (0 <= seg[0] < seg[1] <= dur + 0.5):
                errors.append(f"actions[{i}] segment out of range {seg}")
            if not isinstance(a.get("verb"), str) or not isinstance(a.get("noun"), str):
                errors.append(f"actions[{i}] verb/noun must be strings")
            elif a["verb"] not in V or a["noun"] not in N:
                warns.append(f"actions[{i}] verb/noun outside supported vocabulary")
            if not _num(a.get("score")) or not 0 <= a["score"] <= 1:
                errors.append(f"actions[{i}].score must be in [0,1]")
    hl = d["highlights"]
    if not isinstance(hl, list) or len(hl) < 1:
        errors.append("highlights must contain at least 1 item")
    else:
        prev_end, total, any_long = -1.0, 0.0, False
        for i, h in enumerate(hl):
            seg = h.get("segment") if isinstance(h, dict) else None
            if not (isinstance(seg, list) and len(seg) == 2 and all(_num(x) for x in seg)):
                errors.append(f"highlights[{i}].segment invalid"); continue
            s, e = seg
            if not (0 <= s < e <= dur + 0.5):
                errors.append(f"highlights[{i}] out of range {seg}")
            if s < prev_end:
                errors.append(f"highlights[{i}] overlaps or is out of order")
            prev_end = e
            total += e - s
            any_long |= (e - s) >= 4.0
            if e - s < 4.0:
                warns.append(f"highlights[{i}] shorter than 4.0 s (ignored in scoring)")
        if not any_long:
            errors.append("no highlight segment >= 4.0 s")
        if total > 0.15 * dur + 1e-6:
            warns.append(f"total highlight {total:.2f}s exceeds 15% budget ({0.15 * dur:.2f}s)")
    return errors, warns
