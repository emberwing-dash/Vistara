# Vistara - demo video script and presentation outline (Track 04)

Rules from the PPT (slides 22, 24, 25): final submission closes **18 Nov 2026, 11:59 PM**, no extension. Deliverables: public/shared
GitHub repo, README with reproducible setup + Docker files, **demo video (max 3 min; the timeline slide says "5-min" - target 3:00 to be safe)**,
a presentation named `CollegeName_TeamName`, one Google-Form submission per team. Teams not following the guidelines are disqualified.

## 1. Footage you may use
Use only footage you have the rights to: EPIC-KITCHENS clips (non-commercial licence, credit "EPIC-KITCHENS-100, Damen et al."),
public / Creative-Commons YouTube clips (the app's link import) and your own recordings. Do not show personal videos.
Pre-run the analyses before recording (so you never wait on screen) and keep two results ready: one kitchen clip, one sports clip.

## 2. Demo video - 3:00 shot list

| Time | On screen | Narration (draft) |
|---|---|---|
| 0:00-0:12 | Main menu, title VISTARA, rings turning | "First-person video is hours of footage and thousands of visual tokens. Vistara finds what matters and keeps only that." |
| 0:12-0:30 | Slide or menu: pipeline strip (decode, understand, score, select, export) | "Input: a 2-10 minute egocentric video. Output: about 15 percent highlights from the original footage, verb-noun actions with timestamps, a video category, and one JSON per video." |
| 0:30-0:50 | Library -> pick the kitchen clip (or Paste YouTube link) -> Player tab | "Import a video from the in-VR library, or paste a link. Vistara works in XR, driven here with the XR simulator." |
| 0:50-1:20 | Press Analyze; Analysis tab: progress, stages | "The AI engine runs locally: motion from optical flow, CLIP scene understanding, then action and importance scoring. About thirty seconds for a five-minute video on a laptop GPU." |
| 1:20-1:50 | Results: activities, objects, action timestamps list; click one row | "It recognises verb-noun actions from the official vocabulary and localises them in time. Click any action to jump to it." |
| 1:50-2:25 | Timeline tab: importance bars, orange segments, click to seek | "Each bar is importance. Idle and repetitive footage scores low; distinctive hand-object interactions score high. Orange is what we keep, within the 15 percent budget, segments of at least four seconds, in order, no overlap." |
| 2:25-2:45 | Select a segment -> More like this / Drop this; highlights update | "Live feedback: tell it more like this or drop this and it re-weights importance and re-selects instantly - personal highlights with no labels." |
| 2:45-3:00 | Summary tab: reel playing, retained %, Export JSON | "The reel is cut from the original footage. Export the official JSON, or run the same engine with run.sh for the competition. Vistara: keep what matters." |

Recording tips: OBS at 1080p; hide the desktop and taskbar; keep the Game view large; speak over the footage afterwards; show the
`./run.sh video.mp4 out.json` + `validate.py` line for 3 seconds only if time allows (cut the 0:12 slide instead).

## 3. Presentation outline (file name `CollegeName_TeamName.pptx` or PDF)
The kit lists what it must cover: theme ID, title, team; problem; solution + architecture diagram; tools; innovation; results; limitations.

1. **Title** - Vistara: Action-Aware Egocentric Video Summarization in XR. Theme: Track 04. Team name, college, members.
2. **Problem (own words)** - long first-person video, mostly idle or repetitive; XR needs fewer, better frames. Goal: ~15 % highlights, verb-noun actions, category, JSON.
3. **Solution overview** - one sentence + screenshot of the Timeline tab (take it from a rights-clean clip).
4. **Architecture diagram** - Unity XR app <-> local HTTP (FastAPI) <-> Python engine: decode/flow -> CLIP -> actions -> importance -> selection -> JSON + reel. Same engine behind `run.sh`.
5. **AI pipeline detail** - sampling 2 fps; hand-zone residual motion; zero-shot CLIP on the official 98 verbs / 301 nouns; importance = activity + change + distinctiveness + action confidence; MMR selection with redundancy and pair-novelty terms; hard budget.
6. **Tech stack** - Unity 6 + XRI 3.4 + TextMeshPro; Python, PyTorch, OpenCV, FFmpeg, FastAPI; OpenCLIP ViT-B/32; yt-dlp (optional import).
7. **Innovation highlights** - XR front end with interactive importance timeline; live feedback ("more like this / drop this") adapting per video; action-aware redundancy removal; offline, deterministic `run.sh`; degraded-mode safety net.
8. **Results** - *fill with measured numbers only*: speed (about 30 s / 5-min video on RTX 4060; 10-min video 28 s), 15 % budget compliance, schema validity, deterministic. Add Action mAP / coverage / label accuracy **only after** the local EPIC evaluation exists (`evalkit.py` + trained head); until then state "not yet scored on EPIC/FPVSum".
9. **Limitations (honest)** - zero-shot CLIP is weak on verbs; importance is heuristic; not trained or scored on real data yet; <27 s videos cannot meet both rules; label uses a fixed P(cooking) rule.
10. **Roadmap and thanks** - train verb-noun head on EPIC features; learn importance from FPVSum; credits (Samsung PRISM, EPIC-KITCHENS, OpenCLIP, Unity).

## 4. Checklist before 18 Nov
- [ ] EPIC train videos downloaded, head trained, local mAP/coverage measured (replace slide 8 placeholder)
- [ ] 187 JSONs in `outputs/`, `python validate.py --dir outputs/` clean; folder named with your TEAM_ID
- [ ] Docker image builds (start Docker Desktop, `docker build -t vistara .` in `Backend/`)
- [ ] Repo public/shared, README with setup + Docker, no personal videos
- [ ] Demo video <= 3 min uploaded (YouTube/Drive link), deck named `CollegeName_TeamName`
- [ ] Single Google-Form submission by the team (Final Submission link is sent only to registered teams)
