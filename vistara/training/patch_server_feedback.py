"""One-off patch: add feedback sessions + /jobs/{id}/feedback to server/app.py."""
from pathlib import Path

p = Path(__file__).resolve().parents[1] / "server" / "app.py"
s = p.read_text(encoding="utf-8")
if "/feedback" in s:
    raise SystemExit("already patched")

s = s.replace("_jobs: dict[str, dict] = {}", "_jobs: dict[str, dict] = {}\n_sessions: dict = {}          # job id -> FeedbackSession (in memory; lost on engine restart)", 1)
s = s.replace('''            with open(d / "analysis.json", "w", encoding="utf-8") as f:
                json.dump(res["analysis"], f)''', '''            with open(d / "analysis.json", "w", encoding="utf-8") as f:
                json.dump(res["analysis"], f)
            from vistara.adapt import FeedbackSession
            _sessions[job_id] = FeedbackSession(res["internal"], pipeline().cfg)''', 1)
s = s.replace('''"highlight_path": str(d / "highlight.mp4"), "source_path": j.get("source", "")}''',
              '''"highlight_path": str(d / j.get("highlight_file", "highlight.mp4")), "source_path": j.get("source", "")}''', 1)
s = s.replace('''    p = JOBS_DIR / jid / "highlight.mp4"
    if not p.exists():''', '''    p = JOBS_DIR / jid / _job(jid).get("highlight_file", "highlight.mp4")
    if not p.exists():''', 1)
s += '''

class FeedbackReq(BaseModel):
    start: float
    end: float
    vote: int          # +1 = more like this, -1 = drop this


@app.post("/jobs/{jid}/feedback")
def feedback(jid: str, req: FeedbackReq):
    """Learn from a vote on one highlight segment, re-select highlights, re-render the reel, return the new result."""
    j = _job(jid)
    sess = _sessions.get(jid)
    if j["status"] != "done" or sess is None:
        raise HTTPException(409, "feedback session not available (engine restarted?) - analyse the video again")
    d = JOBS_DIR / jid
    with _gpu_lock:
        sess.vote(req.start, req.end, req.vote)
        hl, imp = sess.reselect()
        out = json.load(open(d / "result.json", encoding="utf-8"))
        an = json.load(open(d / "analysis.json", encoding="utf-8"))
        sess.apply_to(out, an, hl, imp)
        from vistara.schema import validate
        errs, _ = validate(out)
        if errs:
            raise HTTPException(500, "feedback produced invalid output: " + "; ".join(errs))
        name = f"highlight_v{sess.n_votes}.mp4"          # new file name: the old one may still be open in Unity
        from vistara.export import export_highlight_video
        export_highlight_video(j["source"], [h["segment"] for h in out["highlights"]], str(d / name))
        json.dump(out, open(d / "result.json", "w", encoding="utf-8"), indent=1)
        json.dump(an, open(d / "analysis.json", "w", encoding="utf-8"))
        j["highlight_file"] = name
        _save_meta(j)
    return {"output": out, "analysis": an, "highlight_path": str(d / name), "source_path": j.get("source", "")}
'''
p.write_text(s, encoding="utf-8")
print("server patched")
