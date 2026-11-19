# Vistara - Action-Aware Video Summarization (Samsung R&D PRISM 2026, Track 04)

`TEAM_ID = vistara`. One run command, one JSON per video, the same format for every video:

```bash
./run.sh <video.mp4> <out.json>
```

Vistara watches a 2-10 minute first-person video, recognises fine-grained verb-noun actions, scores how important each moment is, labels the video,
and selects <= 15 % highlights (each >= 4.0 s) from the original footage. A Unity VR app shows the result and re-enacts it with a 3D guide.

## Repository layout
```text
run.sh                 <video.mp4> -> <out.json>
requirements.txt       Python dependencies
weights/               trained model (see "Weights")
outputs/               one <video_id>.json for each of the 187 manifest videos
code/
  backend/             the AI engine (Python)
    vistara/           inference package: decode, features, action recognition (AVION + CLIP head), importance, highlights, JSON schema
    server/            FastAPI server the Unity app talks to (port 8765)
    training/          feature caches, priors, head training, AVION export/eval
    evaluation/        held-out evaluation        tests/   unit + edge tests
    validate.py  make_outputs.py  run_manifest.py  data/manifest.csv  data/epic/ (annotations, EPIC downloader)
  Vistara/             Unity 6 (6000.2.7f2) VR project
  assets/              Blender sources (.blend) and exported FBX: category worlds + props, the guide character and her outfits, the star mascot
```

## Setup
Python 3.11+ (developed on 3.14 / Windows 11 / RTX 4060 Laptop). A GPU is recommended; CPU works, slower.
```bash
git lfs install && git lfs pull                 # the CLIP backbone and the AVION weights in weights/
python -m venv .venv
.venv/Scripts/pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128    # or the CPU wheels
.venv/Scripts/pip install -r requirements.txt
./run.sh video.mp4 out.json                     # fully offline: every weight is read from weights/
python code/backend/validate.py --dir outputs --manifest code/backend/data/manifest.csv
```
`TEAM_ID` (env, default `vistara`) sets `team_id`. ffmpeg comes from PATH or the bundled `imageio-ffmpeg`.

## Output format
```json
{ "team_id": "vistara", "video_id": "P01_11", "duration_sec": 512.3,
  "label": {"name": "cooking", "score": 0.93},
  "actions":    [{"segment": [12.4, 18.2], "start": 12.4, "end": 18.2, "verb": "open", "noun": "fridge", "score": 0.88}],
  "highlights": [{"segment": [11.0, 19.5], "start": 11.0, "end": 19.5, "importance": 0.93}] }
```
`label` is `cooking` for EPIC videos and one of the 14 FPVSum categories (or `other`) otherwise. Highlights are chronological, non-overlapping, each >= 4.0 s,
and within 15 % of the duration (videos shorter than ~27 s keep the >= 4 s rule).

## Model
| Stage | Method |
|---|---|
| Decode | 2 fps sampling; Farneback optical flow with dominant-motion subtraction -> hand / object residual motion |
| Label | CLIP ViT-L/14 (OpenAI, frozen), zero-shot prompt ensemble over the 16 official labels |
| Actions (kitchen footage) | **AVION** ViT-L/14 fine-tuned on EPIC-KITCHENS-100 *train* (3806-way verb:noun head; public MIT-licensed checkpoint, converted by `code/backend/training/export_avion.py`), 4 s windows, merged into segments |
| Actions (fallback) | CLIP text similarity + EPIC *train* priors (`pair_prior.npz`) + a trained residual head (`head.pt`) - used for non-EPIC footage or when the AVION weights are absent |
| Importance | activity + temporal change + distinctiveness + action confidence (+ global motion for sport), idle / blur penalties, smoothing |
| Highlights | greedy MMR selection under a hard 15 % budget, >= 4 s segments, temporal spread and pair novelty for kitchen footage |

AVION on 816 held-out segments of 25 EPIC validation videos: verb top-1 75.7 %, noun 64.8 %, action 53.4 % (the CLIP-head fallback: verb ~20 %, noun ~29 %).
Nothing uses video IDs or ground truth at inference; no manifest video was used for training. The AVION weights are a third-party public checkpoint (not trained by us); our own training is the residual head, priors and label prompts (`code/backend/training/`).

## Weights (`weights/`)
`head.pt`, `pair_prior.npz` (small, plain git); `avion_vitl_ek100_fp16.pt` (~590 MB) and the CLIP ViT-L/14 backbone (~1.6 GB) are stored with Git LFS (`.gitattributes`).
Regenerate the AVION file from the public checkpoint with `code/backend/training/export_avion.py`.

## Outputs status
`outputs/` holds 187 valid files (`validate.py`: 0 invalid). **91 are produced by the model; 96 are placeholders** (label `other`, no actions, evenly spaced 4 s highlights)
for videos that could not be downloaded in time; they are listed in `code/backend/outputs_degraded.txt`. The files in `outputs/` were generated **before** AVION was added to the pipeline;
regenerate them with `python code/backend/make_outputs.py --roots <EPIC dir> <FPVSum dir> --redo`.

## VR app (`code/Vistara`)
Open the folder in Unity 6000.2.7f2 (run **Vistara > Build Scenes** once), press Play (XR Interaction Simulator or Quest 3). The app starts the engine from `code/backend` with the weights in `weights/`
(Python from `.venv` at the repo root; set `VISTARA_PYTHON` to use another interpreter).

- **Start**: the spatial shift (the whole 3D space shifts, chromatic/lens glitch, sound) into the app.
- **Category** (15): another spatial shift loads that category's Blender world (`code/assets/categories/<Category>`); the guide **Vee** changes into the category outfit.
- **Pick a video -> ANALYSE**: results theater - output JSON, video with the findings, story/insight bars, key moments.
- **Re-enactment**: Vee acts out, step by step, the action that is highlighted in the video with the category's real props (kitchen: opens the fridge and cupboard, takes items out, chops, stirs, washes, turns the stove on; sports: rides the bike / board / horse / skis, boxes the bag, climbs, vaults ...), with kitchen sounds. The star mascot **Stara** (Llama via [Ollama](https://ollama.com), `ollama pull llama3.2:3b`) talks through the analysis.
- Voice: Vee speaks with the Meta Voice SDK if it is installed, else the headset's text-to-speech (soft voice settings), else the Windows voice in the editor.

Not verified on a physical headset. Details: `code/Vistara/README.md`.

## Assets (`code/assets`)
`categories/<Category>/<Category>.blend` + `export/*.fbx` (every prop and the assembled world) for the 15 categories; `character/` the guide (`Vistara_Girl.blend`, FBX, textures, one outfit FBX per category);
`VStar.blend` the star mascot; `_build/` the Blender generator scripts (run inside Blender). Stylised low-poly. The category worlds, props, outfits and the mascot were made for this project; the guide character is built on a third-party chibi base model (`character/ref`) - check its licence before redistributing.

## Credits
Samsung R&D Institute India PRISM Hackathon 2026; [EPIC-KITCHENS-100](https://epic-kitchens.github.io/2020) (University of Bristol, non-commercial licence); FPVSum (Ho et al., ECCV 2018);
[AVION](https://github.com/zhaoyue-zephyrus/avion) (MIT); OpenAI CLIP via open_clip / timm.
