"""Local approximations of the competition metrics (the official scorer is not public; definitions follow TEAM_README.md).

* action_map  : mean over GT (verb,noun) classes of AP, averaged over tIoU thresholds 0.1..0.5
* coverage    : fraction of distinct GT (verb,noun) pairs overlapped by the selected highlights,
                with a soft multiplicative penalty when highlights exceed the 15 % budget
"""
from __future__ import annotations
from collections import defaultdict
import numpy as np


def tiou(a, b) -> float:
    inter = max(0.0, min(a[1], b[1]) - max(a[0], b[0]))
    union = (a[1] - a[0]) + (b[1] - b[0]) - inter
    return inter / union if union > 0 else 0.0


def _ap(preds, gts, thr):
    """preds: list of (score, video, seg); gts: dict video -> list of seg. Standard greedy matching AP."""
    n_gt = sum(len(v) for v in gts.values())
    if n_gt == 0:
        return None
    preds = sorted(preds, key=lambda p: -p[0])
    used = {v: [False] * len(s) for v, s in gts.items()}
    tp = np.zeros(len(preds)); fp = np.zeros(len(preds))
    for i, (_, vid, seg) in enumerate(preds):
        best, bj = 0.0, -1
        for j, g in enumerate(gts.get(vid, [])):
            if used[vid][j]:
                continue
            u = tiou(seg, g)
            if u > best:
                best, bj = u, j
        if best >= thr:
            used[vid][bj] = True; tp[i] = 1
        else:
            fp[i] = 1
    tp, fp = np.cumsum(tp), np.cumsum(fp)
    rec = tp / n_gt
    prec = tp / np.maximum(tp + fp, 1e-9)
    mrec = np.concatenate([[0], rec, [1]]); mpre = np.concatenate([[0], prec, [0]])
    for i in range(len(mpre) - 2, -1, -1):
        mpre[i] = max(mpre[i], mpre[i + 1])
    idx = np.where(mrec[1:] != mrec[:-1])[0]
    return float(np.sum((mrec[idx + 1] - mrec[idx]) * mpre[idx + 1]))


def action_map(pred: dict, gt: dict, thresholds=(0.1, 0.2, 0.3, 0.4, 0.5)) -> float:
    """pred/gt: {video_id: [{"verb","noun","segment":[s,e],("score")}]}"""
    classes = defaultdict(lambda: ([], defaultdict(list)))
    for vid, acts in gt.items():
        for a in acts:
            classes[(a["verb"], a["noun"])][1][vid].append(a["segment"])
    preds_by = defaultdict(list)
    for vid, acts in pred.items():
        for a in acts:
            preds_by[(a["verb"], a["noun"])].append((a.get("score", 1.0), vid, a["segment"]))
    aps = []
    for c, (_, gts) in classes.items():
        per_thr = [x for t in thresholds if (x := _ap(preds_by.get(c, []), gts, t)) is not None]
        aps.append(np.mean(per_thr) if per_thr else 0.0)
    return float(np.mean(aps)) if aps else 0.0


def coverage(highlights: dict, gt: dict, durations: dict, budget: float = 0.15) -> float:
    scores = []
    for vid, acts in gt.items():
        pairs = {(a["verb"], a["noun"]) for a in acts}
        if not pairs:
            continue
        hl = highlights.get(vid, [])
        got = {(a["verb"], a["noun"]) for a in acts
               if any(min(a["segment"][1], h[1]) - max(a["segment"][0], h[0]) > 0 for h in hl)}
        kept = sum(h[1] - h[0] for h in hl)
        pen = 1.0 if kept <= budget * durations[vid] + 1e-6 else max(0.0, 1 - (kept / (budget * durations[vid]) - 1))
        scores.append(len(got) / len(pairs) * pen)
    return float(np.mean(scores)) if scores else 0.0
