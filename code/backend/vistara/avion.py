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
    def read_clip(cap, fps: float, t0: float, t1: float):
        import cv2
        idx = np.linspace(t0 * fps, max(t0 * fps, t1 * fps - 1), FRAMES).astype(int); out = []
        for i in idx:
            cap.set(cv2.CAP_PROP_POS_FRAMES, int(i)); ok, fr = cap.read()
            if not ok:
                fr = out[-1] if out else np.zeros((SIZE, SIZE, 3), np.uint8)
            else:
                fr = cv2.cvtColor(fr, cv2.COLOR_BGR2RGB); h, w = fr.shape[:2]; s = SIZE / min(h, w)
                fr = cv2.resize(fr, (max(SIZE, round(w * s)), max(SIZE, round(h * s))), interpolation=cv2.INTER_AREA)
                h, w = fr.shape[:2]; y, x = (h - SIZE) // 2, (w - SIZE) // 2; fr = fr[y:y + SIZE, x:x + SIZE]
            out.append(fr)
        a = (np.stack(out).astype(np.float32) - MEAN) / STD
        return a.transpose(3, 0, 1, 2)                                       # C,T,H,W

    @property
    def n_actions(self) -> int:
        return self.fc.out_features

    def predict_windows(self, video_path: str, windows: list[tuple[float, float]], batch: int = 6, progress=None) -> np.ndarray:
        """-> (n_windows, n_actions) probabilities."""
        import cv2
        torch = self.torch
        cap = cv2.VideoCapture(video_path); fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
        out = np.zeros((len(windows), self.n_actions), np.float32)
        for b0 in range(0, len(windows), batch):
            clips = [self.read_clip(cap, fps, t0, t1) for t0, t1 in windows[b0:b0 + batch]]
            x = torch.from_numpy(np.stack(clips)).to(self.device).to(self.dt)
            with torch.no_grad():
                emb = self.visual(x)
                if isinstance(emb, list):
                    emb = emb[0]
                out[b0:b0 + len(clips)] = torch.softmax(self.fc(emb).float(), -1).cpu().numpy()
            if progress:
                progress(min(1.0, (b0 + batch) / len(windows)))
        cap.release()
        return out

    def marginals(self, p: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
        v = np.bincount(self.verb_of, weights=p, minlength=97); n = np.bincount(self.noun_of, weights=p, minlength=300)
        return v, n

