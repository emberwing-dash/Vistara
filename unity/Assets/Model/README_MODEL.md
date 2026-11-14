# Vistara trained model (Samsung PRISM 2026, Track 04)

Everything the application needs to run the trained model lives in this folder. Training datasets are **not** copied here.

```
Assets/Model/
  Weights/            head.pt (verb-noun action head), label_head.pt (video label head), weights_manifest.json (SHA-256)
  Config/             supported_classes.json (16 labels / 98 verbs / 301 nouns), model_config.json (architecture + preprocessing),
                      eval_config.json (evaluation data paths), manifest.csv (official 187-video list)
  Backbone~/          pretrained CLIP ViT-B/32 (OpenAI) cache - not trained by us, hidden from Unity's importer (the "~")
  Engine~/            Python inference engine: code/ (pipeline), server/ (FastAPI), evaluation/, training/ (reference), run.sh, Dockerfile
  Results/            eval_summary.json, eval_per_video.csv, predictions/<video_id>.json (written by the evaluation)
  run_evaluation.bat / .sh   repeatable held-out evaluation
```

The weights are byte-identical to the trained originals (`Vistara/Backend/weights`, `E:\samsung_temp\TRAIN\out`); see `Weights/weights_manifest.json`.
Nothing was retrained for the Unity integration.

## What the model does
Input: one first-person video (2-10 min). Output: the official Track-04 JSON (`team_id, video_id, duration_sec, label, actions[], highlights[]`)
plus extra analysis data for the UI (importance curve, timings). Highlights add up to <= 15 % of the video, segments >= 4 s, sorted, non-overlapping.

## Architecture
| Stage | Method | Trained? |
|---|---|---|
| Decode + motion | frames sampled at 2 fps; Farneback optical flow (160 px) with dominant-motion subtraction -> hand/object residual motion | no |
| Backbone | CLIP ViT-B/32 (OpenAI weights, open_clip), frozen, fp16 on CUDA | pretrained only |
| Window feature (1027 d) | mean CLIP embedding (512) + last-first embedding difference (512) + 3 motion cues, 4 s windows / 2 s hop | - |
| **Action head** `head.pt` | MLP 1027-1024-1024 (GELU, dropout 0.5) -> foreground (1), verb (97), noun (300); blended 50/50 with zero-shot CLIP text similarity | **yes, EPIC-KITCHENS-100 train split** |
| Verb-noun pairing | top-6 verbs x top-6 nouns re-ranked with pair prompts, merged over time | no |
| **Label head** `label_head.pt` | linear 512 -> 16, initialised from CLIP text prompts, L2-regularised towards them; per-frame posterior averaged over the video | **yes, FPVSum (non-manifest) + EPIC as "cooking"** |
| Importance | activity + change + distinctiveness + action confidence (+ global motion for sport), idle / blur penalties, Gaussian smoothing | heuristic |
| Highlights | greedy MMR over 4/6/8 s windows with redundancy + pair-novelty terms, hard 15 % budget | heuristic |

## Input / output format
* Preprocessing (identical in training and inference, code shared): 224x224 RGB frames, CLIP mean `[0.481, 0.458, 0.408]` / std `[0.269, 0.261, 0.276]`.
* Label ids: `Config/supported_classes.json`. EPIC class id `i` <-> `verbs[i]` / `nouns[i]` was verified against `EPIC_100_verb_classes.csv` / `EPIC_100_noun_classes.csv` (0 mismatches).
* Output JSON: see `examples/` of the student kit; validated by `Engine~/validate.py` and at run time by `vistara/schema.py`.

## Inference approach in Unity (and why not ONNX)
The pipeline needs video decoding, optical flow and ffmpeg highlight rendering, none of which exist inside Unity, and a Sentis/ONNX port of only
the CLIP + heads would leave the rest in C#. The original PyTorch weights are therefore used **unchanged** by the Python engine in `Engine~/`,
which Unity starts (`BackendLauncher`) and talks to over local HTTP (`BackendClient`, JSON). Requirements: Python 3.10+, `Engine~/requirements.txt`
(torch, open_clip_torch, opencv-python-headless, fastapi, uvicorn, numpy, imageio-ffmpeg), a CUDA GPU is recommended (CPU works, ~4x slower).
Python is located via `VISTARA_PYTHON`, `Engine~/.venv`, the sibling `Vistara/Backend/.venv`, then `python` on PATH. For a standalone headset build the engine
must run on a PC (or the provided Docker image) reachable at `BackendClient.BaseUrl`.

## Training (done once, outside Unity)
`Engine~/training/` holds the scripts used: `extract_features.py` + `train_head.py` (EPIC train videos only; manifest videos are refused),
`extract_fpvsum.py` + `train_label.py`. Held-out validation split by **participant** (unseen kitchens). Shipping rule: a head is used only if it beats the
zero-shot baseline on held-out videos.

## Evaluation (held-out, real ground truth)
Run `run_evaluation.bat` (or the **Run held-out evaluation** button in the Test Lab). Videos: the manifest videos present on this machine
(EPIC-KITCHENS-100 validation split + FPVSum), none of which were used for training. Ground truth: `EPIC_100_validation.csv` and FPVSum `gt_data.mat`.
Outputs: `Results/eval_summary.json`, `Results/eval_per_video.csv`, `Results/predictions/*.json` (official schema).
Metric definitions and limits are documented at the top of `Engine~/evaluation/evaluate_heldout.py`. The official scorer is not public, so these are local approximations.
