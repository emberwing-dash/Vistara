"""Window-level features shared by training (extract/train) and inference (recognizer)."""
from __future__ import annotations
import numpy as np
from .importance import robust_norm

FEAT_DIM = 512 * 2 + 3


def motion_matrix(hand: np.ndarray, local: np.ndarray, glob: np.ndarray) -> np.ndarray:
    """(N,3) per-video robust-normalised motion cues."""
    return np.stack([robust_norm(hand), robust_norm(local), robust_norm(glob)], axis=1)


def window_feature(emb: np.ndarray, motion: np.ndarray, times: np.ndarray, t0: float, t1: float) -> np.ndarray | None:
    idx = np.where((times >= t0) & (times < t1))[0]
    if len(idx) == 0:
        return None
    e = emb[idx]
    mean = e.mean(0)
    mean /= (np.linalg.norm(mean) + 1e-9)
    diff = e[-1] - e[0]                                   # appearance change across the window (motion-ish cue)
    return np.concatenate([mean, diff, motion[idx].mean(0)]).astype(np.float32)
