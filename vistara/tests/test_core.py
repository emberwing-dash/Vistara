"""Self-contained checks (no personal or dataset videos): python tests/test_core.py"""
import subprocess
import sys
import tempfile
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "code"))
from vistara import evalkit  # noqa: E402


def test_metrics():
    gt = {"v1": [{"verb": "cut", "noun": "onion", "segment": [10, 14]}, {"verb": "open", "noun": "fridge", "segment": [30, 34]}]}
    assert abs(evalkit.action_map(gt, gt) - 1.0) < 1e-6
    bad = {"v1": [{"verb": "cut", "noun": "onion", "segment": [50, 54], "score": 0.9}]}
    assert evalkit.action_map(bad, gt) == 0.0
    off = {"v1": [{"verb": "cut", "noun": "onion", "segment": [11, 15], "score": 0.9}]}
    assert 0 < evalkit.action_map(off, gt) < 1
    assert evalkit.coverage({"v1": [[9, 15], [29, 35]]}, gt, {"v1": 100}) == 1.0
    assert evalkit.coverage({"v1": [[9, 15]]}, gt, {"v1": 100}) == 0.5
    assert evalkit.coverage({"v1": [[0, 60]]}, gt, {"v1": 100}) < 1.0       # over budget is penalised
    print("metrics ok")


def test_training_roundtrip():
    from vistara.features import FEAT_DIM
    rng = np.random.RandomState(0)
    protos_v, protos_n = rng.randn(97, FEAT_DIM), rng.randn(300, FEAT_DIM)
    with tempfile.TemporaryDirectory() as td:
        fd = Path(td) / "feats"
        fd.mkdir()
        for i in range(5):
            v, n = rng.randint(0, 97, 300), rng.randint(0, 300, 300)
            fg = rng.rand(300) < 0.7
            X = (protos_v[v] + protos_n[n]) * 0.3 + rng.randn(300, FEAT_DIM) * 0.5
            X[~fg] = rng.randn((~fg).sum(), FEAT_DIM) * 0.5
            np.savez(fd / f"vid{i}.npz", X=X.astype(np.float32), verb=np.where(fg, v, -1), noun=np.where(fg, n, -1), t=np.arange(300.0))
        out = Path(td) / "head.pt"
        r = subprocess.run([sys.executable, str(ROOT / "training" / "train_head.py"), "--feats", str(fd), "--out", str(out), "--epochs", "15"],
                           capture_output=True, text=True)
        assert r.returncode == 0, r.stderr[-500:]
        last = [l for l in r.stdout.splitlines() if l.startswith("ep")][-1]
        verb_acc = float(last.split("verb@1")[1].split()[0])
        assert verb_acc > 0.2, last                      # chance is ~0.01 -> the head learns on separable data
        from vistara.head import HeadRuntime
        h = HeadRuntime(out, "cpu")
        fgp, pv, pn = h.predict(X[0].astype(np.float32))
        assert pv.shape == (97,) and pn.shape == (300,) and abs(pv.sum() - 1) < 1e-4
        print("training roundtrip ok:", last)


def test_pipeline_on_synthetic_video():
    """Synthetic moving shapes (no real footage): checks the whole pipeline produces a valid, in-budget output."""
    from vistara.pipeline import VistaraPipeline
    from vistara.schema import validate
    with tempfile.TemporaryDirectory() as td:
        vp = str(Path(td) / "synthetic.mp4")
        w = cv2.VideoWriter(vp, cv2.VideoWriter_fourcc(*"mp4v"), 25, (320, 240))
        rng = np.random.RandomState(1)
        for f in range(25 * 60):
            img = np.full((240, 320, 3), 40 + (f // 250) * 30, np.uint8)
            x = int(160 + 100 * np.sin(f / 15.0)) if (f // 150) % 2 == 0 else 160
            cv2.circle(img, (x, 180), 25, (50, 200, 255), -1)
            img += rng.randint(0, 6, img.shape, dtype=np.uint8)
            w.write(img)
        w.release()
        res = VistaraPipeline().analyse(vp)
        out = res["output"]
        errs, _ = validate(out)
        assert not errs, errs
        kept = sum(h["segment"][1] - h["segment"][0] for h in out["highlights"])
        assert kept <= 0.15 * out["duration_sec"] + 1e-6, kept
        print("pipeline ok:", out["label"], len(out["highlights"]), "segments,", round(kept, 1), "s kept")


def test_head_in_inference():
    """A (random) head dropped in weights/ must load, blend, and still give valid kitchen-domain output."""
    import torch
    from vistara import recognizer as R
    from vistara.config import WEIGHTS_DIR
    from vistara.features import FEAT_DIM
    from vistara.head import ActionHead
    from vistara.pipeline import VistaraPipeline
    from vistara.schema import validate
    hp = WEIGHTS_DIR / "head.pt"
    assert not hp.exists(), "a real head.pt is present; refusing to overwrite it"
    torch.save({"state_dict": ActionHead().state_dict(), "dim": FEAT_DIM, "hidden": 1024, "meta": {"blend": 0.7}}, hp)
    try:
        with tempfile.TemporaryDirectory() as td:
            vp = str(Path(td) / "synthetic.mp4")
            w = cv2.VideoWriter(vp, cv2.VideoWriter_fourcc(*"mp4v"), 25, (320, 240))
            for f in range(25 * 40):
                img = np.full((240, 320, 3), 60, np.uint8)
                cv2.circle(img, (int(160 + 120 * np.sin(f / 9.0)), 190), 30, (60, 180, 255), -1)
                w.write(img)
            w.release()
            p = VistaraPipeline()
            assert p.rec.head is not None
            orig = p.rec.classify_video
            p.rec.classify_video = lambda e: ("cooking", 0.9, {**orig(e)[2], "cooking": 0.9})   # force the kitchen path
            out = p.analyse(vp)["output"]
            assert not validate(out)[0]
            print("head inference ok:", len(out["actions"]), "actions")
    finally:
        hp.unlink(missing_ok=True)


if __name__ == "__main__":
    test_head_in_inference()
    test_metrics()
    test_training_roundtrip()
    test_pipeline_on_synthetic_video()
    print("ALL OK")
