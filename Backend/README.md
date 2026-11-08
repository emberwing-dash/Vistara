# Vistara - Action-Aware Video Summarization (Samsung PRISM 2026, Track 04)

Vistara understands first-person (egocentric) video, localises verb-noun actions, scores how important each moment is,
and compiles a ~15 % highlight reel from the **original footage only**. The AI engine (this folder) is independent of
Unity: it runs through `run.sh` for the competition and through a local HTTP API for the Unity XR front end.

```
./run.sh <video.mp4> <out.json>
```

## Output (follows the student-kit `TEAM_README.md`)

```json
{ "team_id": "vistara", "video_id": "P02_109", "duration_sec": 512.3,
  "label": {"name": "cooking", "score": 0.96},
  "actions": [{"segment": [12.4, 18.2], "verb": "open", "noun": "fridge", "score": 0.88}],
  "highlights": [{"segment": [11.0, 19.5]}] }
```

> The kit slides show a simplified schema (`start`/`end`, string `label`); the kit README is more detailed (and is what
> its `validate.py` checks), so the README schema is implemented. `validate.py` was listed in the kit but missing from
> the provided package, so `Backend/validate.py` re-implements the documented rules (keys, types, ranges, sorted
> non-overlapping highlights, >= 1 segment >= 4 s, 15 % budget warning, manifest duration +/- 0.5 s).

## Setup

```
python -m venv .venv
.venv\Scripts\pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128   # GPU build (CPU also works)
.venv\Scripts\pip install -r requirements.txt
```
Weights: `open_clip` ViT-B/32 (OpenAI, ~600 MB) is cached in `weights/` on first run. Copy `weights/` as-is for offline use.
`TEAM_ID` env var sets `team_id` (default `vistara`). ffmpeg is found on PATH or via the bundled `imageio-ffmpeg`.

Batch over the manifest + validate:
```
for id in $(tail -n +2 data/manifest.csv | cut -d, -f1); do ./run.sh videos/$id.mp4 outputs/$id.json; done
python validate.py --dir outputs/
```

## Architecture

| Stage | Method | Module |
|---|---|---|
| Decode | Sequential `grab()` sampling at 2 fps; Farneback optical flow (160 px) between frames 0.25 s apart; dominant-motion subtraction -> *residual (hand/object) motion*, lower-half weighted | `video_io.py` |
| Understanding | **CLIP ViT-B/32** (pretrained, zero-shot) image embeddings, fp16 on CUDA | `model.py` |
| Video label | Prompt-ensemble zero-shot over the 16 official labels; per-frame posteriors averaged | `recognizer.py` |
| Actions | 4 s windows / 2 s hop; noun & verb distributions over the **official 98 verbs / 301 nouns**; top-6 x top-6 pairs re-ranked with pair prompts; identical consecutive predictions merged; boundaries trimmed by activity | `recognizer.py` |
| Importance | activity (residual motion) + temporal change + distinctiveness + action confidence, idle penalty, blur penalty, Gaussian smoothing. Sports footage swaps in global motion (speed). | `importance.py` |
| Highlights | Greedy MMR over 4/6/8 s windows: importance - visual redundancy + novelty of the (verb,noun) pair, hard 15 % budget (x0.985 safety), >= 4.0 s, chronological, non-overlapping, touching/tiny gaps merged | `highlights.py` |
| Export | ffmpeg cuts of the original video (H.264/AAC) concatenated into the reel | `export.py` |

Speed (RTX 4060 Laptop, 1080p60): ~35 s to analyse a 5-minute video (decode-bound), CLIP encoding < 1 s.

## Local API (used by Unity)

`uvicorn app:app --host 127.0.0.1 --port 8765` in `server/` (or `start_server.bat`):
`GET /health`, `POST /analyze_raw?filename=` (streamed body), `POST /analyze` (multipart), `POST /analyze_path`,
`GET /jobs/{id}` (progress), `/jobs/{id}/result`, `/result_json`, `/highlight`, `GET /history`.

## Honest limitations

* **Zero-shot, not fine-tuned.** No EPIC-KITCHENS / FPVSum videos were available locally (184 GB EPIC download), so the
  system has **not** been scored with the competition metrics. Expect low Action mAP: CLIP separates objects far better
  than verbs, and sees single frames, not true motion dynamics.
* Verb-noun scores are CLIP-derived pseudo-probabilities (calibrated into [0,1] but not a trained classifier).
  Class priors come from the order of the official class list (EPIC keys are frequency ordered); a small hand-written
  penalty down-weights visually ambiguous nouns / abstract verbs. No video IDs or ground truth are used anywhere.
* Importance is a heuristic proxy for human importance (motion, change, distinctiveness), not trained on FPVSum labels.
* Actions are only emitted for videos classified as `cooking`; for the 14 FPVSum categories `actions` is `[]` (allowed by the schema).
* The label decision uses a fixed rule: P(cooking) >= 0.35 -> `cooking` (EPIC is all kitchen footage, FPVSum has no cooking class).
* Very short videos (< ~27 s) cannot satisfy both "<= 15 %" and ">= 4 s"; one 4 s segment is emitted.
* Best next step with more time/data: fine-tune a verb/noun head (e.g. on EPIC features) and learn importance from FPVSum.
