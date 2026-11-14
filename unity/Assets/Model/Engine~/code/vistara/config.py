"""Central configuration. All tunables live here so the pipeline stays reproducible."""
from __future__ import annotations
import os
from dataclasses import dataclass
from pathlib import Path

PKG_DIR = Path(__file__).resolve().parent
BACKEND_DIR = PKG_DIR.parent.parent
WEIGHTS_DIR = Path(os.environ.get("VISTARA_WEIGHTS", BACKEND_DIR / "weights"))
# pretrained CLIP backbone cache; defaults to the weights dir, but can live elsewhere (e.g. Unity: Assets/Model/Backbone~)
CLIP_DIR = Path(os.environ.get("VISTARA_CLIP_DIR", str(WEIGHTS_DIR)))
CLASSES_PATH = PKG_DIR / "supported_classes.json"


@dataclass
class Config:
    team_id: str = os.environ.get("TEAM_ID", "vistara")

    # --- sampling ---
    sample_fps: float = 2.0          # CLIP + importance sampling rate
    motion_gap_s: float = 0.25       # temporal gap used for optical flow
    flow_width: int = 160            # downscaled width for Farneback flow
    clip_batch: int = 64

    # --- model ---
    clip_model: str = "ViT-B-32"
    clip_pretrained: str = "openai"

    # --- action recognition ---
    action_window_s: float = 4.0
    action_hop_s: float = 2.0
    top_nouns: int = 6
    top_verbs: int = 6
    pairs_per_window: int = 3
    min_action_s: float = 2.0
    max_action_s: float = 14.0
    max_actions: int = 2000
    idle_gate: float = 0.12          # windows with activity below this emit no actions

    # --- highlights ---
    budget_frac: float = 0.15
    budget_safety: float = 0.985     # stay just under 15 % after rounding
    min_segment_s: float = 4.0
    seg_len_choices: tuple = (4.0, 6.0, 8.0)
    smooth_sigma_s: float = 1.5
    redundancy_weight: float = 0.45
    pair_novelty_weight: float = 0.25
    merge_gap_s: float = 0.75
    min_gap_s: float = 0.5


DEFAULT = Config()
