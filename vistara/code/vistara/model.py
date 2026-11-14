"""Pretrained CLIP wrapper (open_clip). Weights are cached inside ./weights for offline reproduction."""
from __future__ import annotations
import logging
import os
import numpy as np
from .config import CLIP_DIR, Config, WEIGHTS_DIR

# If the CLIP weights are bundled in ./weights, never touch the network (organisers re-run in an isolated environment).
if (CLIP_DIR / "models--timm--vit_base_patch32_clip_224.openai").exists():
    os.environ.setdefault("HF_HUB_OFFLINE", "1")
os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")
import torch  # noqa: E402
import open_clip  # noqa: E402

log = logging.getLogger("vistara.model")
_MEAN = torch.tensor([0.48145466, 0.4578275, 0.40821073]).view(1, 3, 1, 1)
_STD = torch.tensor([0.26862954, 0.26130258, 0.27577711]).view(1, 3, 1, 1)


class ClipEncoder:
    def __init__(self, cfg: Config, device: str | None = None):
        self.cfg = cfg
        self.device = device or ("cuda" if torch.cuda.is_available() else "cpu")
        self.dtype = torch.float16 if self.device == "cuda" else torch.float32
        CLIP_DIR.mkdir(parents=True, exist_ok=True)
        log.info("loading %s/%s on %s", cfg.clip_model, cfg.clip_pretrained, self.device)
        model, _, _ = open_clip.create_model_and_transforms(
            cfg.clip_model, pretrained=cfg.clip_pretrained, cache_dir=str(CLIP_DIR))
        self.model = model.to(self.device, dtype=self.dtype).eval()
        self.tokenizer = open_clip.get_tokenizer(cfg.clip_model)
        self.mean, self.std = _MEAN.to(self.device, self.dtype), _STD.to(self.device, self.dtype)

    @torch.inference_mode()
    def encode_images(self, frames: np.ndarray, progress=None) -> np.ndarray:
        """frames: (N,224,224,3) uint8 RGB -> (N,D) L2-normalised float32."""
        out = []
        bs = self.cfg.clip_batch
        for i in range(0, len(frames), bs):
            x = torch.from_numpy(frames[i:i + bs]).to(self.device).permute(0, 3, 1, 2).to(self.dtype) / 255.0
            x = (x - self.mean) / self.std
            f = self.model.encode_image(x).float()
            out.append(torch.nn.functional.normalize(f, dim=-1).cpu().numpy())
            if progress:
                progress(min(1.0, (i + bs) / len(frames)))
        return np.concatenate(out, 0)

    @torch.inference_mode()
    def encode_texts(self, texts: list[str]) -> np.ndarray:
        out = []
        for i in range(0, len(texts), 256):
            tok = self.tokenizer(texts[i:i + 256]).to(self.device)
            f = self.model.encode_text(tok).float()
            out.append(torch.nn.functional.normalize(f, dim=-1).cpu().numpy())
        return np.concatenate(out, 0)

    def encode_text_groups(self, groups: list[list[str]]) -> np.ndarray:
        """Prompt-ensemble: average the embeddings of each group's prompts, renormalise."""
        flat = [t for g in groups for t in g]
        emb = self.encode_texts(flat)
        res, k = [], 0
        for g in groups:
            m = emb[k:k + len(g)].mean(0)
            res.append(m / np.linalg.norm(m))
            k += len(g)
        return np.stack(res)
