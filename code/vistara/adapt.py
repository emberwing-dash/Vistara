"""Live, per-video adaptation from user feedback ("more like this" / "drop this").

No labels and no retraining of CLIP: each vote
  1. nudges the weights of the importance components (activity, change, distinctiveness, motion, action confidence)
     towards what the liked segment has more of (and away from it for dislikes),
  2. updates a preference vector in CLIP space so visually similar footage is promoted / demoted,
  3. pins liked ranges in and disliked ranges out.
Then highlights are re-selected within the same 15 % budget.
"""
from __future__ import annotations
import numpy as np
from .config import Config
from .highlights import budget_seconds, retained_fraction, select_highlights
from .importance import importance_from, robust_norm


class FeedbackSession:
    def __init__(self, internal: dict, cfg: Config):
        self.d, self.cfg = internal, cfg
        self.times = internal["times"]
        self.step = float(self.times[1] - self.times[0]) if len(self.times) > 1 else 1.0
        self.w = np.array(internal["weights"], dtype=float)
        self.w0 = self.w.copy()
        self.pref = np.zeros(internal["emb"].shape[1])
        self.liked: list[tuple[int, int]] = []
        self.disliked: list[tuple[int, int]] = []
        self.n_votes = 0

    def _range(self, start: float, end: float) -> tuple[int, int]:
        n = len(self.times)
        i0 = int(np.clip(round(start / self.step), 0, n - 1))
        i1 = int(np.clip(round(end / self.step), i0 + 1, n))
        return i0, i1

    def vote(self, start: float, end: float, v: int) -> None:
        v = 1 if v > 0 else -1
        i0, i1 = self._range(start, end)
        comps = self.d["comps"]
        diff = comps[i0:i1].mean(0) - comps.mean(0)
        self.w = np.clip(self.w + 0.5 * v * diff, 0.02, None)
        self.w /= self.w.sum()
        e = self.d["emb"][i0:i1].mean(0)
        self.pref += v * e / (np.linalg.norm(e) + 1e-9)
        (self.liked if v > 0 else self.disliked).append((i0, i1))
        self.n_votes += 1

    def importance(self) -> np.ndarray:
        d = self.d
        imp = importance_from(d["comps"], self.w, d["blur"], d["idle"], d["fps"], self.cfg.smooth_sigma_s)
        if np.linalg.norm(self.pref) > 1e-6:
            p = self.pref / np.linalg.norm(self.pref)
            imp = np.clip(imp + 0.35 * (robust_norm(d["emb"] @ p) - 0.5), 0, 1.2)
        for i0, i1 in self.liked:
            imp[i0:i1] = np.maximum(imp[i0:i1], 0.95)
        margin = max(1, int(round(1.0 / self.step)))
        for i0, i1 in self.disliked:
            imp[max(0, i0 - margin):i1 + margin] = 0.0
        return imp

    def reselect(self) -> tuple[list[dict], np.ndarray]:
        d = self.d
        imp = self.importance()
        hl = select_highlights(self.times, imp, d["emb"], d["duration"], self.cfg, d["pair_at"], d["pair_conf"])
        return hl, imp

    def apply_to(self, output: dict, analysis: dict, hl: list[dict], imp: np.ndarray) -> None:
        """Mutate the official output + UI analysis dicts in place."""
        output["highlights"] = [{"segment": h["segment"]} for h in hl]
        dur = output["duration_sec"]
        ix = range(0, len(self.times), self.d["curve_step"])
        analysis["curve"]["importance"] = [round(float(imp[i]), 3) for i in ix]
        analysis["segments"] = [{"start": h["segment"][0], "end": h["segment"][1], "importance": h["importance"]} for h in hl]
        analysis["retained_fraction"] = round(retained_fraction(hl, dur), 4)
        analysis["budget_seconds"] = budget_seconds(dur, self.cfg)
        analysis["feedback_votes"] = self.n_votes
