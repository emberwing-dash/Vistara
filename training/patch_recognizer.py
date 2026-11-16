"""One-off patch helper (kept for traceability): wires the trained head into recognizer.py / pipeline.py."""
from pathlib import Path

base = Path(__file__).resolve().parents[1] / "code" / "vistara"

p = base / "recognizer.py"
s = p.read_text(encoding="utf-8")
if "self.head" not in s:
    s = s.replace("from .config import Config\n", "from .config import Config, WEIGHTS_DIR\nfrom .features import window_feature\n", 1)
    s = s.replace("        self._build_text()\n\n    def _build_text", '''        self._build_text()
        self.head = None
        hp = WEIGHTS_DIR / "head.pt"
        if hp.exists():
            try:
                from .head import HeadRuntime
                self.head = HeadRuntime(hp, enc.device)
                log.info("loaded trained action head %s", hp)
            except Exception as e:  # noqa: BLE001
                log.warning("could not load action head (%s); using zero-shot only", e)

    def _build_text''', 1)
    s = s.replace("def recognise(self, times: np.ndarray, emb: np.ndarray, activity: np.ndarray, duration: float) -> list[ActionWindow]:",
                  "def recognise(self, times: np.ndarray, emb: np.ndarray, activity: np.ndarray, duration: float,\n                  motion: np.ndarray | None = None) -> list[ActionWindow]:")
    s = s.replace('''                v = emb[idx].mean(0)
                v /= np.linalg.norm(v)
                wins.append(self._window_pairs(v, t0, t1, act))''', '''                v = emb[idx].mean(0)
                v /= np.linalg.norm(v)
                feat = window_feature(emb, motion, times, t0, t1) if (self.head is not None and motion is not None) else None
                w = self._window_pairs(v, t0, t1, act, feat)
                if w is not None:
                    wins.append(w)''')
    s = s.replace("def _window_pairs(self, v: np.ndarray, t0: float, t1: float, act: float) -> ActionWindow:",
                  "def _window_pairs(self, v: np.ndarray, t0: float, t1: float, act: float, feat=None):")
    s = s.replace('''        pv = softmax(lv, 1.0)
        ni =''', '''        pv = softmax(lv, 1.0)
        fgp = 1.0
        if feat is not None:                       # blend in the trained head (learned on the EPIC train split)
            fgp, hv, hn = self.head.predict(feat)
            if fgp < 0.1:
                return None
            a = float(self.head.meta.get("blend", 0.7))
            pn = (1 - a) * pn + a * hn
            pv = (1 - a) * pv + a * hv
        ni =''')
    s = s.replace("gate = 0.6 + 0.4 * min(1.0, act / 0.4)", "gate = (0.6 + 0.4 * min(1.0, act / 0.4)) * (0.5 + 0.5 * fgp)")
    p.write_text(s, encoding="utf-8")

p = base / "pipeline.py"
s = p.read_text(encoding="utf-8")
if "motion_matrix" not in s:
    s = s.replace("from .recognizer import Recognizer", "from .features import motion_matrix\nfrom .recognizer import Recognizer")
    s = s.replace("wins = self.rec.recognise(s.times, emb, act, info.duration)",
                  "wins = self.rec.recognise(s.times, emb, act, info.duration,\n                                      motion_matrix(s.hand_motion, s.local_motion, s.global_motion))")
    p.write_text(s, encoding="utf-8")
print("patched")
