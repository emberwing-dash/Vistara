@echo off
rem Run once: installs the Python environment (.venv in this folder) for the AI engine. Needs Python 3.11+ and internet (~3 GB).
rem The Unity project (code\Vistara) starts the engine by itself when you press Play and uses this .venv, weights\, videos\ and cache\.
cd /d "%~dp0"
where py >nul 2>nul && (set "PY=py -3") || (set "PY=python")
%PY% --version || goto nopython
%PY% -m venv .venv
call .venv\Scripts\activate.bat
python -m pip install --upgrade pip
nvidia-smi >nul 2>nul && (set "IDX=https://download.pytorch.org/whl/cu128") || (set "IDX=https://download.pytorch.org/whl/cpu")
echo Installing PyTorch from %IDX% ...
pip install torch torchvision --index-url %IDX%
pip install -r requirements.txt
if not errorlevel 1 goto done
echo Pinned versions failed, trying the latest compatible ones ...
python -c "import re;open('req_loose.txt','w').write(''.join(re.sub(r'==.*','',l)+'\n' for l in open('requirements.txt') if l.strip() and not l.startswith('#')))"
pip install -r req_loose.txt
:done
echo.
echo Setup finished. Open the folder code\Vistara in Unity 6000.2.7f2 and press Play.
pause
exit /b 0
:nopython
echo Python 3.11 or newer is required: https://www.python.org/downloads/  (tick "Add python.exe to PATH"), then run this file again.
pause
exit /b 1
