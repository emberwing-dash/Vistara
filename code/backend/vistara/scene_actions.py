"""What is going on in the video, second by second, for the sports (FPVSum) categories - so the 3D guide can act the same thing.

The model gives no verb-noun actions for these videos, so the frames themselves are described: CLIP (already used for the video label) scores every ~2 s window of the video against a few
short descriptions of what happens in that sport (riding along, pedalling hard, turning, stopped, jumping ...). Each description has a generic movement id that the Unity app knows
(move, fast, turn, stop, jump, hit, climb, swim, idle). Windows are returned as {"t0", "t1", "action"}; consecutive windows with the same action are merged.
Nothing here uses video ids or labels from the ground truth; it only reads the frames' CLIP embeddings that the pipeline computed anyway."""
from __future__ import annotations

import numpy as np

# category label -> [(generic id, description)]; the first entry is the default
VOCAB: dict[str, list[tuple[str, str]]] = {
    "bike": [("move", "riding a bicycle along a road or trail"), ("fast", "riding a bicycle very fast downhill with speed"), ("turn", "turning a corner on a bicycle, leaning into the turn"),
             ("stop", "standing still next to a bicycle, not riding"), ("jump", "a bicycle jumping in the air over a ramp")],
    "bikepolo": [("move", "riding a bicycle on a court"), ("fast", "sprinting fast on a bicycle chasing a ball"), ("turn", "turning sharply on a bicycle"),
                 ("hit", "hitting a ball with a mallet while riding a bicycle"), ("stop", "standing still with a bicycle")],
    "motor": [("move", "riding a motorbike along a road"), ("fast", "riding a motorbike very fast"), ("turn", "leaning a motorbike into a corner"), ("stop", "a stopped motorbike, standing still")],
    "boxing": [("hit", "punching a heavy bag with boxing gloves"), ("fast", "throwing fast punch combinations"), ("idle", "standing in a boxing guard stance, resting"),
               ("move", "moving around a boxing ring, footwork")],
    "horse": [("move", "riding a horse at a walk"), ("fast", "riding a horse at a gallop, fast"), ("jump", "a horse jumping over an obstacle"), ("stop", "sitting on a horse standing still")],
    "jumping": [("jump", "jumping up in the air, parkour jump"), ("move", "running and jumping over obstacles"), ("stop", "standing still, preparing to jump")],
    "parkour": [("jump", "jumping over a wall or gap, parkour"), ("climb", "climbing up a wall"), ("move", "running through a city"), ("stop", "standing still, looking ahead")],
    "longboarding": [("move", "riding a longboard down a road"), ("fast", "riding a longboard very fast downhill"), ("turn", "carving a turn on a longboard"), ("stop", "standing still, not riding")],
    "skate": [("move", "riding a skateboard"), ("jump", "jumping a skateboard in the air, trick"), ("turn", "turning on a skateboard"), ("stop", "standing still with a skateboard")],
    "ski": [("move", "skiing down a snowy slope"), ("fast", "skiing very fast straight down the hill"), ("turn", "turning on skis, carving"), ("jump", "jumping on skis in the air"), ("stop", "standing still on skis")],
    "surfing": [("move", "surfing on a wave"), ("fast", "surfing fast along a big wave"), ("turn", "turning on a surfboard"), ("stop", "lying or sitting on a surfboard waiting")],
    "rockclimbing": [("climb", "climbing up a rock wall"), ("idle", "resting, hanging on a rock wall"), ("fast", "climbing quickly up a rock face")],
    "scuba": [("swim", "swimming underwater with fins"), ("idle", "floating still underwater looking at fish"), ("fast", "swimming fast underwater")],
    "plane": [("move", "flying in a plane, looking out of the window"), ("turn", "a plane banking in a turn"), ("fast", "taking off or landing quickly, fast")],
}

WINDOW_S = 2.0


def scene_actions(enc, label: str, emb: np.ndarray, times: np.ndarray, activity: np.ndarray | None = None) -> list[dict]:
    voc = VOCAB.get((label or "").lower())
    if not voc or emb is None or len(emb) == 0:
        return []
    texts = [d for _, d in voc]
    T = enc.encode_texts(texts)                                    # (K, D), L2-normalised like the frame embeddings
    logits = 100.0 * (emb @ T.T)                                   # (N, K)
    times = np.asarray(times, dtype=np.float64)
    end = float(times[-1]) + 0.5
    out: list[dict] = []
    t0 = 0.0
    act = np.asarray(activity, dtype=np.float64) if activity is not None and len(activity) == len(times) else None
    amax = float(np.max(act)) if act is not None and np.max(act) > 1e-6 else 1.0
    while t0 < end:
        t1 = min(end, t0 + WINDOW_S)
        m = (times >= t0) & (times < t1)
        if m.any():
            z = logits[m].mean(0)
            p = np.exp(z - z.max()); p /= p.sum()
            k = int(np.argmax(p))
            a = voc[k][0] if p[k] >= 0.34 else voc[0][0]           # not sure: the sport's default movement
            # (the camera is worn by the rider, so the picture moving little says nothing about standing still: no motion override)
            if a in ("stop", "idle") and p[k] < 0.6 and voc[0][0] not in ("stop", "idle"):
                a = voc[0][0]                                      # only a clear "standing still" counts; otherwise the sport's default movement
        else:
            a = out[-1]["action"] if out else voc[0][0]
        if out and out[-1]["action"] == a:
            out[-1]["t1"] = round(t1, 2)
        else:
            out.append({"t0": round(t0, 2), "t1": round(t1, 2), "action": a})
        t0 = t1
    return out
