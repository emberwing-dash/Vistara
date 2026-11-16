"""Assemble the TEAM_ID/ submission folder: run.sh, code/, weights/, requirements.txt, outputs/, README.md.
   python package_submission.py --team-id T07 [--dest dist]"""
import argparse, shutil
from pathlib import Path

ap = argparse.ArgumentParser()
ap.add_argument("--team-id", required=True); ap.add_argument("--dest", default="dist")
a = ap.parse_args()
src = Path(__file__).parent; dst = Path(a.dest) / a.team_id
if dst.exists():
    shutil.rmtree(dst)
dst.mkdir(parents=True)
ign = shutil.ignore_patterns("__pycache__", "*.pyc")
shutil.copytree(src / "code", dst / "code", ignore=ign)
shutil.copytree(src / "weights", dst / "weights")
shutil.copytree(src / "outputs", dst / "outputs")
for f in ("run.sh", "requirements.txt", "README.md", "validate.py"):
    shutil.copy2(src / f, dst / f)
print("created", dst.resolve())
