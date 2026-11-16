# Vistara - Action-Aware Video Summarization (Samsung R&D PRISM 2026, Track 04)

`TEAM_ID = vistara`. Vistara watches a 2-10 minute first-person video, recognises fine-grained verb-noun actions, scores how important each
moment is, and selects <= 15 % highlights (each >= 4.0 s) taken from the original footage. It writes one standardised JSON per video.
A hand-controlled Unity mixed-reality app (Meta Quest 3) wraps the same engine.

```
./run.sh <video.mp4> <out.json>
```

## Submission layout (as required by the Student Package Guide)
```text
run.sh                  single-video inference entry point
code/
  vistara/              complete inference source (decode, CLIP features, recognition, importance, highlights, schema)
  VisteraMR/            Unity 6 (6000.2.7f2) MR project - the hand-controlled Track 04 app
weights/                the trained model: head.pt (residual action head), pair_prior.npz (data priors),
                        CLIP ViT-L/14 backbone (models--timm--vit_large_patch14_clip_224.openai, Git LFS, ~1.6 GB)
requirements.txt        exact Python dependencies
outputs/                187 validated <video_id>.json files (one per manifest video) - see "Outputs status"
README.md               this file
server/ training/ evaluation/ tests/ data/ docs/   engine HTTP API for Unity, training + evaluation scripts, self-tests, manifest + class list, demo notes
```

## Environment setup
Python 3.11+ (developed on 3.14, Windows 11, RTX 4060 Laptop). A GPU is recommended; CPU works but is slower.
```bash
python -m venv .venv
.venv/Scripts/pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128   # or the CPU wheels
.venv/Scripts/pip install -r requirements.txt
git lfs pull                       # fetches the ~1.6 GB CLIP backbone in weights/
./run.sh video.mp4 out.json        # offline: all weights are read from weights/ (no network access needed)
```
`TEAM_ID` (env, default `vistara`) sets `team_id`. ffmpeg is taken from PATH or the bundled `imageio-ffmpeg`.
Batch over the manifest and validate: `python run_manifest.py --videos <dir>` then `python validate.py --dir outputs/ --manifest data/manifest.csv`.
Docker: `docker build -t vistara .` then `docker run --rm --gpus all -v <videos>:/videos -v <out>:/out vistara /videos/x.mp4 /out/x.json`.

## Output format
Exactly the 6 fields of the kit (`team_id, video_id, duration_sec, label{name,score}, actions[{segment,verb,noun,score}], highlights[{segment}]`).
`label` is `cooking` for EPIC and one of the 14 FPVSum categories (or `other`) otherwise; highlights are chronological, non-overlapping, >= 4.0 s,
and kept under 15 % of the duration. `validate.py` (a re-implementation of the kit's rules) passes on all 187 files.

## Model architecture
| Stage | Method |
|---|---|
| Decode | 2 fps sampling; Farneback optical flow (160 px) with dominant-motion subtraction -> hand/object residual motion |
| Backbone | **CLIP ViT-L/14** (OpenAI weights, QuickGELU, open_clip), frozen, fp16 on CUDA |
| Video label | zero-shot prompt ensemble over the 16 official labels; per-frame posteriors averaged |
| Actions | 4 s windows / 2 s hop; verb (97) and noun (300) distributions = CLIP text similarity + **data priors** (verb / noun / pair frequencies from the public EPIC-100 *train* annotations, `pair_prior.npz`) + a **trained residual head** (`head.pt`, MLP on window features, trained on EPIC *train* videos, validated on held-out participants); top verbs x nouns re-ranked with pair prompts and merged |
| Importance | activity + temporal change + distinctiveness + action confidence (+ global motion for sport), idle / blur penalties, smoothing |
| Highlights | greedy MMR selection with a hard 15 % budget (x0.985 safety), >= 4 s; temporal spread and pair novelty for kitchen footage (coverage), general policy for sport |

Nothing uses video IDs or ground truth at inference; training never touched manifest videos (feature extraction refuses them).
Retraining: `training/build_cache.py` (features) -> `training/build_priors.py` -> `training/windows_from_cache.py` -> `training/train_head2.py`.

## Measured results (local approximation of the kit metrics, real video decode)
Only the manifest videos that could be downloaded were evaluated (14 of 138 EPIC, 45 of 49 FPVSum); the official scorer runs on a hidden subset.

| Metric (weight) | Local result |
|---|---|
| Action mAP (25 %) | 0.091 (14 EPIC videos) |
| Action coverage (40 %) | 0.294 (14 EPIC videos; random selection scored ~0.30 on the same short clips) |
| Highlight F1 (25 %) | 0.223 (44 FPVSum videos) |
| Label accuracy (10 %) | 91.1 % (45 FPVSum videos) |
| Schema valid / within 15 % budget | 100 % / 94.9 % |
| Speed | ~5 s per video, ~0.03 x real time (RTX 4060 Laptop) |

Honest notes: action recognition is weak (verb top-1 ~20 %, noun top-1 ~29 %); the oracle ceiling for coverage on the 138 EPIC videos is ~0.75, so
high accuracy on the action metrics is not reachable with this approach. Importance is a heuristic - there are no human importance labels outside the scored set to train on.
Videos shorter than ~27 s cannot satisfy both "<= 15 %" and ">= 4 s"; they keep the >= 4 s rule.

## Outputs status
`outputs/` holds 187 schema-valid JSON files. **59 are real model outputs** (14 EPIC + 45 FPVSum). **128 are degraded placeholders** (label `other`, score 0,
no actions, evenly spaced 4 s highlights) because their source videos could not be downloaded (the EPIC server data.bris was unreachable; 124 EPIC + 4 FPVSum).
They exist only so no file is missing; they are not model inference and are listed in `outputs_degraded.txt`. Regenerate them once the videos are available:
`python make_outputs.py --roots <EPIC dir> <FPVSum dir> --redo`.

## The MR app (code/VisteraMR)
START -> 15-category carousel -> video selection + preview -> ANALYSE (real model) -> a "do it yourself" coach that walks through the steps the model found.
Each step plays its clip once with overlay signs (what the model sees, tappable runner-up readings, key-moment frame colour, tap-to-pause, tappable step strip),
then pauses for **your turn** and moves on. Panels are grabbed with a pinch on the side grips or the bottom move bar and follow your hand in 3D.
Open `code/VisteraMR` in Unity 6000.2.7f2; the engine (`server/`, `code/vistara`, `weights/`) is found automatically at the repo root and started on port 8765
(Python from `VISTARA_PYTHON`, `./.venv` or PATH). On the headset the engine runs on a PC and its LAN address goes in `engine_url.txt`.
Details: [`code/VisteraMR/README.md`](code/VisteraMR/README.md). Physical Quest 3 verification of hand input is still to be done.

## Credits and licences
Samsung R&D Institute India PRISM Hackathon 2026 (Track 04 kit); [EPIC-KITCHENS-100](https://epic-kitchens.github.io/2020) (University of Bristol, non-commercial licence);
FPVSum (Ho et al., ECCV 2018); CLIP by OpenAI via [OpenCLIP](https://github.com/mlfoundations/open_clip); PyTorch, OpenCV, FFmpeg, FastAPI; Unity 6, XR Interaction Toolkit, XR Hands, OpenXR.
Built with Claude assistance. Respect the dataset licences for any video you analyse.
