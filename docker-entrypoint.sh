#!/usr/bin/env bash
# "serve"  -> start the HTTP engine for the Unity app (port 8765)
# anything else -> competition mode: <video.mp4> <out.json>
set -euo pipefail
if [ "${1:-}" = "serve" ]; then
  mkdir -p /app/data/jobs
  cd /app/server
  exec python -m uvicorn app:app --host 0.0.0.0 --port "${PORT:-8765}"
fi
exec /app/run.sh "$@"
