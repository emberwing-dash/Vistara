"""Root shortcut so the documented command works from the repo root:  python validate.py --dir outputs/   (or --file examples/P02_109.json -v)
The validator itself is code/backend/validate.py (manifest: code/backend/data/manifest.csv)."""
import runpy, sys
from pathlib import Path
sys.argv[0] = str(Path(__file__).resolve().parent / "code" / "backend" / "validate.py")
runpy.run_path(sys.argv[0], run_name="__main__")
