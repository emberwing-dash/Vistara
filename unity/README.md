# VisteraMR - Vistara in Mixed Reality (Samsung PRISM 2026, Track 04)

Unity 6 (6000.2.7f2) MR project: the **Vistara interface** (ported from the `Vistara` project) running on the MR template rig, connected to the
**trained action-aware video summarisation model** in `Assets/Model/`. The original MR template scene (`Assets/Scenes/SampleScene.unity`) is untouched.

![Test Lab](../../docs/screenshots/05-test-lab.png)

## What is in the project
| Path | Content |
|---|---|
| `Assets/Model/` | trained model: `Weights/` (head.pt, label_head.pt), `Config/`, `Backbone~/` (pretrained CLIP), `Engine~/` (Python inference engine, server, evaluation), `Results/` (evaluation outputs), `README_MODEL.md` (architecture, I/O format, requirements) |
| `Assets/Vistara/Scripts/` | the Vistara UI (code-built world-space UI: main menu, library, player, analysis, timeline, summary) **plus the new Test Lab tab**, backend client / launcher, smoke test |
| `Assets/Vistara/Editor/` | `VistaraMRSceneBuilder` (menu **Vistara > Build MR Scenes**), smoke launcher |
| `Assets/Scenes/` | `MainMenu.unity`, `Workspace.unity` (new, MR rig from `MR Interaction Setup`), `SampleScene.unity` (original template) |

## Run it
1. **Python engine** (once): create a venv and install `Assets/Model/Engine~/requirements.txt` (torch with CUDA recommended). Unity finds Python via env `VISTARA_PYTHON`,
   `Assets/Model/Engine~/.venv`, the sibling `../Vistara/Backend/.venv`, then `python` on PATH. The engine is started automatically by `BackendLauncher` (port 8765) and stopped on quit.
2. Open the project in **Unity 6000.2.7f2**, open `Assets/Scenes/MainMenu.unity` (rebuild with **Vistara > Build MR Scenes** if needed), press **Play**
   (XR Simulator / XR Interaction Simulator drive the headset and controllers).
3. **START** -> pick a category -> pick a video -> **ANALYSE** -> **ENTER MR HUD** (hand-controlled flow, see below); the previous Workspace (library, timeline, summary, link import) and the **Test Lab** stay available from the menu.
4. Docker alternative for the engine: `docker run --rm --gpus all -p 127.0.0.1:8765:8765 vistara serve` (see `Assets/Model/Engine~/Dockerfile`).

## Hand-controlled MR flow (Journey scene)
`START` (main menu, pinch) -> **category carousel** -> **video selection + preview** -> **ANALYSE** (real model) -> **results** -> **floating MR HUD**.
Scene: `Assets/Scenes/Journey.unity` (rebuild with **Vistara > Build MR Scenes**); code in `Assets/Vistara/Scripts/Journey` and `Assets/Vistara/Scripts/Hands`.

| Step | What it does |
|---|---|
| Start | `MainMenu` scene, big **START** button (pinch-selectable world-space UI). |
| Category carousel | 15 categories derived from `manifest.csv` video ids: **Kitchen** (EPIC-KITCHENS-100, 138 videos) + 14 FPVSum categories parsed from the `<Category>_<YouTubeID>` ids (Bike, BikePolo, Boxing, Horse, Jumping, Longboarding, Motor, Parkour, Plane, RockClimbing, Scuba, Skate, Ski, Surfing). Cards show a real thumbnail (a frame of a local video, rendered by the engine), the manifest video count and how many are available on this PC. Browse by hand swipe, pinch-drag, or the arrow buttons; pinch a card to select. Engine: `GET /categories`, `/thumbnail/category/{id}`. |
| Video selection | (Clips that Unity's player cannot prepare, e.g. the video-only FPVSum downloads, are played from a transcoded copy served by the engine: `GET /playable`; the analysis always uses the original file.) Lists the category's videos available locally (thumbnail, duration, size), **Browse files...** for your own file, a preview player (pinch play / scrub) and **ANALYSE**. A manifest video is analysed from disk; your own file is uploaded to the local engine and the model decides its category itself (the result screen says whether it matches the category you picked). |
| AI analysis | `POST /analyze_path` or `/analyze_raw` -> the real trained model (CLIP + trained heads + importance + highlight selection). Progress per stage. No mock data. |
| Results | verb-noun actions with timestamps and confidence, detected objects and activities, highlight segments and duration vs the 15 % budget. |
| MR HUD | Floating panel in front of you: video with a live caption of the action at the current time (verb noun + confidence), NOW / NEXT cards, **contextual guidelines**, highlight timeline (importance curve, orange highlights, action ticks, playhead), hand playback controls and **Skim highlights**. |

**Guidelines are derived from the predictions:** the headline, confidence, time range, next action, importance value and "inside a highlight" flag all come from the model output at the current playback time.
Only the advice sentence is a fixed template keyed by the predicted verb (kitchen) or predicted category (sport), see `GuidanceBuilder.cs`.

### Hand tracking
* **Stack:** XR Interaction Toolkit 3.4.1, OpenXR 1.16.1 (Android features enabled: *Hand Tracking Subsystem*, *Meta Hand Tracking Aim*, *Meta Quest Support*), XR Hands 1.7.3, the MR template rig (`MR Interaction Setup` with hand interactors).
* **Pinch selection** = XRI hand interactors driving the `XRUIInputModule` on world-space canvases (every button / card is a UI `Button`).
* **Swipes** = `HandSwipeDetector` (XR Hands palm joint velocity, not pinching, >= 22 cm at >= 0.9 m/s lateral): carousel +/-2 cards; in the HUD +/-10 s (previous / next highlight while skimming).
* **Pinch-drag**: carousel (ScrollRect), HUD grip bar (`DragHandle` moves the panel along the pointer ray), HUD timeline (`PointerScrub`).
* **Hand-controlled playback**: play/pause, -10/+10 s, previous/next highlight, skim highlights, scrub, swipe-seek.
* **Hands only:** `HandOnlyMode` removes the controller objects from the `XRInputModalityManager` on device builds (in the editor the simulator keeps its controller mode available).

### XR Simulator testing (done) vs Quest 3 (to do)
Verified in the editor through the XR Simulator rig (`-vistaraJourney` smoke test, screenshots in `../../docs/screenshots/`): START -> carousel with 15 real thumbnails and counts -> swipe event -> category -> video list + preview -> real analysis -> results -> HUD with live captions / guidance / timeline -> HUD swipe-seek.
The test drives the **same handlers** the pinch buttons call; it injects the swipe event.

**Requires a physical Quest 3 (not verified):**
1. Real **pinch selection** on the world-space UI through the hand interactors (ray origin, pinch threshold, accidental pinch on the thumb) - in the editor use the Interaction Simulator's Hand mode or the mouse.
2. **Swipe thresholds** (`minSpeed`, `minDistance`) of `HandSwipeDetector` against real hand-tracking data; false positives while reaching for a panel.
3. **Pinch-drag** of the carousel, the HUD grip bar and the timeline with hand rays (needs a head-locked comfort check at 1.45 m).
4. **Hands-only mode** on device (`HandOnlyMode`, controllers removed) and the Android permissions / hand-tracking manifest entries of an Android build.
5. Readability of the text at headset resolution, passthrough blending of the HUD, and the performance of the world-space canvases.
6. The AI engine does not run on the headset: run it on a PC (or Docker) reachable from the Quest and set `BackendClient.BaseUrl` to its LAN address (engine bound with `--host 0.0.0.0`).

## Test Lab (real model, held-out videos)
* Lists the manifest videos present on this machine (EPIC-KITCHENS-100 validation + FPVSum). Select one and **RUN TRAINED MODEL**: the engine runs the real pipeline and the tab shows
  category + confidence, highlight segments, **highlight duration vs original with the 15 % budget bar**, verb-noun actions with timestamps and confidence, inference time and device.
  Tap a segment or action to jump to it in the Timeline (video playback with highlight segments).
* **RUN HELD-OUT EVALUATION** re-runs the evaluation over all test videos (progress bar), then shows the metrics; with a video selected it also shows that video's ground-truth comparison.
* Model panel: weights file hashes and the held-out numbers recorded at training time.
* Repeatable from the command line: `Assets/Model/run_evaluation.bat` (or `.sh`; `--limit N` for a quick run). Data paths: `Assets/Model/Config/eval_config.json`.

## Measured results (held-out, real ground truth, `Assets/Model/Results/`)
Run: 2026-11-14, RTX 4060 Laptop GPU, 59 videos, none used for training. Metric definitions follow `TEAM_README.md`; the official scorer is not public, so these are local approximations.

| Metric | Value | Data |
|---|---|---|
| Action mAP (tIoU 0.1-0.5, verb-noun) | **0.022** | 14 EPIC validation videos, 132 GT actions |
| Action coverage | **0.280** | same |
| Verb / noun / pair top-1 on GT actions | **20.5 % / 17.4 % / 6.1 %** | same (best-scoring overlapping prediction) |
| Temporal localisation recall (tIoU >= 0.1) | 99.2 % | same (many overlapping predictions, so this is lenient) |
| Label accuracy (FPVSum) | **86.7 %** (39/45) | 45 FPVSum manifest videos |
| Highlight F1 (FPVSum, vs top-15 % human importance) | **0.180** | 44 videos with annotations |
| Outputs valid against the official schema | 100 % (59/59, `validate.py`) | all |
| Highlights within the 15 % budget | 94.9 % (56/59) | the 3 exceptions are 11 s, 15 s and 25 s videos |
| Mean fraction kept | 15.1 % | all |
| Inference time | mean 11.6 s per video, median 0.06 x real time | RTX 4060 laptop GPU (model already loaded) |

How to read these honestly
* **Action recognition is weak.** The verb-noun head beats zero-shot CLIP (see `README_MODEL.md`) but the absolute accuracy is low, which is why mAP is 0.022. It was trained on only 30 EPIC videos.
* **Highlight F1 0.18 is barely above chance** (a random 15 % selection scores about 0.15). Importance is a heuristic, not learned from FPVSum labels.
* **Label accuracy 86.7 %** is the strongest result; the 6 errors are 3 Plane videos (labelled motor/jumping), 2 Bike videos (motor, bikepolo) and 1 Skate video (longboarding).
* Only 14 of the 138 EPIC validation videos (the shortest, 11-55 s) could be evaluated (the full 138-video set is ~184 GB); 45 of the 49 FPVSum videos were downloadable. These are small samples, so expect large variance.
* The manifest contains videos shorter than 27 s (11.1 s, 15.3 s, 25.2 s ...), for which one >= 4 s segment already exceeds 15 %; the engine keeps the >= 4 s rule, hence the 3 over-budget cases.
* Not computed: nothing was invented; the importance-ranking quality is only measured through Highlight F1, and redundancy removal has no ground-truth metric in the kit (it is part of the coverage score).

## Unity verification
* Compiles without errors (batch build of the scenes: no `error CS`).
* Automated play-mode smoke test through the XR Simulator, real engine, real model: `Unity.exe -projectPath VisteraMR -vistaraSmoke <outDir> -vistaraVideo <mp4> -vistaraTestLab`
  (screenshots in `../../docs/screenshots/`).
* Not verified: controller-ray clicking of the buttons (the smoke test calls the same handlers), a device build for Android XR / Quest, and the engine running on the headset itself (it runs on a PC or in Docker).

## Credits
Samsung R&D Institute India PRISM Hackathon 2026 (Track 04 kit), EPIC-KITCHENS-100 (Damen et al., University of Bristol; non-commercial licence), FPVSum (Ho et al., ECCV 2018),
OpenAI CLIP via OpenCLIP, PyTorch, OpenCV, FFmpeg, FastAPI, Unity XR Interaction Toolkit and the Unity MR template.
