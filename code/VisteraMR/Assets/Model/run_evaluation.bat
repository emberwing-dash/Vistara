@echo off
rem Repeatable held-out evaluation: runs the REAL trained model on the evaluation videos listed in Config\eval_config.json
rem and compares with the public ground truth. Results -> Assets\Model\Results (eval_summary.json, eval_per_video.csv, predictions\)
rem Python: set VISTARA_PYTHON, else the sibling Vistara\Backend\.venv is used. Optional: run_evaluation.bat --limit 3
setlocal
set HERE=%~dp0
if "%VISTARA_PYTHON%"=="" (set PY=%HERE%..\..\..\Vistara\Backend\.venv\Scripts\python.exe) else (set PY=%VISTARA_PYTHON%)
set VISTARA_WEIGHTS=%HERE%Weights
set VISTARA_CLIP_DIR=%HERE%Backbone~
set PYTHONPATH=%HERE%Engine~\code
set PYTHONIOENCODING=utf-8
"%PY%" "%HERE%Engine~\evaluation\evaluate_heldout.py" --config "%HERE%Config\eval_config.json" --classes "%HERE%Config\supported_classes.json" %*
