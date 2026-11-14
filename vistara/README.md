# Vistara - Action-Aware Video Summarization (Samsung PRISM 2026, Track 04)

Submission folder (`TEAM_ID` = `vistara`). First-person video in -> verb-noun actions with timestamps, a video category and a <= 15 % highlight reel out, as the official one-JSON-per-video format.

```
vistara/
├── run.sh             # ./run.sh <video.mp4> <out.json>        (single-video inference entry point)
├── code/              # complete inference source (code/vistara/: decode, CLIP, heads, importance, highlight selection, schema)
├── weights/           # trained heads (head.pt, label_head.pt) + the pretrained CLIP backbone cache (offline run)
├── requirements.txt   # exact Python dependencies
├── outputs/           # validated <video_id>.json files - 59 of the 187 manifest videos (see "Outputs status")
└── README.md
```
Extras: `validate.py` (output checker), `run_manifest.py` (batch over the manifest), `evaluation/`, `training/`, `server/` (HTTP API used by the Unity app), `Dockerfile`, `data/manifest.csv`, `outputs_missing.txt`.
The Unity MR front end lives next to this folder in `../unity/VisteraMR`.

## Setup
Python 3.10+ (developed on 3.14, Windows 11). A CUDA GPU is recommended (about 0.06 x real time on an RTX 4060 laptop GPU); CPU works (~4x slower).
```bash
python -m venv .venv && source .venv/bin/activate          # Windows: .venv\Scripts\activate
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128   # or the CPU wheels
pip install -r requirements.txt
```
`weights/` already contains everything the run needs: the trained heads and the pretrained CLIP ViT-B/32 weights, so **no network is used at run time** (`HF_HUB_OFFLINE` is set automatically).
The CLIP backbone is ~600 MB; in git it is stored with Git LFS (`.gitattributes`).

## Run
```bash
export TEAM_ID=vistara                       # optional, default "vistara"
./run.sh /path/to/P02_13.MP4 outputs/P02_13.json
python validate.py --dir outputs/ --manifest data/manifest.csv
# every manifest video present in a folder (loads the model once; skips existing outputs):
python run_manifest.py --videos /path/to/videos [--out outputs] [--team-id ID]
```
Output = the six official fields: `team_id, video_id, duration_sec, label{name,score}, actions[{segment,verb,noun,score}], highlights[{segment}]` (see the student kit `TEAM_README.md`).
The run is deterministic: re-running `./run.sh` on `P02_13` reproduces the shipped `outputs/P02_13.json` (identical JSON).
If analysis ever fails, `run.sh` writes a clearly logged degraded fallback JSON (label `other`, score 0, evenly spaced 4 s segments) instead of an invalid file.
Docker: `docker build -t vistara .` then `docker run --rm --gpus all -v videos:/videos -v out:/out vistara /videos/x.mp4 /out/x.json` (also `... vistara serve` for the Unity app).

## Model architecture
| Stage | Method | Trained? |
|---|---|---|
| Decode + motion | frames at 2 fps; Farneback optical flow (160 px) with dominant-motion subtraction -> hand/object residual motion | no |
| Backbone | CLIP ViT-B/32 (OpenAI weights via open_clip), frozen, fp16 on CUDA | pretrained |
| Window feature | 4 s windows / 2 s hop: mean CLIP embedding + last-first embedding difference + 3 motion cues (1027-d) | - |
| **Action head** `weights/head.pt` | MLP 1027-1024-1024 (dropout 0.5) -> foreground, verb (97), noun (300); blended 50/50 with zero-shot CLIP text similarity | **yes** (EPIC-KITCHENS-100 train videos) |
| Verb-noun pairing | top-6 x top-6 re-ranked with pair prompts, merged over time, boundaries trimmed by activity | no |
| **Label head** `weights/label_head.pt` | linear 512 -> 16 labels, initialised from CLIP text prompts and L2-regularised towards them | **yes** (FPVSum + EPIC-as-"cooking") |
| Importance | activity + change + distinctiveness + action confidence (+ global motion for sport), idle / blur penalties, Gaussian smoothing | heuristic |
| Highlights | greedy MMR over 4/6/8 s windows with redundancy + pair-novelty terms; hard 15 % budget; segments >= 4 s, sorted, non-overlapping | heuristic |

Labels use the official 16 labels / 98 verbs / 301 nouns (`code/vistara/supported_classes.json`); EPIC class id `i` was verified to equal the position of the same key in that list.
Rule: P(cooking) >= 0.35 -> label `cooking`; verb-noun actions are produced whenever P(cooking) >= 0.10 (actions are not scored for FPVSum).

## Training (done once; scripts in `training/`)
* **Data (no manifest video was ever used for training):** EPIC-KITCHENS-100 *train*-split videos that are not in the manifest (45 short videos downloaded, 30 featurised: 25 train + 5 held-out from unseen participants), 37 FPVSum videos that are not in the manifest (+ 3 EPIC clips as "cooking") for the label head. `extract_features.py` / `extract_fpvsum.py` refuse any video listed in `data/manifest.csv`.
* `python training/extract_features.py --videos <EPIC videos> --ann EPIC_100_train.csv --out feats` then `python training/train_head.py --feats feats --out weights/head.pt` (validation split by **participant**, early stopping, shipping rule: only used if it beats zero-shot on held-out videos).
* `python training/extract_fpvsum.py ...` then `python training/train_label.py --fpv feats_fpvsum --epic feats --out weights/label_head.pt`.
* Held-out numbers recorded at training time: action head verb+noun top-1 (sum) 0.146 zero-shot -> 0.381 blended; label head 75.0 % -> 82.5 % (5-fold by video, 40 videos).

## Results (held-out, real ground truth)
`python evaluation/evaluate_heldout.py --epic-root <dir> --fpv-root <dir> --epic-ann EPIC_100_validation.csv --fpv-gt gt_data.mat --out results`
runs the real pipeline on the manifest videos present on disk and scores them against `EPIC_100_validation.csv` and the FPVSum `gt_data.mat`.
2026-11-14, 59 videos (14 EPIC validation + 45 FPVSum), none used for training. The official scorer is not public: these are local approximations of the kit's metrics.

| Metric | Result | Data |
|---|---|---|
| Action mAP (tIoU 0.1-0.5, verb-noun) | **0.022** | 14 EPIC videos, 132 GT actions |
| Action coverage | **0.280** | same |
| Verb / noun / pair top-1 on GT actions | 20.5 % / 17.4 % / 6.1 % | same |
| Label accuracy (FPVSum) | **86.7 %** (39/45) | 45 FPVSum videos |
| Highlight F1 (vs top-15 % human importance) | **0.180** | 44 FPVSum videos with annotations |
| Schema-valid outputs | 59/59 | `validate.py` |
| Within the 15 % budget | 56/59 | the 3 exceptions are the 11 s, 15 s and 25 s videos (one 4 s segment already exceeds 15 %) |
| Inference | mean 11.6 s per video (median 0.06 x real time) | RTX 4060 Laptop GPU |

Honest reading: action recognition is weak (the head was trained on only 30 videos); Highlight F1 0.18 is barely above what a random 15 % selection gives (~0.15) because importance is a heuristic, not learned; label accuracy is the strongest result. Samples are small (14 of 138 EPIC videos), expect high variance.

## Outputs status (important)
`outputs/` holds **59 of the required 187** validated JSONs - exactly the manifest videos that could be processed on the machine used for this submission:
* 14 EPIC-KITCHENS-100 videos (the shortest of the 138; the full set is ~184 GB and was not downloadable here),
* 45 of the 49 FPVSum videos (4 - `Scuba_KD74Ywuy9ng`, `Scuba_Um4kqB8bQBg`, `Scuba__JnCVbCL5Wg`, `Surfing_BKwkLQr9ai8` - could not be downloaded: unavailable/forbidden on YouTube at download time).

`outputs_missing.txt` lists the 128 manifest ids without an output. No placeholder or fake JSON was created for them. To complete the set on a machine that has the videos:
`python run_manifest.py --videos <folder with <video_id>.mp4> && python validate.py --dir outputs/ --manifest data/manifest.csv` (existing outputs are skipped).

## Limitations
Zero-shot-plus-small-head action recognition (low mAP); heuristic importance; label rule fixed by P(cooking); videos < 27 s cannot satisfy both the 4 s segment minimum and the 15 % budget (the >= 4 s rule wins); verb-noun actions are only emitted for kitchen-like video; no GT or video id is used anywhere (code scan: none).

## Credits
Samsung R&D Institute India PRISM Hackathon 2026 Track 04 kit; EPIC-KITCHENS-100 (Damen et al., University of Bristol, non-commercial licence); FPVSum (Ho et al., ECCV 2018); OpenAI CLIP via OpenCLIP; PyTorch, OpenCV, FFmpeg, FastAPI. Built with Claude Code assistance.
