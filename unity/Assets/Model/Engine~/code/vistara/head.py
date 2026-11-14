"""Small trainable head on frozen CLIP window features: foreground / verb / noun."""
from __future__ import annotations
from pathlib import Path
import numpy as np
import torch
import torch.nn as nn
from .features import FEAT_DIM

N_VERB, N_NOUN = 97, 300      # official supported_classes (without "other")


class ActionHead(nn.Module):
    def __init__(self, dim: int = FEAT_DIM, hidden: int = 1024, p: float = 0.3):
        super().__init__()
        self.body = nn.Sequential(nn.Linear(dim, hidden), nn.GELU(), nn.Dropout(p),
                                  nn.Linear(hidden, hidden), nn.GELU(), nn.Dropout(p))
        self.fg = nn.Linear(hidden, 1)
        self.verb = nn.Linear(hidden, N_VERB)
        self.noun = nn.Linear(hidden, N_NOUN)

    def forward(self, x):
        h = self.body(x)
        return self.fg(h).squeeze(-1), self.verb(h), self.noun(h)


class HeadRuntime:
    """Inference wrapper (CPU/GPU) returning probabilities."""

    def __init__(self, path: Path, device: str):
        ck = torch.load(path, map_location=device)
        self.model = ActionHead(ck.get("dim", FEAT_DIM), ck.get("hidden", 1024)).to(device).eval()
        self.model.load_state_dict(ck["state_dict"])
        self.device = device
        self.meta = ck.get("meta", {})

    @torch.inference_mode()
    def predict(self, feat: np.ndarray):
        x = torch.from_numpy(feat[None]).to(self.device)
        fg, v, n = self.model(x)
        return float(torch.sigmoid(fg)[0]), torch.softmax(v, -1)[0].cpu().numpy(), torch.softmax(n, -1)[0].cpu().numpy()
