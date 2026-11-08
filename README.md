# Vistara

**Action-aware egocentric video summarization in XR** - Samsung R&D Institute India PRISM Hackathon 2026, Track 04.

Vistara watches first-person video, finds the moments that matter, and compiles a ~15 % highlight reel from the original footage.
A Unity 6 XR front end (works with the XR Interaction Simulator) talks to a local Python AI engine over HTTP.

## Prototype

Screens: main menu (branding, engine status, recent analyses), in-VR library (+ paste a YouTube link), video workspace,
live AI analysis, results (activities, objects, action timestamps), highlight timeline (importance bars, selected segments,
click to jump) and summary preview (highlight reel, % retained, JSON export).

_Screenshots / demo video: to be added (captured with videos we have the rights to use)._

## How it works

1. **Decode** - frame sampling at 2 fps, optical-flow motion (camera motion removed, hand-zone weighted).
2. **Understand** - pretrained CLIP ViT-B/32 embeddings; zero-shot video label and verb-noun actions over the official 98 verbs / 301 nouns.
3. **Score** - importance from activity, change, distinctiveness and action confidence; idle and repetitive footage is penalised.
4. **Select** - greedy, redundancy-aware selection within the 15 % budget (segments >= 4 s, chronological, non-overlapping).
5. **Export** - official JSON per video (`./run.sh <video.mp4> <out.json>`) and a highlight reel cut from the original footage.

```
Assets/Vistara/   Unity C# front end (UI is built in code; scenes via Vistara > Build Scenes)
Backend/          Python engine, FastAPI server, run.sh, validate.py, submission files
```

## Run it

1. Backend setup (venv, CUDA PyTorch, requirements): see [`Backend/README.md`](Backend/README.md).
2. Open in **Unity 6000.2.7f2**, open `Assets/Scenes/MainMenu.unity`, press **Play** (the engine starts automatically, or run `Backend/start_server.bat`).
3. **Start Analysis** -> pick an MP4 (or copy a YouTube link and press **Paste YouTube link**) -> **Analyze**.

## Status and honest limitations

Zero-shot CLIP is used; it has **not** been scored on EPIC-KITCHENS or FPVSum, so expect modest Action mAP (CLIP is better at objects than verbs).
Importance is a heuristic, not learned from human labels. Details in [`Backend/README.md`](Backend/README.md).
A trained verb-noun head on EPIC-KITCHENS features is the planned next step.

## Credits

- **Challenge:** Samsung R&D Institute India, Bangalore - PRISM Hackathon 2026, Track 04 (student kit, `manifest.csv`, `supported_classes.json`).
- **Datasets:** [EPIC-KITCHENS-100](https://epic-kitchens.github.io/2020) (University of Bristol / University of Catania / University of Toronto; class vocabulary and annotations) and FPVSum (see the kit's [azuxmioy/fpvsum](https://github.com/azuxmioy/fpvsum) and [v-iashin/FPVSum](https://github.com/v-iashin/FPVSum) links).
- **Models and libraries:** CLIP by OpenAI via [OpenCLIP](https://github.com/mlfoundations/open_clip); [PyTorch](https://pytorch.org); [OpenCV](https://opencv.org); [FFmpeg](https://ffmpeg.org) (via imageio-ffmpeg); [FastAPI](https://fastapi.tiangolo.com) / Uvicorn; [yt-dlp](https://github.com/yt-dlp/yt-dlp) for the optional link import.
- **Unity:** Unity 6, XR Interaction Toolkit 3.4, XR Hands, OpenXR, TextMeshPro, the Unity VR template assets, and the Meta XR Simulator.
- **Design inspiration:** VR media-browser and video-control panels and immersive-analytics dashboards (reference images supplied by the team); all Vistara UI, sprites and the neon environment are generated in code.
- **Built with** [Claude Code](https://claude.com/claude-code) assistance.

Please respect the licences of the datasets and of any video you analyse.
