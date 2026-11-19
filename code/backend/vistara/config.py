"""Central configuration. All tunables live here so the pipeline stays reproducible."""
from __future__ import annotations
import dataclasses
import os
from dataclasses import dataclass
from pathlib import Path

PKG_DIR = Path(__file__).resolve().parent
BACKEND_DIR = PKG_DIR.parent                 # <repo>/code/backend
REPO_DIR = BACKEND_DIR.parent.parent         # <repo>


def _default_weights() -> Path:
    for cand in (REPO_DIR / "weights", BACKEND_DIR / "weights"):
        if (cand / "head.pt").exists():
            return cand
    return REPO_DIR / "weights"


WEIGHTS_DIR = Path(os.environ.get("VISTARA_WEIGHTS", _default_weights()))
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
    clip_model: str = os.environ.get("VISTARA_CLIP_MODEL", "ViT-L-14-quickgelu")
    clip_pretrained: str = os.environ.get("VISTARA_CLIP_PRETRAINED", "openai")

    # --- action recognition ---
    action_window_s: float = 4.0
    action_hop_s: float = 2.0
    top_nouns: int = 10
    top_verbs: int = 8
    pairs_per_window: int = 10       # more ranked candidates per window: lifts mAP (AP only rewards extra lower-ranked hits)
    min_action_s: float = 2.0
    max_action_s: float = 14.0
    max_actions: int = 2000
    # data priors from the public EPIC train annotations (weights/pair_prior.npz); weights are tuned on held-out train videos
    use_data_prior: bool = True
    use_avion: bool = True           # AVION (EPIC-100 action recogniser, weights/avion_vitl_ek100_fp16.pt) when the weights and the video file are present
    prior_verb_w: float = 1.2        # weights of log P(verb) / log P(noun) added to the CLIP logits (tuned on held-out EPIC train windows)
    prior_noun_w: float = 1.2
    prior_pair_w: float = 0.0
    # kitchen (EPIC) videos: highlight selection tuned for DISTINCT-ACTION COVERAGE on EPIC train videos (4 s segments, spread in time, no budget fill)
    cook_seg_len_choices: tuple = (4.0,)
    cook_fill_budget: bool = False
    cook_spread_weight: float = 1.0
    cook_spread_tau_s: float = 40.0
    cook_pair_novelty_weight: float = 0.5
    cook_redundancy_weight: float = 0.3
    cook_importance_mix: float = 0.5   # 1 = pure importance, 0 = flat (selection driven by diversity only): coverage rewards DISTINCT actions, not busy ones
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
    fill_budget: bool = True         # grow segments into leftover budget (helps frame-level F1, hurts distinct-action coverage)
    spread_weight: float = 0.0       # penalty for being close in time to already selected footage (temporal diversity)
    spread_tau_s: float = 20.0
    min_gap_s: float = 0.5

    def __post_init__(self):
        """Experiment aid: VISTARA_CFG_<FIELD>=value overrides a field (tuples as comma lists). Not used in the submission run."""
        for f in dataclasses.fields(self):
            v = os.environ.get("VISTARA_CFG_" + f.name.upper())
            if v is None:
                continue
            cur = getattr(self, f.name)
            if isinstance(cur, tuple):
                setattr(self, f.name, tuple(float(x) for x in v.split(",")))
            elif isinstance(cur, bool):
                setattr(self, f.name, v.lower() in ("1", "true"))
            elif isinstance(cur, int):
                setattr(self, f.name, int(v))
            elif isinstance(cur, float):
                setattr(self, f.name, float(v))
            else:
                setattr(self, f.name, v)


def for_domain(cfg: Config, domain: str) -> Config:
    """Selection settings for a domain: kitchen videos use the coverage-tuned policy, everything else keeps the general one."""
    if domain != "cooking":
        return cfg
    return dataclasses.replace(cfg, seg_len_choices=cfg.cook_seg_len_choices, fill_budget=cfg.cook_fill_budget, spread_weight=cfg.cook_spread_weight,
                               spread_tau_s=cfg.cook_spread_tau_s, pair_novelty_weight=cfg.cook_pair_novelty_weight,
                               redundancy_weight=cfg.cook_redundancy_weight)


DEFAULT = Config()
