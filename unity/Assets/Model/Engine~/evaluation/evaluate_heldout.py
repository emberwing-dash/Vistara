"""Held-out evaluation of the trained Vistara model on the Track-04 evaluation manifest.

Runs the REAL pipeline (CLIP + trained heads + importance + highlight selection) on every manifest video that is present on
disk, compares with the public ground truth and writes CSV / JSON results. Nothing here is trained or tuned on these videos.

Ground truth
  EPIC-KITCHENS-100  EPIC_100_validation.csv     (verb/noun class ids -> official keys; temporal boundaries)
  FPVSum             annotation/gt_data.mat       (frame-level consensus Score; top-15 % frames = ground-truth highlights)
                     video category = prefix of the video id (as in the public FPVSum list; only used for scoring)

Metrics (definitions follow TEAM_README.md; the official scorer is not public, so these are local approximations)
  action_map        mean AP over GT (verb,noun) classes, averaged over tIoU 0.1..0.5            (EPIC)
  action_coverage   share of distinct GT (verb,noun) pairs inside the highlights, x budget penalty (EPIC)
  highlight_f1      frame-level F1 of the highlights vs the top-15 % human-importance frames     (FPVSum)
  label_accuracy    video category accuracy                                                      (FPVSum)
  verb/noun/pair_acc  for every GT action: label of the best-scoring predicted action overlapping it (EPIC)
  loc_recall_iou0.1   share of GT actions with a predicted action at tIoU >= 0.1                 (EPIC)

    python evaluate_heldout.py --epic-root E:/.../EVAL/data/EPIC-KITCHENS --fpv-root E:/.../EVAL/fpvsum \
        --epic-ann E:/.../EPIC_100_validation.csv --fpv-gt E:/.../gt_data.mat --out ../Results
"""
from __future__ import annotations
import argparse
import csv
import json
import statistics
import sys
import time
from collections import defaultdict
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
for cand in (HERE.parent / "code", HERE.parent.parent / "code"):
    if (cand / "vistara").exists():
        sys.path.insert(0, str(cand))
        break

from vistara import evalkit  # noqa: E402
from vistara.config import Config  # noqa: E402
from vistara.schema import validate  # noqa: E402

BUDGET = 0.15


def ts(t: str) -> float:
    h, m, s = t.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def load_manifest(path: Path):
    return {r["video_id"]: r for r in csv.DictReader(open(path, encoding="utf-8"))}


def load_epic_gt(ann: Path, ids: set[str], classes: dict):
    verbs, nouns = classes["verbs"], classes["nouns"]
    gt = defaultdict(list)
    for r in csv.DictReader(open(ann, encoding="utf-8")):
        if r["video_id"] in ids:
            gt[r["video_id"]].append({"verb": verbs[int(r["verb_class"])], "noun": nouns[int(r["noun_class"])],
                                      "segment": [ts(r["start_timestamp"]), ts(r["stop_timestamp"])]})
    return gt


def load_fpv_gt(mat: Path):
    from scipy.io import loadmat
    m = loadmat(str(mat))["gt_data"][0]
    out = {}
    for e in m:
        out[str(e["Name"][0])] = np.asarray(e["Score"]).reshape(-1).astype(np.float64)
    return out


def hl_mask(segments, n_frames: int, duration: float):
    mask = np.zeros(n_frames, bool)
    for s, e in segments:
        a = int(np.floor(s / duration * n_frames)); b = int(np.ceil(e / duration * n_frames))
        mask[max(0, a):min(n_frames, b)] = True
    return mask


def f1_scores(pred: np.ndarray, gt: np.ndarray):
    tp = float((pred & gt).sum()); p = tp / max(1.0, float(pred.sum())); r = tp / max(1.0, float(gt.sum()))
    return (2 * p * r / (p + r) if p + r > 0 else 0.0), p, r


def action_accuracy(pred_actions, gt_actions):
    """For each GT action pick the best-scoring predicted action that overlaps it; report verb/noun/pair accuracy."""
    n = v = nn = pr = loc = 0
    for g in gt_actions:
        n += 1
        ov = [p for p in pred_actions if min(p["segment"][1], g["segment"][1]) - max(p["segment"][0], g["segment"][0]) > 0]
        if any(evalkit.tiou(p["segment"], g["segment"]) >= 0.1 for p in pred_actions):
            loc += 1
        if not ov:
            continue
        b = max(ov, key=lambda p: p["score"])
        v += b["verb"] == g["verb"]; nn += b["noun"] == g["noun"]; pr += b["verb"] == g["verb"] and b["noun"] == g["noun"]
    return n, v, nn, pr, loc


def find_video(roots, vid):
    for root in roots:
        for ext in (".MP4", ".mp4"):
            hits = list(Path(root).rglob(vid + ext))
            if hits:
                return hits[0]
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--manifest", default=str(HERE.parent / "data" / "manifest.csv"))
    ap.add_argument("--classes", default=str(HERE.parent / "code" / "vistara" / "supported_classes.json"))
    ap.add_argument("--epic-root", nargs="*", default=[])
    ap.add_argument("--fpv-root", nargs="*", default=[])
    ap.add_argument("--epic-ann", default=None)
    ap.add_argument("--fpv-gt", default=None)
    ap.add_argument("--out", default=None)
    ap.add_argument("--config", default=None, help="Config/eval_config.json (fills the paths above)")
    ap.add_argument("--limit", type=int, default=0, help="max videos per dataset (0 = all available)")
    ap.add_argument("--status", default=None, help="optional JSON file updated with progress (used by the Unity Test Lab)")
    a = ap.parse_args()
    if a.config:
        cp = Path(a.config); c = json.load(open(cp, encoding="utf-8"))
        res = lambda v: str((cp.parent / v).resolve()) if v and not Path(v).is_absolute() else v  # noqa: E731
        a.manifest = a.manifest if a.manifest != str(HERE.parent / "data" / "manifest.csv") else res(c["manifest"])
        a.epic_root = a.epic_root or [res(x) for x in c.get("epic_root", [])]
        a.fpv_root = a.fpv_root or [res(x) for x in c.get("fpv_root", [])]
        a.epic_ann = a.epic_ann or res(c.get("epic_ann")); a.fpv_gt = a.fpv_gt or res(c.get("fpv_gt"))
        a.out = a.out or res(c.get("results_dir", "../Results"))
    if not a.out:
        ap.error("--out or --config required")

    out = Path(a.out)
    (out / "predictions").mkdir(parents=True, exist_ok=True)
    man = load_manifest(Path(a.manifest))
    classes = json.load(open(a.classes, encoding="utf-8"))
    from vistara.pipeline import VistaraPipeline
    cfg = Config()
    pipe = VistaraPipeline(cfg)
    device = str(pipe.enc.device)

    todo = []
    for vid, row in man.items():
        roots = a.epic_root if row["dataset"] == "epic_kitchens_100" else a.fpv_root
        p = find_video(roots, vid) if roots else None
        if p is not None:
            todo.append((vid, row, p))
    if a.limit:
        cnt = defaultdict(int); keep = []
        for t in todo:
            if cnt[t[1]["dataset"]] < a.limit:
                keep.append(t); cnt[t[1]["dataset"]] += 1
        todo = keep
    n_man = {d: sum(1 for r in man.values() if r["dataset"] == d) for d in ("epic_kitchens_100", "fpvsum")}
    print(f"evaluating {len(todo)} of {len(man)} manifest videos present on disk", flush=True)

    epic_ids = {v for v, r, _ in todo if r["dataset"] == "epic_kitchens_100"}
    epic_gt = load_epic_gt(Path(a.epic_ann), epic_ids, classes) if epic_ids and a.epic_ann else {}
    fpv_gt = load_fpv_gt(Path(a.fpv_gt)) if a.fpv_gt and any(r["dataset"] == "fpvsum" for _, r, _ in todo) else {}

    rows, preds, durations, hls = [], {}, {}, {}
    acc = dict(n=0, v=0, nn=0, pr=0, loc=0)
    for i, (vid, row, path) in enumerate(todo):
        t0 = time.time()
        res = pipe.analyse(str(path), video_id=vid)
        wall = time.time() - t0
        o = res["output"]
        errs, _ = validate(o, manifest_duration=float(row["duration_sec"]))
        json.dump(o, open(out / "predictions" / f"{vid}.json", "w", encoding="utf-8"), indent=1)
        hl = [h["segment"] for h in o["highlights"]]
        kept = sum(e - s for s, e in hl)
        r = {"video_id": vid, "dataset": row["dataset"], "duration_sec": o["duration_sec"], "manifest_duration": row["duration_sec"],
             "label_pred": o["label"]["name"], "label_score": o["label"]["score"], "n_actions": len(o["actions"]),
             "n_highlights": len(hl), "highlight_sec": round(kept, 2), "highlight_frac": round(kept / o["duration_sec"], 4),
             "within_15pct_budget": kept <= BUDGET * o["duration_sec"] + 1e-6, "schema_valid": not errs,
             "infer_sec": round(wall, 2), "realtime_factor": round(wall / o["duration_sec"], 3), "device": device}
        if row["dataset"] == "epic_kitchens_100" and vid in epic_gt:
            g = epic_gt[vid]
            n, v, nn, pr, loc = action_accuracy(o["actions"], g)
            for k, x in zip(("n", "v", "nn", "pr", "loc"), (n, v, nn, pr, loc)):
                acc[k] += x
            r.update(gt_actions=n, verb_acc=round(v / max(n, 1), 4), noun_acc=round(nn / max(n, 1), 4), pair_acc=round(pr / max(n, 1), 4),
                     loc_recall_iou0_1=round(loc / max(n, 1), 4),
                     video_action_map=round(evalkit.action_map({vid: o["actions"]}, {vid: g}), 4),
                     video_action_coverage=round(evalkit.coverage({vid: hl}, {vid: g}, {vid: o["duration_sec"]}, BUDGET), 4))
            preds[vid] = o["actions"]; hls[vid] = hl; durations[vid] = o["duration_sec"]
        if row["dataset"] == "fpvsum":
            truth = vid.split("_")[0].lower()
            r["label_true"] = truth; r["label_correct"] = o["label"]["name"] == truth
            if vid in fpv_gt:
                sc = fpv_gt[vid]; n = len(sc)
                k = max(1, int(round(BUDGET * n)))
                gtm = np.zeros(n, bool); gtm[np.argsort(-sc, kind="stable")[:k]] = True
                f1, p, rc = f1_scores(hl_mask(hl, n, o["duration_sec"]), gtm)
                r.update(highlight_f1=round(f1, 4), highlight_precision=round(p, 4), highlight_recall=round(rc, 4))
        rows.append(r)
        print(f"[{i + 1}/{len(todo)}] {vid}: " + ", ".join(f"{k}={r[k]}" for k in ("label_pred", "n_actions", "highlight_frac", "infer_sec")
                                                          if k in r), flush=True)
        if a.status:
            json.dump({"state": "running", "done": i + 1, "total": len(todo), "video": vid}, open(a.status, "w"))

    # ------------------------------------------------------------ aggregate
    summary = {"generated": time.strftime("%Y-%m-%d %H:%M:%S"), "device": device, "engine_config": {
        "sample_fps": cfg.sample_fps, "budget_frac": cfg.budget_frac, "action_window_s": cfg.action_window_s},
        "videos_evaluated": len(rows), "manifest_videos": n_man,
        "coverage_of_manifest": {d: sum(1 for r in rows if r["dataset"] == d) for d in n_man},
        "note": "Local approximations of the official metrics on videos that are NOT used for training. Only the manifest videos "
                "present on this machine were evaluated; the official scorer evaluates a hidden subset."}
    ep = [r for r in rows if r["dataset"] == "epic_kitchens_100" and "gt_actions" in r]
    if ep:
        summary["epic"] = {"videos": len(ep), "gt_actions": acc["n"],
                           "action_map": round(evalkit.action_map(preds, {v: epic_gt[v] for v in preds}), 4),
                           "action_coverage": round(evalkit.coverage(hls, {v: epic_gt[v] for v in hls}, durations, BUDGET), 4),
                           "verb_acc": round(acc["v"] / max(acc["n"], 1), 4), "noun_acc": round(acc["nn"] / max(acc["n"], 1), 4),
                           "pair_acc": round(acc["pr"] / max(acc["n"], 1), 4), "loc_recall_iou0_1": round(acc["loc"] / max(acc["n"], 1), 4),
                           "label_cooking_rate": round(sum(r["label_pred"] == "cooking" for r in ep) / len(ep), 4)}
    fp = [r for r in rows if r["dataset"] == "fpvsum"]
    if fp:
        summary["fpvsum"] = {"videos": len(fp), "label_accuracy": round(sum(r["label_correct"] for r in fp) / len(fp), 4)}
        f1s = [r["highlight_f1"] for r in fp if "highlight_f1" in r]
        summary["fpvsum"]["highlight_f1_videos"] = len(f1s)
        summary["fpvsum"]["highlight_f1"] = round(float(np.mean(f1s)), 4) if f1s else None
        if not f1s:
            summary["fpvsum"]["highlight_f1_note"] = "no ground-truth importance scores available for the evaluated videos"
    if rows:
        summary["all"] = {"schema_valid_rate": round(sum(r["schema_valid"] for r in rows) / len(rows), 4),
                          "within_budget_rate": round(sum(r["within_15pct_budget"] for r in rows) / len(rows), 4),
                          "mean_highlight_frac": round(float(np.mean([r["highlight_frac"] for r in rows])), 4),
                          "mean_infer_sec": round(float(np.mean([r["infer_sec"] for r in rows])), 2),
                          "median_realtime_factor": round(statistics.median(r["realtime_factor"] for r in rows), 3)}
    cols = sorted({k for r in rows for k in r}, key=lambda k: (list(rows[0]).index(k) if k in rows[0] else 99, k)) if rows else []
    with open(out / "eval_per_video.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=cols); w.writeheader(); w.writerows(rows)
    json.dump(summary, open(out / "eval_summary.json", "w", encoding="utf-8"), indent=2)
    if a.status:
        json.dump({"state": "done", "done": len(rows), "total": len(todo)}, open(a.status, "w"))
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
