#!/usr/bin/env bash
# Usage: ./run.sh <video.mp4> <out.json>
# Env:   TEAM_ID (default "vistara"), VISTARA_WEIGHTS (default ./weights)
set -euo pipefail
if [ "$#" -lt 2 ]; then echo "usage: $0 <video.mp4> <out.json>" >&2; exit 2; fi
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if   [ -x "$HERE/.venv/Scripts/python.exe" ]; then PY="$HERE/.venv/Scripts/python.exe"
elif [ -x "$HERE/.venv/bin/python" ];        then PY="$HERE/.venv/bin/python"
else PY="${PYTHON:-python3}"; fi
export PYTHONPATH="$HERE/code${PYTHONPATH:+:$PYTHONPATH}"
exec "$PY" -m vistara.cli "$1" "$2" "${@:3}"
