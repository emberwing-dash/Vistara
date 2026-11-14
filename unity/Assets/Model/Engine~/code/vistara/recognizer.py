"""Zero-shot video label + verb-noun action recognition/localisation on top of CLIP embeddings.

Honest scope: CLIP is an image-text model, so verb discrimination relies on appearance cues
(hand/object configuration) rather than learned motion dynamics. Optical-flow activity gates and
refines the temporal boundaries. See README for limitations.
"""
from __future__ import annotations
import logging
import math
from dataclasses import dataclass
import numpy as np
from . import vocab
from .config import Config, WEIGHTS_DIR
from .features import window_feature
from .model import ClipEncoder

log = logging.getLogger("vistara.recog")

# Mild down-weighting of scene-level nouns that are almost never the manipulated object.
_SCENE_NOUNS = {"kitchen", "floor", "wall", "window", "air", "light", "heat", "power", "rest", "stand", "top"}


# Words whose CLIP text embedding is ambiguous out of context (non-physical or multi-sense) -> penalised.
_AMBIGUOUS_NOUNS = {"spreads", "pith", "stalk", "bar", "label", "cover", "filter", "stand", "rest", "power", "alarm", "clip",
                    "cap", "roll", "wrap", "seed", "stock", "recipe", "plug", "knob", "holder", "top", "squash", "mat", "rubber"}
# Abstract / hard-to-see verbs: appearance alone cannot separate them reliably.
_ABSTRACT_VERBS = {"choose", "switch", "wait", "finish", "form", "use", "let-go", "transition", "check", "set", "look",
                   "feel", "increase", "lower", "gather", "prepare", "search", "measure", "divide", "mark", "unfreeze"}


@dataclass
class ActionWindow:
    start: float
    end: float
    pairs: list  # [(verb, noun, score)]
    confidence: float


def softmax(x: np.ndarray, t: float = 100.0) -> np.ndarray:
    z = x * t
    z = z - z.max(-1, keepdims=True)
    e = np.exp(z)
    return e / e.sum(-1, keepdims=True)


class Recognizer:
    def __init__(self, enc: ClipEncoder, cfg: Config):
        self.enc, self.cfg = enc, cfg
        self.labels = vocab.labels()
        self.verbs = vocab.verbs()
        self.nouns = vocab.nouns()
        self.noun_ph = [vocab.noun_phrase(n) for n in self.nouns]
        self.verb_ph = [vocab.verb_phrase(v) for v in self.verbs]
        self._build_text()
        self.label_head = None                       # optional fine-tuned video-label classifier (weights/label_head.pt)
        lp = WEIGHTS_DIR / "label_head.pt"
        if lp.exists():
            try:
                import torch
                ck = torch.load(lp, map_location="cpu")
                if list(ck["labels"]) == list(self.labels):
                    self.label_head = (ck["W"].numpy().astype(np.float32), ck["b"].numpy().astype(np.float32))
                    log.info("loaded trained label head %s", lp)
                else:
                    log.warning("label head classes differ from the official labels; ignoring it")
            except Exception as e:  # noqa: BLE001
                log.warning("could not load label head (%s); using zero-shot labels", e)
        self.head = None
        hp = WEIGHTS_DIR / "head.pt"
        if hp.exists():
            try:
                from .head import HeadRuntime
                self.head = HeadRuntime(hp, enc.device)
                log.info("loaded trained action head %s", hp)
            except Exception as e:  # noqa: BLE001
                log.warning("could not load action head (%s); using zero-shot only", e)

    def _build_text(self):
        e = self.enc
        self.label_emb = e.encode_text_groups([vocab.LABEL_PROMPTS[l] for l in self.labels])
        self.noun_emb = e.encode_text_groups([
            [f"a first-person photo of a {n} in a kitchen", f"hands holding a {n}", f"a photo of a {n}"]
            for n in self.noun_ph])
        self.verb_emb = e.encode_text_groups([
            [f"a first-person photo of hands {v} something in a kitchen", f"a person {v} an object"]
            for v in self.verb_ph])
        # class prior from the official class ordering (EPIC keys are listed by descending frequency)
        self.noun_logprior = np.array([-0.35 * math.log(i + 8) for i in range(len(self.nouns))])
        self.verb_logprior = np.array([-0.35 * math.log(i + 8) for i in range(len(self.verbs))])
        for i, n in enumerate(self.nouns):
            if n in _SCENE_NOUNS:
                self.noun_logprior[i] -= 1.0
            if n in _AMBIGUOUS_NOUNS:
                self.noun_logprior[i] -= 1.5
        for i, v in enumerate(self.verbs):
            if v in _ABSTRACT_VERBS:
                self.verb_logprior[i] -= 1.0

    # ---------------- video level label ----------------
    def classify_video(self, emb: np.ndarray) -> tuple[str, float, dict]:
        if self.label_head is not None:                    # fine-tuned (initialised from the zero-shot prompts)
            W, b = self.label_head
            p = softmax(emb @ W.T + b, 1.0).mean(0)
        else:
            sims = emb @ self.label_emb.T                  # (N, L)
            p = softmax(sims, 100.0).mean(0)               # average per-frame posterior
        p = p / p.sum()
        i = int(p.argmax())
        return self.labels[i], float(p[i]), {l: float(x) for l, x in zip(self.labels, p)}

    # ---------------- actions ----------------
    def recognise(self, times: np.ndarray, emb: np.ndarray, activity: np.ndarray, duration: float,
                  motion: np.ndarray | None = None) -> list[ActionWindow]:
        cfg = self.cfg
        W, H = cfg.action_window_s, cfg.action_hop_s
        wins: list[ActionWindow] = []
        t0 = 0.0
        while t0 < duration - 1e-6:
            t1 = min(t0 + W, duration)
            idx = np.where((times >= t0) & (times < t1))[0]
            if len(idx) == 0:
                t0 += H
                continue
            act = float(activity[idx].mean())
            if act >= cfg.idle_gate:                        # idle footage -> no action claim
                v = emb[idx].mean(0)
                v /= np.linalg.norm(v)
                feat = window_feature(emb, motion, times, t0, t1) if (self.head is not None and motion is not None) else None
                w = self._window_pairs(v, t0, t1, act, feat)
                if w is not None:
                    wins.append(w)
            t0 += H
        return wins

    def _window_pairs(self, v: np.ndarray, t0: float, t1: float, act: float, feat=None):
        cfg = self.cfg
        ln = self.noun_emb @ v * 100.0 + self.noun_logprior
        lv = self.verb_emb @ v * 100.0 + self.verb_logprior
        pn = softmax(ln, 1.0)
        pv = softmax(lv, 1.0)
        fgp = 1.0
        if feat is not None:                       # blend in the trained head (learned on the EPIC train split)
            fgp, hv, hn = self.head.predict(feat)
            if fgp < 0.1:
                return None
            a = float(self.head.meta.get("blend", 0.7))
            pn = (1 - a) * pn + a * hn
            pv = (1 - a) * pv + a * hv
        ni = np.argsort(-pn)[:cfg.top_nouns]
        vi = np.argsort(-pv)[:cfg.top_verbs]
        cand = [(int(a), int(b)) for a in vi for b in ni]
        texts = [[f"a first-person photo of a person {self.verb_ph[a]} the {self.noun_ph[b]}",
                  f"hands {self.verb_ph[a]} a {self.noun_ph[b]} in a kitchen"] for a, b in cand]
        pe = self.enc.encode_text_groups(texts)
        pp = softmax(pe @ v, 100.0)
        pn_c = np.array([pn[b] for _, b in cand]) / pn[ni].sum()
        pv_c = np.array([pv[a] for a, _ in cand]) / pv[vi].sum()
        score = 0.5 * pp + 0.25 * pn_c + 0.25 * pv_c
        order = np.argsort(-score)[:cfg.pairs_per_window]
        gate = (0.6 + 0.4 * min(1.0, act / 0.4)) * (0.5 + 0.5 * fgp)
        pairs = []
        for o in order:
            a, b = cand[int(o)]
            pairs.append((self.verbs[a], self.nouns[b], float(np.clip(score[o] * gate, 0, 1))))
        return ActionWindow(t0, t1, pairs, pairs[0][2])

    # ---------------- temporal localisation: merge identical consecutive predictions ----------------
    def merge(self, wins: list[ActionWindow], times: np.ndarray, activity: np.ndarray, duration: float) -> list[dict]:
        cfg = self.cfg
        open_: dict[tuple, dict] = {}
        done: list[dict] = []

        for w in wins:
            keys = {(v, n) for v, n, _ in w.pairs}
            for k in list(open_.keys()):
                if k not in keys or w.start > open_[k]["end"] + 1e-6:
                    done.append(open_.pop(k))
            for v, n, s in w.pairs:
                k = (v, n)
                if k in open_:
                    seg = open_[k]
                    seg["end"] = min(w.end, seg["start"] + cfg.max_action_s)
                    seg["scores"].append(s)
                else:
                    open_[k] = {"verb": v, "noun": n, "start": w.start, "end": w.end, "scores": [s]}
        done.extend(open_.values())

        out = []
        for seg in done:
            s, e = self._refine(seg["start"], seg["end"], times, activity)
            e = min(e, duration)
            if e <= s or e - s < 1.0:
                continue
            sc = float(np.clip(0.6 * max(seg["scores"]) + 0.4 * float(np.mean(seg["scores"])), 0, 1))
            out.append({"verb": seg["verb"], "noun": seg["noun"],
                        "segment": [round(s, 2), round(e, 2)], "score": round(sc, 4)})
        out.sort(key=lambda a: -a["score"])
        out = out[:cfg.max_actions]
        out.sort(key=lambda a: (a["segment"][0], -a["score"]))
        return out

    @staticmethod
    def _refine(s: float, e: float, times: np.ndarray, activity: np.ndarray) -> tuple[float, float]:
        """Trim idle lead-in / tail using the activity signal."""
        idx = np.where((times >= s) & (times < e))[0]
        if len(idx) < 3:
            return s, e
        a = activity[idx]
        thr = 0.5 * float(np.median(a))
        act = np.where(a >= thr)[0]
        if len(act) == 0:
            return s, e
        step = float(times[1] - times[0])
        ns, ne = float(times[idx[act[0]]]), float(times[idx[act[-1]]]) + step
        if ne - ns < 2.0:
            return s, e
        return ns, min(ne, e)
