#!/usr/bin/env bash
# Repeatable held-out evaluation (real model, real ground truth). Results -> Assets/Model/Results. Optional: --limit 3
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PY="${VISTARA_PYTHON:-$HERE/../../../Vistara/Backend/.venv/Scripts/python.exe}"; [ -x "$PY" ] || PY="${VISTARA_PYTHON:-python3}"
export VISTARA_WEIGHTS="$HERE/Weights" VISTARA_CLIP_DIR="$HERE/Backbone~" PYTHONPATH="$HERE/Engine~/code" PYTHONIOENCODING=utf-8
exec "$PY" "$HERE/Engine~/evaluation/evaluate_heldout.py" --config "$HERE/Config/eval_config.json" --classes "$HERE/Config/supported_classes.json" "$@"
