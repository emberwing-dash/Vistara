"""Validate output JSONs against the documented Track-04 schema. Usage: python validate.py --dir outputs/ | --file x.json"""
import argparse, csv, json, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent / "code"))
from vistara.schema import validate


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--file"); ap.add_argument("--dir"); ap.add_argument("-v", action="store_true")
    ap.add_argument("--manifest", default=str(Path(__file__).parent / "data" / "manifest.csv"))
    a = ap.parse_args()
    man = {}
    if Path(a.manifest).exists():
        man = {r["video_id"]: float(r["duration_sec"]) for r in csv.DictReader(open(a.manifest, encoding="utf-8"))}
    files = [Path(a.file)] if a.file else sorted(Path(a.dir).glob("*.json"))
    bad = 0
    for f in files:
        d = json.load(open(f, encoding="utf-8"))
        errs, warns = validate(d, man.get(d.get("video_id")))
        if d.get("video_id") != f.stem:
            errs.append("video_id does not match filename")
        if errs:
            bad += 1
        if errs or (a.v and warns):
            print(f"{f.name}: {'FAIL' if errs else 'ok'}"); [print("  ERR ", e) for e in errs]; [print("  warn", w) for w in warns]
    if a.dir:
        missing = sorted(set(man) - {f.stem for f in files})
        print(f"{len(files)} files, {bad} invalid, {len(missing)} manifest videos missing")
    sys.exit(1 if bad else 0)


main()
