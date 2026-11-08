@echo off
rem Starts the Vistara AI engine (FastAPI) on http://127.0.0.1:8765
cd /d "%~dp0server"
set PYTHONPATH=%~dp0code
"%~dp0.venv\Scripts\python.exe" -m uvicorn app:app --host 127.0.0.1 --port 8765
