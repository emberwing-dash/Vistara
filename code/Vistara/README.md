# Vistara - Samsung PRISM 2026, Track 04 (Action-Aware Video Summarization)

VR front end (Unity 6, XRI 3.4, XR Hands, Meta Quest 3 / XR Interaction Simulator) on top of a Python AI engine, plus a Llama-powered mascot.

```
Vistara/
  Assets/Vistara/      Unity C# (scenes built by Vistara > Build Scenes)
    Scripts/Journey    category carousel -> video selection -> ANALYSE -> summary viewer
    Scripts/Hands      direct hand input (poke = tap, pinch = press / drag), swipe detector, drag handles
    Scripts/Mascot     MascotAssistant: the mascot model + chat with Meta Llama (Ollama)
    Resources/Mascot   VStar.fbx (rigged jelly star + 6 animations), JellyStar.shader, MascotToon.shader (URP)
  Backend/             Python engine + submission files (run.sh, code/, weights/, requirements.txt, outputs/, README.md)
```

## Run the prototype
1. One-time backend setup: see `Backend/README.md` (venv with torch + requirements). The trained model is in `Backend/weights`.
2. Open the project in Unity 6000.2.7f2, run the menu **Vistara > Build Scenes** once (creates MainMenu, Workspace and Journey), open `Assets/Scenes/MainMenu.unity`, press Play.
   The engine starts automatically from `Backend/` (or run `Backend/start_server.bat`).
3. **START** -> pick a category -> pick a video -> **ANALYSE** -> the summary viewer: the whole video with the findings marked on it, a full-length timeline, key moments, and a switch between the full video and the highlight reel.

## Category worlds and the results theater
- **3D world per category** (`Scripts/Mascot/CategoryEnvironment.cs`, shader `Vistara/SkyGradient`): picking a category replaces the neon room with a low-poly world (kitchen, bike road, boxing ring, ski slope, underwater reef ...), with its own sky, fog and light.
- **Analysis**: while the engine reads the video Stara stands beside it, turned about 40 degrees towards it (never showing her back), and inspects it with a magnifying glass (`P_Magnifier`, "Inspect" clip).
- **Results theater** (`Scripts/Mascot/ResultsTheater.cs`): left, the output JSON with the entry for the current video time lit up; right, the summary viewer with the key moments highlighted and Stara acting out the highlighted action with a 3D prop (`Resources/Mascot/Activities.fbx`, 24 props; verb picks the animation, noun or sport picks the prop).

## The mascot (Stara, Meta Llama via Ollama)
The mascot is **Stara**, a shiny yellow jelly star (rigged model `assets/VStar.blend`) in a teal outfit; she wears a prop that matches the category you pick (chef hat, helmet, boxing gloves, dive mask ...) and her dialogue box sits right under the category panel. Under the hood she is (`assets/VStar.blend`: rigged, with Idle / Blink / Wave / Jump / Talk / Happy animations). Unity renders her with the **JellyStar** shader:
translucent gel with a light-to-saturated gradient, glowing rim, fake back-light scattering, a glossy highlight streak and a vertex wobble that gets stronger while she talks.
In the Journey and Workspace scenes she stands beside the UI, turns to face you, waves when she appears, talks while the model answers and hops when she has replied. She answers
questions about the analysed video. The chat runs on **Meta Llama 3.2 (3B)** served locally by [Ollama](https://ollama.com): nothing leaves your machine.
```bash
ollama pull llama3.2:3b          # once (about 2 GB)
ollama serve                     # the app starts it for you if it is not running
```
The mascot only talks about facts taken from the analysis result (category, key moments, recognised actions, objects, what is playing now) and says so when she does not know.
Buttons: *Summarise it*, *Key moments?*, *Right now?*, *Objects?*. Environment overrides: `VISTARA_LLM_MODEL` (default `llama3.2:3b`), `VISTARA_OLLAMA_URL` (default `http://127.0.0.1:11434`).
If the model faces away from you, change `modelYawOffset` on the `VistaraMascot` object (180 by default).

## Competition inference
`Backend/run.sh <video.mp4> <out.json>`; batch: `python run_manifest.py --videos <dir>`; validate: `python validate.py --dir outputs/`.
The submission package lives in the `repo` folder next to this project.
