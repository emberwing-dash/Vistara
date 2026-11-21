"""Keeps the machine usable while the engine works: when VISTARA_YIELD_MS is set (the Unity app sets it), the heavy GPU loops run in small batches and pause briefly after each,
so the headset / editor keeps rendering instead of freezing. Without the variable (command line, evaluation) nothing changes."""
import os
import time

YIELD_MS = float(os.environ.get("VISTARA_YIELD_MS", "0") or 0)


def batch(default: int, env: str) -> int:
    v = os.environ.get(env)
    return max(1, int(v)) if v else default


def pause() -> None:
    if YIELD_MS > 0:
        time.sleep(YIELD_MS / 1000.0)
