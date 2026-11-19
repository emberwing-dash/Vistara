"""Vocabulary + prompt construction for the official supported classes."""
from __future__ import annotations
import json
from functools import lru_cache
from .config import CLASSES_PATH

# natural-language phrases for the 16 video-level labels (several prompts each -> ensemble)
LABEL_PROMPTS = {
    "bike": ["a first-person view of riding a bicycle", "a handlebar camera view while cycling on a road or trail"],
    "bikepolo": ["a first-person view of playing bike polo", "riding a bicycle with a mallet on a court"],
    "boxing": ["a first-person view of boxing", "gloved fists punching a bag or opponent in a boxing ring"],
    "cooking": ["a first-person view of cooking in a kitchen", "hands preparing food on a kitchen counter",
                "a person washing dishes or chopping vegetables in a kitchen"],
    "horse": ["a first-person view of horse riding", "riding a horse seen from the rider's helmet camera"],
    "jumping": ["a first-person view of jumping or skydiving", "a point of view of jumping off a height, skydiving or bungee jumping"],
    "longboarding": ["a first-person view of longboarding down a road", "riding a longboard skateboard down a hill"],
    "motor": ["a first-person view of riding a motorcycle", "a helmet camera view of a motorbike on the road"],
    "parkour": ["a first-person view of parkour", "running and vaulting over obstacles in a city, freerunning"],
    "plane": ["a first-person view from an airplane cockpit or flying a plane", "a view of the sky and wings from a small aircraft"],
    "rockclimbing": ["a first-person view of rock climbing", "hands gripping rock holds on a climbing wall or cliff"],
    "scuba": ["a first-person view of scuba diving underwater", "an underwater view with fish and coral reef"],
    "skate": ["a first-person view of skateboarding", "riding a skateboard in a skate park, doing tricks"],
    "ski": ["a first-person view of skiing on snow", "skis and snowy mountain slope seen from a helmet camera"],
    "surfing": ["a first-person view of surfing on ocean waves", "a surfboard riding a wave seen from the surfer"],
    "other": ["a video of something else", "an ordinary video of an unrelated everyday scene"],
}

# gerund / action phrases for the 98 EPIC verbs. Only used to build text prompts for CLIP.
VERB_PHRASES = {
    "take": "taking", "put": "putting down", "wash": "washing", "open": "opening", "close": "closing",
    "insert": "inserting", "turn-on": "turning on", "cut": "cutting", "turn-off": "turning off",
    "pour": "pouring", "mix": "mixing", "move": "moving", "remove": "removing", "throw": "throwing away",
    "dry": "drying", "shake": "shaking", "scoop": "scooping", "adjust": "adjusting", "squeeze": "squeezing",
    "peel": "peeling", "empty": "emptying", "press": "pressing", "flip": "flipping", "turn": "turning",
    "check": "checking", "scrape": "scraping", "fill": "filling", "apply": "applying", "fold": "folding",
    "scrub": "scrubbing", "break": "breaking", "pull": "pulling", "pat": "patting", "lift": "lifting",
    "hold": "holding", "eat": "eating", "wrap": "wrapping", "filter": "filtering", "look": "looking at",
    "unroll": "unrolling", "sort": "sorting", "hang": "hanging", "sprinkle": "sprinkling", "rip": "ripping",
    "spray": "spraying", "cook": "cooking", "add": "adding", "roll": "rolling", "search": "searching for",
    "crush": "crushing", "stretch": "stretching", "knead": "kneading", "divide": "dividing", "set": "setting",
    "feel": "feeling", "rub": "rubbing", "soak": "soaking", "brush": "brushing", "sharpen": "sharpening",
    "drop": "dropping", "drink": "drinking", "slide": "sliding", "water": "watering", "gather": "gathering",
    "attach": "attaching", "turn-down": "turning down", "coat": "coating", "transition": "moving between",
    "wear": "wearing", "measure": "measuring", "increase": "increasing", "unscrew": "unscrewing",
    "wait": "waiting for", "lower": "lowering", "form": "shaping", "smell": "smelling", "use": "using",
    "grate": "grating", "screw": "screwing", "let-go": "letting go of", "finish": "finishing", "stab": "stabbing",
    "serve": "serving", "uncover": "uncovering", "unwrap": "unwrapping", "choose": "choosing", "lock": "locking",
    "flatten": "flattening", "switch": "switching", "carry": "carrying", "season": "seasoning",
    "unlock": "unlocking", "prepare": "preparing", "bake": "baking", "mark": "marking", "bend": "bending",
    "unfreeze": "defrosting",
}

_NOUN_OVERRIDES = {"scotch:egg": "scotch egg", "air": "air", "other": "object"}


def noun_phrase(key: str) -> str:
    """EPIC keys are 'head:modifier[:modifier]' -> 'modifier head' (board:chopping -> chopping board)."""
    if key in _NOUN_OVERRIDES:
        return _NOUN_OVERRIDES[key]
    parts = key.split(":")
    if len(parts) == 1:
        return parts[0]
    return " ".join(parts[1:] + parts[:1])


@lru_cache(maxsize=1)
def load_classes() -> dict:
    with open(CLASSES_PATH, "r", encoding="utf-8") as f:
        return json.load(f)


def labels() -> list[str]:
    return load_classes()["labels"]


def verbs() -> list[str]:
    return [v for v in load_classes()["verbs"] if v != "other"]


def nouns() -> list[str]:
    return [n for n in load_classes()["nouns"] if n != "other"]


def verb_phrase(v: str) -> str:
    return VERB_PHRASES.get(v, v)
