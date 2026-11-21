"""Collect outputs/*.json into predictions.jsonl (one JSON per video) and results.csv (one summary row per video).
   python code/backend/make_predictions.py [--outputs outputs] [--out .]
Standard library only; run it from the repo root after the outputs are generated."""
import argparse, csv, json
from pathlib import Path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--outputs", default="outputs")
    ap.add_argument("--out", default=".")
    a = ap.parse_args()
    files = sorted(Path(a.outputs).glob("*.json"))
    out = Path(a.out); out.mkdir(parents=True, exist_ok=True)
    with open(out / "predictions.jsonl", "w", encoding="utf-8") as jl, open(out / "results.csv", "w", newline="", encoding="utf-8") as cf:
        w = csv.writer(cf)
        w.writerow(["video_id", "duration_sec", "label", "label_score", "n_actions", "n_highlights", "highlight_sec", "highlight_pct"])
        for f in files:
            d = json.loads(f.read_text(encoding="utf-8"))
            jl.write(json.dumps(d, ensure_ascii=False, separators=(",", ":")) + "\n")
            hs = sum(h["segment"][1] - h["segment"][0] for h in d["highlights"])
            w.writerow([d["video_id"], d["duration_sec"], d["label"]["name"], d["label"]["score"], len(d["actions"]), len(d["highlights"]),
                        round(hs, 2), round(100 * hs / d["duration_sec"], 2)])
    print(f"{len(files)} videos -> {out / 'predictions.jsonl'}, {out / 'results.csv'}")


main()
