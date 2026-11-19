# Vistara - the VR app (Unity 6000.2.7f2)

Hand/controller-driven summary viewer for the Vistara engine with a 3D guide (Vee) and a jelly-star mascot (Stara). The engine is not inside the Unity project: it is `../backend/server` (FastAPI) +
`../backend/vistara` (inference package) + `../../weights` (trained model). `Assets/Vistara/Scripts/Backend/BackendLauncher.cs` finds them and starts the server on port 8765.

1. Create the Python environment at the repo root (`.venv`) and install `../../requirements.txt` (see the root README); `git lfs pull` the weights.
2. Open this folder in Unity, run **Vistara > Build Scenes** once (MainMenu, Workspace, Journey), open `Assets/Scenes/MainMenu.unity`, press Play.
3. START (spatial shift) -> category (shift into its world, Vee changes outfit) -> video -> ANALYSE -> results: JSON, video with findings, insights, key moments; Vee re-enacts the highlighted action.
4. Stara: install [Ollama](https://ollama.com) and `ollama pull llama3.2:3b`.

Where things are (`Assets/Vistara`): `Scripts/Mascot` (SpaceShift, CategoryEnvironment, GuideGirl, Reenactor, SportStage, VoiceOut, MascotAssistant, ResultsTheater, InsightsPanel), `Scripts/Journey`, `Scripts/Backend`,
`Resources/Categories` (world FBX per category), `Resources/Props` (interactive prop FBX), `Resources/Girl` (Vee + 15 outfits). Sources are in `../assets`.

Editor automation for screenshots: `Unity -projectPath code/Vistara -vistaraPreview [-previewMock 1] [-previewCats A,B] [-previewSport A,B]` (writes `../preview`, silent: audio muted, no TTS).
Not verified on a physical headset.
