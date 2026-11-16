@echo off
rem Starts the model engine so a Quest (standalone build or Link) can reach it over Wi-Fi/LAN: it listens on all interfaces.
rem 1) run this on the PC   2) put the printed address in Assets\Resources\engine_url.txt (build time) or in
rem    <persistentDataPath>\engine_url.txt on the headset   3) allow port 8765 in the Windows firewall (private network).
setlocal
set HERE=%~dp0
if "%VISTARA_PYTHON%"=="" (set PY=%HERE%..\..\..\Vistara\Backend\.venv\Scripts\python.exe) else (set PY=%VISTARA_PYTHON%)
set VISTARA_WEIGHTS=%HERE%Weights
set VISTARA_CLIP_DIR=%HERE%Backbone~
set VISTARA_EVAL_CONFIG=%HERE%Config\eval_config.json
set PYTHONPATH=%HERE%Engine~\code
set PYTHONIOENCODING=utf-8
for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /c:"IPv4"') do echo Engine address candidates: http:%%a:8765
cd /d "%HERE%Engine~\server"
"%PY%" -m uvicorn app:app --host 0.0.0.0 --port 8765 --timeout-keep-alive 300
