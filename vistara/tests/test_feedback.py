"""Feedback loop through the HTTP API on a synthetic video: python tests/test_feedback.py"""
import sys
import tempfile
import time
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "code"))
sys.path.insert(0, str(ROOT / "server"))


def make_video(path):
    w = cv2.VideoWriter(path, cv2.VideoWriter_fourcc(*"mp4v"), 25, (320, 240))
    rng = np.random.RandomState(3)
    for f in range(25 * 150):
        sec = f / 25
        img = np.full((240, 320, 3), 30 + int(sec // 15) * 12, np.uint8)
        if int(sec // 15) % 2 == 0:                      # every other 15 s block has a moving object
            cv2.circle(img, (int(160 + 110 * np.sin(f / 8.0)), 170), 28, (60, 190, 255), -1)
        img += rng.randint(0, 5, img.shape, dtype=np.uint8)
        w.write(img)
    w.release()


def main():
    import app
    from fastapi.testclient import TestClient
    c = TestClient(app.app)
    with tempfile.TemporaryDirectory() as td:
        vp = str(Path(td) / "synthetic.mp4")
        make_video(vp)
        jid = c.post("/analyze_path", json={"path": vp}).json()["job_id"]
        for _ in range(180):
            j = c.get(f"/jobs/{jid}").json()
            if j["status"] in ("done", "error"):
                break
            time.sleep(1)
        assert j["status"] == "done", j
        r0 = c.get(f"/jobs/{jid}/result").json()
        hl0 = [h["segment"] for h in r0["output"]["highlights"]]
        print("before:", hl0)
        first = hl0[0]
        r1 = c.post(f"/jobs/{jid}/feedback", json={"start": first[0], "end": first[1], "vote": -1}).json()
        hl1 = [h["segment"] for h in r1["output"]["highlights"]]
        print("after drop:", hl1)
        assert not any(min(s[1], first[1]) - max(s[0], first[0]) > 0 for s in hl1), "dropped segment still selected"
        kept = sum(s[1] - s[0] for s in hl1)
        assert kept <= 0.15 * r1["output"]["duration_sec"] + 1e-6
        assert r1["analysis"]["feedback_votes"] == 1 and Path(r1["highlight_path"]).exists()
        second = hl1[0]
        r2 = c.post(f"/jobs/{jid}/feedback", json={"start": second[0], "end": second[1], "vote": 1}).json()
        hl2 = [h["segment"] for h in r2["output"]["highlights"]]
        print("after like:", hl2)
        assert any(min(s[1], second[1]) - max(s[0], second[0]) > 0 for s in hl2), "liked segment not kept"
        assert r2["analysis"]["feedback_votes"] == 2
    print("FEEDBACK OK")


if __name__ == "__main__":
    main()
