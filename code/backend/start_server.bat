@echo off
rem Starts the Vistara AI engine (FastAPI) on http://127.0.0.1:8765  (the Unity app starts it itself; this is for running it by hand)
cd /d "%~dp0server"
set PYTHONPATH=%~dp0
set PYTHONIOENCODING=utf-8
rem keeps the headset / editor smooth while a video is analysed: small GPU batches, short pauses
set VISTARA_YIELD_MS=80
set VISTARA_AVION_BATCH=2
set VISTARA_CLIP_BATCH=16
set OMP_NUM_THREADS=3
set VISTARA_WEIGHTS=%~dp0..\..\weights
"%~dp0..\..\.venv\Scripts\python.exe" -m uvicorn app:app --host 127.0.0.1 --port 8765
