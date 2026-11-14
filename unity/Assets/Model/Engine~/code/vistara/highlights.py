"""Budget-constrained, redundancy-aware highlight selection (original footage only)."""
from __future__ import annotations
import math
import numpy as np
from .config import Config


def budget_seconds(duration: float, cfg: Config) -> float:
    return math.floor(duration * cfg.budget_frac * cfg.budget_safety * 100) / 100.0


def select_highlights(times: np.ndarray, importance: np.ndarray, emb: np.ndarray, duration: float, cfg: Config,
                      pair_at: list | None = None, pair_conf: np.ndarray | None = None) -> list[dict]:
    """Greedy maximal-marginal-relevance selection on the sampling grid.

    score(window) = mean importance  (x mild length bonus)
                    - redundancy_weight * visual similarity to already selected footage
                    + pair_novelty_weight * (action pair not yet covered) * confidence
    Returns [{"segment":[s,e], "importance":float}] sorted chronologically, non-overlapping.
    """
    n = len(times)
    step = float(times[1] - times[0]) if n > 1 else 1.0
    budget = budget_seconds(duration, cfg)
    min_len = cfg.min_segment_s

    if duration <= min_len + 1e-6:
        return [{"segment": [0.0, round(duration, 2)], "importance": float(importance.mean())}]
    budget = max(budget, min_len)                       # always allow one valid >=4 s segment

    csum = np.concatenate([[0.0], np.cumsum(importance)])
    ecum = np.concatenate([np.zeros((1, emb.shape[1])), np.cumsum(emb, axis=0)])

    def win_stats(i: int, k: int):
        m = (csum[i + k] - csum[i]) / k
        v = (ecum[i + k] - ecum[i])
        v = v / (np.linalg.norm(v) + 1e-9)
        return m, v

    lens = sorted({max(min_len, L) for L in cfg.seg_len_choices})
    cands = []   # (start_idx, k_samples, length_s, mean_imp, emb)
    for L in lens:
        k = int(round(L / step))
        if k > n:
            continue
        for i in range(0, n - k + 1):
            m, v = win_stats(i, k)
            cands.append((i, k, k * step, m, v))
    if not cands:
        k = n
        m, v = win_stats(0, k)
        cands = [(0, k, k * step, m, v)]

    selected, sel_embs, covered_pairs = [], [], set()
    used = np.zeros(n, dtype=bool)
    total = 0.0
    margin = max(1, int(round(cfg.min_gap_s / step)))

    while True:
        best, best_score = None, -1e9
        remaining = budget - total
        for (i, k, L, m, v) in cands:
            if L > remaining + 1e-6:
                continue
            lo, hi = max(0, i - margin), min(n, i + k + margin)
            if used[lo:hi].any():
                continue
            score = m * (L ** 0.12)
            if sel_embs:
                sim = max(float(v @ e) for e in sel_embs)
                score -= cfg.redundancy_weight * float(np.clip((sim - 0.75) / 0.25, 0, 1))
            if pair_at is not None:
                c = i + k // 2
                p = pair_at[c] if c < len(pair_at) else None
                if p is not None and p not in covered_pairs:
                    score += cfg.pair_novelty_weight * float(pair_conf[c] if pair_conf is not None else 0.5)
            if score > best_score:
                best, best_score = (i, k, L, m, v), score
        if best is None:
            break
        i, k, L, m, v = best
        selected.append((i, k, L, m))
        sel_embs.append(v)
        used[i:i + k] = True
        total += L
        if pair_at is not None:
            for j in range(i, min(i + k, len(pair_at))):
                if pair_at[j] is not None:
                    covered_pairs.add(pair_at[j])
        if total >= budget - min_len + 1e-6 and remaining - L < min_len:
            break

    selected.sort(key=lambda x: x[0])
    segs = []
    for (i, k, L, m) in selected:
        s = float(times[i])
        e = min(s + L, duration)
        if e - s < min_len:                              # window hit the end of the video: shift left
            s = max(0.0, e - min_len)
        segs.append([s, e, float(m)])

    # merge touching segments (union length never exceeds the sum)
    merged = []
    for s, e, m in segs:
        if merged and s - merged[-1][1] <= 1e-6:
            ps, pe, pm = merged[-1]
            w1, w2 = pe - ps, e - s
            merged[-1] = [ps, e, (pm * w1 + m * w2) / (w1 + w2)]
        else:
            merged.append([s, e, m])

    # bridge tiny gaps when the budget allows (avoids choppy back-to-back cuts)
    used = sum(e - s for s, e, _ in merged)
    bridged = []
    for s, e, m in merged:
        if bridged and s - bridged[-1][1] <= cfg.merge_gap_s and used + (s - bridged[-1][1]) <= budget:
            ps, pe, pm = bridged[-1]
            used += s - pe
            w1, w2 = pe - ps, e - s
            bridged[-1] = [ps, e, (pm * w1 + m * w2) / (w1 + w2)]
        else:
            bridged.append([s, e, m])
    merged = bridged

    # use the leftover budget: grow segments towards whichever neighbouring second is most important (recall / F1)
    used = sum(e - s for s, e, _ in merged)
    guard = cfg.min_gap_s

    def at(t: float) -> float:
        return float(importance[int(np.clip(round(t / step), 0, n - 1))])

    while budget - used >= step - 1e-6:
        best = None
        for i, (s, e, m) in enumerate(merged):
            if e + step <= duration + 1e-6 and (i == len(merged) - 1 or e + step <= merged[i + 1][0] - guard + 1e-6):
                g = at(e)
                if best is None or g > best[0]:
                    best = (g, i, "r")
            if s - step >= -1e-6 and (i == 0 or s - step >= merged[i - 1][1] + guard - 1e-6):
                g = at(s - step)
                if best is None or g > best[0]:
                    best = (g, i, "l")
        if best is None:
            break
        _, i, side = best
        if side == "r":
            merged[i][1] += step
        else:
            merged[i][0] -= step
        used += step

    out = [{"segment": [round(s, 2), round(e, 2)], "importance": round(float(np.clip(m, 0, 1)), 4)} for s, e, m in merged]
    return out


def retained_fraction(highlights: list[dict], duration: float) -> float:
    return sum(h["segment"][1] - h["segment"][0] for h in highlights) / max(duration, 1e-9)
