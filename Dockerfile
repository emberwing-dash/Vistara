# Vistara inference image (Samsung PRISM Track 04).  Build from the Backend/ folder:
#   docker build -t vistara .
#   docker run --rm --gpus all -v "$PWD/videos:/videos" -v "$PWD/out:/out" vistara /videos/P01_11.mp4 /out/P01_11.json
#   Engine for the Unity app:  docker run --rm --gpus all -p 127.0.0.1:8765:8765 vistara serve
# CPU-only also works (omit --gpus all); it is just slower. Weights are bundled in ./weights (offline at run time).
FROM pytorch/pytorch:2.11.0-cuda12.8-cudnn9-runtime

WORKDIR /app
# this base image's Python is marked "externally managed" (PEP 668); the container is disposable, so allow pip
ENV PIP_BREAK_SYSTEM_PACKAGES=1
COPY requirements.txt .
# torch/torchvision come from the base image (Python 3.11); numpy is pinned to a 3.11-compatible release.
RUN pip install --no-cache-dir numpy==2.2.6 \
 && pip install --no-cache-dir $(grep -v '^#' requirements.txt | grep -v -i -E '^(torch|torchvision|numpy)(==|$)' | tr '\n' ' ')

COPY code ./code
COPY server ./server
COPY weights ./weights
COPY run.sh validate.py docker-entrypoint.sh ./
RUN chmod +x run.sh docker-entrypoint.sh && sed -i 's/\r$//' run.sh docker-entrypoint.sh
ENV TEAM_ID=vistara PYTHONUNBUFFERED=1 PYTHONPATH=/app/code PYTHON=python

EXPOSE 8765
ENTRYPOINT ["./docker-entrypoint.sh"]
