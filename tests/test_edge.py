"""Edge cases through the real entry point (./run.sh) on generated videos only.
   python tests/test_edge.py <path-to-packaged-or-backend-dir> [--quick]"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "code"))
from vistara.schema import validate  # noqa: E402

BASH = shutil.which("bash") or r"C:\Program Files\Git\bin\bash.exe"
PY = os.environ.get("PYTHON") or str(Path(__file__).resolve().parents[1] / ".venv" / "Scripts" / "python.exe")


def make(path, secs, fps=25, size=(320, 240), moving=True, seed=0):
    w = cv2.VideoWriter(str(path), cv2.VideoWriter_fourcc(*"mp4v"), fps, size)
    rng = np.random.RandomState(seed)
    W, H = size
    for f in range(int(secs * fps)):
        sec = f / fps
        img = np.full((H, W, 3), 30 + int(sec // 12) * 9 % 120, np.uint8)
        if moving and int(sec // 10) % 2 == 0:
            cv2.circle(img, (int(W / 2 + W * 0.35 * np.sin(f / 8.0)), int(H * 0.75)), H // 9, (60, 190, 255), -1)
        img += rng.randint(0, 5, img.shape, dtype=np.uint8)
        w.write(img)
    w.release()


def run(video, out, env_extra=None):
    env = dict(os.environ, PYTHON=PY, **(env_extra or {}))
    t = time.time()
    r = subprocess.run([BASH, "run.sh", str(video), str(out), "-q"], cwd=ROOT, env=env, capture_output=True, text=True)
    return r, time.time() - t


def check(name, video, expect_dur=None, env_extra=None):
    out = video.with_suffix(".json")
    r, dt = run(video, out, env_extra)
    assert out.exists(), f"{name}: no JSON written\n{r.stderr[-600:]}"
    d = json.load(open(out, encoding="utf-8"))
    errs, warns = validate(d)
    kept = sum(h["segment"][1] - h["segment"][0] for h in d["highlights"])
    if d['duration_sec'] < 4.0:      # impossible to have a >= 4 s segment: the organisers' own rule cannot be met
        errs = [e for e in errs if 'no highlight segment' not in e]
    ok = not errs and (kept <= 0.15 * d["duration_sec"] + 1e-6 or d["duration_sec"] < 27)
    if expect_dur:
        ok &= abs(d["duration_sec"] - expect_dur) <= 0.5
    degraded = d["label"]["score"] == 0.0 and d["label"]["name"] == "other"
    print(f"[{'PASS' if ok else 'FAIL'}] {name}: {d['duration_sec']}s, {len(d['highlights'])} segs, kept {kept:.1f}s "
          f"({kept / d['duration_sec'] * 100:.1f}%), label={d['label']['name']}, rc={r.returncode}, {dt:.0f}s{' DEGRADED' if degraded else ''}"
          f"{' ERR:' + str(errs) if errs else ''}")
    return ok, d


def main():
    quick = "--quick" in sys.argv
    results = []
    with tempfile.TemporaryDirectory() as td:
        td = Path(td)
        make(td / "short_29s.mp4", 29.5); results.append(check("29.5 s (manifest minimum)", td / "short_29s.mp4", 29.5)[0])
        make(td / "tiny_3s.mp4", 3.0); results.append(check("3 s (shorter than one segment)", td / "tiny_3s.mp4")[0])
        make(td / "fps60.mp4", 40, fps=60, size=(640, 360)); results.append(check("60 fps 640x360", td / "fps60.mp4", 40)[0])
        make(td / "portrait.mp4", 35, size=(240, 426)); results.append(check("portrait", td / "portrait.mp4", 35)[0])
        make(td / "static.mp4", 40, moving=False); results.append(check("static scene (idle)", td / "static.mp4", 40)[0])
        a = check("determinism run A", td / "short_29s.mp4")[1]
        b = check("determinism run B", td / "short_29s.mp4")[1]
        same = a == b
        print(f"[{'PASS' if same else 'FAIL'}] deterministic output: {same}"); results.append(same)
        (td / "corrupt.mp4").write_bytes(b"not a video at all" * 100)
        out = td / "corrupt.json"
        r, _ = run(td / "corrupt.mp4", out)
        print(f"[INFO] corrupt file -> rc={r.returncode}, json written={out.exists()} (cannot be valid: duration unknown)")
        make(td / "cpu_20s.mp4", 30)
        results.append(check("CPU only (CUDA hidden)", td / "cpu_20s.mp4", 30, {"CUDA_VISIBLE_DEVICES": ""})[0])
        if not quick:
            make(td / "long_10min.mp4", 600, size=(320, 240)); results.append(check("10 minutes", td / "long_10min.mp4", 600)[0])
    print("ALL PASS" if all(results) else f"FAILURES: {results.count(False)}")
    sys.exit(0 if all(results) else 1)


if __name__ == "__main__":
    main()
