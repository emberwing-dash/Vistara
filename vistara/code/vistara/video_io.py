"""Efficient sequential frame sampling + cheap egocentric motion cues (OpenCV)."""
from __future__ import annotations
import logging
from dataclasses import dataclass
from typing import Callable, Optional
import cv2
import numpy as np

log = logging.getLogger("vistara.video")


@dataclass
class VideoInfo:
    path: str
    fps: float
    n_frames: int
    duration: float
    width: int
    height: int


@dataclass
class Samples:
    times: np.ndarray          # (N,) seconds
    frames: np.ndarray         # (N,224,224,3) uint8 RGB, full frame squashed (keeps whole field of view)
    global_motion: np.ndarray  # (N,) mean optical-flow magnitude (ego-motion + scene motion), px/s @ flow width
    local_motion: np.ndarray   # (N,) residual flow after removing dominant camera motion
    hand_motion: np.ndarray    # (N,) residual flow in the lower half of the frame (where hands live in egocentric video)
    sharpness: np.ndarray      # (N,) variance of Laplacian


def probe(path: str) -> VideoInfo:
    cap = cv2.VideoCapture(path)
    if not cap.isOpened():
        raise IOError(f"cannot open video: {path}")
    fps = cap.get(cv2.CAP_PROP_FPS) or 0.0
    n = int(cap.get(cv2.CAP_PROP_FRAME_COUNT) or 0)
    w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH) or 0)
    h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT) or 0)
    cap.release()
    if fps <= 0 or n <= 0:
        raise IOError(f"video has no readable timing info (fps={fps}, frames={n}): {path}")
    return VideoInfo(path, float(fps), n, n / fps, w, h)


def _flow_features(a: np.ndarray, b: np.ndarray, dt: float):
    flow = cv2.calcOpticalFlowFarneback(a, b, None, 0.5, 2, 13, 2, 5, 1.1, 0)
    mag = np.hypot(flow[..., 0], flow[..., 1]) / max(dt, 1e-3)
    dom = np.median(flow.reshape(-1, 2), axis=0)                      # dominant (camera) motion
    resid = np.hypot(flow[..., 0] - dom[0], flow[..., 1] - dom[1]) / max(dt, 1e-3)
    h = resid.shape[0]
    return float(mag.mean()), float(resid.mean()), float(resid[h // 2:].mean())


def sample_video(path: str, info: VideoInfo, sample_fps: float = 2.0, motion_gap_s: float = 0.25,
                 flow_width: int = 160, progress: Optional[Callable[[float], None]] = None) -> Samples:
    cap = cv2.VideoCapture(path)
    if not cap.isOpened():
        raise IOError(f"cannot open video: {path}")
    n_samples = max(1, int(info.duration * sample_fps))
    gap_frames = max(1, int(round(motion_gap_s * info.fps)))
    dt = gap_frames / info.fps
    fh = int(round(flow_width * info.height / max(info.width, 1))) or flow_width

    times, frames, gm, lm, hm, sh = [], [], [], [], [], []
    pos = -1  # index of the last frame consumed
    for i in range(n_samples):
        t = i / sample_fps
        target = min(int(round(t * info.fps)), info.n_frames - 1)
        ok = True
        while pos < target - 1:
            if not cap.grab():
                ok = False
                break
            pos += 1
        if not ok:
            break
        ok, fa = cap.read()
        pos += 1
        if not ok or fa is None:
            break
        small_a = cv2.cvtColor(cv2.resize(fa, (flow_width, fh), interpolation=cv2.INTER_AREA), cv2.COLOR_BGR2GRAY)
        rgb = cv2.cvtColor(cv2.resize(fa, (224, 224), interpolation=cv2.INTER_AREA), cv2.COLOR_BGR2RGB)
        lap = float(cv2.Laplacian(small_a, cv2.CV_64F).var())
        g = l = h = 0.0
        for _ in range(gap_frames - 1):
            if not cap.grab():
                break
            pos += 1
        ok2, fb = cap.read()
        if ok2 and fb is not None:
            pos += 1
            small_b = cv2.cvtColor(cv2.resize(fb, (flow_width, fh), interpolation=cv2.INTER_AREA), cv2.COLOR_BGR2GRAY)
            g, l, h = _flow_features(small_a, small_b, dt)
        times.append(t); frames.append(rgb); gm.append(g); lm.append(l); hm.append(h); sh.append(lap)
        if progress and (i % 20 == 0):
            progress((i + 1) / n_samples)
    cap.release()
    if not frames:
        raise IOError("no frames could be decoded")
    if progress:
        progress(1.0)
    log.info("sampled %d frames from %s", len(frames), path)
    return Samples(np.array(times), np.stack(frames), np.array(gm), np.array(lm), np.array(hm), np.array(sh))
