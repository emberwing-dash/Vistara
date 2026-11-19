"""Class and pair priors from the PUBLIC EPIC-KITCHENS-100 TRAIN annotations (EPIC_100_train.csv; no videos needed).

  python training/build_priors.py --ann EPIC_100_train.csv --out weights/pair_prior.npz [--exclude P01_01,P02_03,...]

verb_logp[97], noun_logp[300], pair_logp[97,300] (log P(verb,noun) with additive smoothing backed off to the product of marginals),
verb_len[97] mean action length in seconds. The manifest (scored) videos are never part of the train split; --exclude removes extra videos
(used to keep an experiment unbiased when it is evaluated on videos that are also in the train split).
"""
import argparse, csv, json
from pathlib import Path
import numpy as np


def ts(t):
    h, m, s = t.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ann", required=True); ap.add_argument("--out", required=True)
    ap.add_argument("--exclude", default=""); ap.add_argument("--alpha", type=float, default=2.0)
    a = ap.parse_args()
    ex = set(x for x in a.exclude.split(",") if x)
    man = {r["video_id"] for r in csv.DictReader(open(Path(__file__).resolve().parents[1] / "data" / "manifest.csv", encoding="utf-8"))}
    nv, nn = 97, 300
    C = np.zeros((nv, nn)); L = np.zeros(nv); cnt = np.zeros(nv); used = 0
    for r in csv.DictReader(open(a.ann, encoding="utf-8")):
        if r["video_id"] in ex or r["video_id"] in man:      # never use a scored video
            continue
        v, n = int(r["verb_class"]), int(r["noun_class"])
        if v < nv and n < nn:
            C[v, n] += 1; L[v] += ts(r["stop_timestamp"]) - ts(r["start_timestamp"]); cnt[v] += 1; used += 1
    pv = (C.sum(1) + 1) / (C.sum() + nv); pn = (C.sum(0) + 1) / (C.sum() + nn)
    P = (C + a.alpha * nv * nn * np.outer(pv, pn) / (nv * nn)) / (C.sum() + a.alpha)         # smoothed joint, backs off to the marginals
    P /= P.sum()
    np.savez(a.out, verb_logp=np.log(pv), noun_logp=np.log(pn), pair_logp=np.log(P), verb_len=L / np.maximum(cnt, 1), n_actions=used)
    print("actions used:", used, "| distinct pairs seen:", int((C > 0).sum()), "| out:", a.out)


if __name__ == "__main__":
    main()
