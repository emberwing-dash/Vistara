"""AVION action recogniser (ViT-L/14, fine-tuned on EPIC-KITCHENS-100 *train* split; public weights from github.com/zhaoyue-zephyrus/avion).

Reads 16 frames per window, returns a distribution over EPIC-100 (verb, noun) actions: held-out validation top-1 of 72% verb / 64.5% noun / 52% action
(measured here on 200 validation segments never used for training). Used for the actions; CLIP still does video label, importance and the fallback.
Weights: weights/avion_vitl_ek100_fp16.pt (state dict converted once from the public checkpoint by training/export_avion.py).
"""
from __future__ import annotations
import csv, logging, os
from pathlib import Path
import numpy as np

log = logging.getLogger("vistara.avion")
MEAN = np.array([108.3272985, 116.7460125, 104.09373615000001], np.float32)
STD = np.array([68.5005327, 66.6321579, 70.32316305], np.float32)
FRAMES, SIZE = 16, 224


class AvionRecognizer:
    def __init__(self, weights: Path, device: str):
        import torch
        from ._avion_vit import VisionTransformer
        self.torch, self.device = torch, device
        sd = torch.load(weights, map_location="cpu")
        vis = VisionTransformer(224, 14, 1024, 24, 16, 4, output_dim=sd["visual.image_projection"].shape[-1] if "visual.image_projection" in sd else 768,
                                patch_dropout=0., drop_path_rate=0., num_frames=FRAMES, use_fast_conv1=True, use_flash_attn=False)
        if hasattr(vis, "image_projection"):
            vis.image_projection = None
        self.visual = vis
        self.fc = torch.nn.Linear(vis.width, sd["fc_cls.weight"].shape[0])
        rest = {k[len("visual."):]: v.float() for k, v in sd.items() if k.startswith("visual.")}
        res = vis.load_state_dict(rest, strict=False)
        if res.missing_keys:
            raise RuntimeError("AVION weights incomplete: %s" % res.missing_keys[:3])
        self.fc.load_state_dict({"weight": sd["fc_cls.weight"].float(), "bias": sd["fc_cls.bias"].float()})
        dt = torch.float16 if device.startswith("cuda") else torch.float32
        self.dt = dt
        self.visual.to(device).to(dt).eval(); self.fc.to(device).to(dt).eval()
        ls = sd["label_space"]
        self.verb_of = np.array([v for v, _ in ls]); self.noun_of = np.array([n for _, n in ls])

    # ---------------------------------------------------------------- frames
    @staticmethod
    def _prep(fr):
        import cv2
        fr = cv2.cvtColor(fr, cv2.COLOR_BGR2RGB); h, w = fr.shape[:2]; s = SIZE / min(h, w)
        fr = cv2.resize(fr, (max(SIZE, round(w * s)), max(SIZE, round(h * s))), interpolation=cv2.INTER_AREA)
        h, w = fr.shape[:2]; y, x = (h - SIZE) // 2, (w - SIZE) // 2
        return fr[y:y + SIZE, x:x + SIZE]

    @classmethod
    def read_clip(cls, cap, fps: float, t0: float, t1: float):
        """One clip by random access (used by the training / evaluation scripts, which sample a few segments per video)."""
        idx = np.linspace(t0 * fps, max(t0 * fps, t1 * fps - 1), FRAMES).astype(int); out = []
        for i in idx:
            cap.set(1, int(i)); ok, fr = cap.read()
            out.append(cls._prep(fr) if ok else (out[-1] if out else np.zeros((SIZE, SIZE, 3), np.uint8)))
        a = (np.stack(out).astype(np.float32) - MEAN) / STD
        return a.transpose(3, 0, 1, 2)

    @classmethod
    def clips_sequential(cls, cap, fps: float, windows):
        """Yields one (C,T,H,W) clip per window, reading the video FORWARD once (grab() skips decoding cost of the colour conversion;
        random seeks re-decode from the previous keyframe for every frame, which is ~10x slower on long 1080p footage)."""
        cur, last = -1, None
        for t0, t1 in windows:
            idx = np.linspace(t0 * fps, max(t0 * fps, t1 * fps - 1), FRAMES).astype(int); out = []
            for i in idx:
                i = int(i)
                if i == cur and last is not None:
                    out.append(last); continue
                if i < cur or i - cur > 400:                          # backwards or a long jump (idle gap): one seek is cheaper than decoding through
                    cap.set(1, i); cur = i - 1                         # cv2.CAP_PROP_POS_FRAMES == 1
                while cur + 1 < i:
                    if not cap.grab():
                        break
                    cur += 1
                ok = cap.grab()
                if ok:
                    cur = i; ok, fr = cap.retrieve()
                if ok:
                    last = cls._prep(fr)
                elif last is None:
                    last = np.zeros((SIZE, SIZE, 3), np.uint8)
                out.append(last)
            a = (np.stack(out).astype(np.float32) - MEAN) / STD
            yield a.transpose(3, 0, 1, 2)                              # C,T,H,W

    @property
    def n_actions(self) -> int:
        return self.fc.out_features

    def predict_windows(self, video_path: str, windows: list[tuple[float, float]], batch: int = 6, progress=None) -> np.ndarray:
        """-> (n_windows, n_actions) probabilities."""
        import cv2, hashlib
        torch = self.torch
        cache_dir = os.environ.get("VISTARA_AVION_CACHE")          # optional: window probabilities are stored per video, so post-processing can be tuned without re-running the network
        cfile = None
        if cache_dir:
            try:
                st = os.stat(video_path); key = hashlib.sha1((f"{video_path}|{st.st_size}|{st.st_mtime_ns}|" + ",".join(f"{a:.2f}-{b:.2f}" for a, b in windows)).encode()).hexdigest()[:20]
                cfile = Path(cache_dir) / f"{Path(video_path).stem}_{key}.npy"
                if cfile.exists():
                    return np.load(cfile).astype(np.float32)
            except Exception:
                cfile = None
        cap = cv2.VideoCapture(video_path); fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
        out = np.zeros((len(windows), self.n_actions), np.float32)
        import queue, threading
        q: "queue.Queue" = queue.Queue(maxsize=3 * batch)          # a reader thread decodes the next windows while the GPU works on the current batch
        def _produce():
            try:
                for c in self.clips_sequential(cap, fps, windows):
                    q.put(c)
            finally:
                q.put(None)
        threading.Thread(target=_produce, daemon=True).start(); done = 0
        while done < len(windows):
            clips = []
            for _ in range(batch):
                c = q.get()
                if c is None:
                    break
                clips.append(c)
            if not clips:
                break
            x = torch.from_numpy(np.stack(clips)).to(self.device).to(self.dt)
            with torch.no_grad():
                emb = self.visual(x)
                if isinstance(emb, list):
                    emb = emb[0]
                out[done:done + len(clips)] = torch.softmax(self.fc(emb).float(), -1).cpu().numpy()
            done += len(clips)
            if progress:
                progress(min(1.0, done / len(windows)))
        cap.release()
        if cfile is not None:
            try:
                cfile.parent.mkdir(parents=True, exist_ok=True); np.save(cfile, out.astype(np.float16))
            except Exception:
                pass
        return out

    def marginals(self, p: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
        v = np.bincount(self.verb_of, weights=p, minlength=97); n = np.bincount(self.noun_of, weights=p, minlength=300)
        return v, n

