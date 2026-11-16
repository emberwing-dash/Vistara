using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vistara
{
    /// <summary>One candidate reading of a moment (the model ranks several verb-noun pairs per window).</summary>
    public class Alt { public string verb, noun; public float score; public string Text => verb.Replace('-', ' ') + " " + noun.Replace(':', ' '); }

    /// <summary>One step of the "replicate what you saw" coach, built from the model's own predictions.</summary>
    public class CoachStep
    {
        public float start, end, score, share;       // share = how much of the window's probability the best guess holds (relative confidence)
        public string verb, noun, headline, advice;
        public bool fromAction, corrected;
        public List<Alt> alts = new List<Alt>();     // the best guess first, then the runner-ups the user may pick instead

        public void Pick(int i)
        {
            if (i < 0 || i >= alts.Count) return;
            var a = alts[i]; alts.RemoveAt(i); alts.Insert(0, a);
            verb = a.verb; noun = a.noun; score = a.score; corrected = i != 0 || corrected;
            headline = a.Text; advice = GuidanceBuilder.Advice(a.verb, a.noun);
        }
    }

    /// <summary>
    /// Turns the analysis result into an ordered list of steps to repeat. The model proposes several overlapping verb-noun readings per window: they are grouped
    /// per window, the best guess becomes the step label and the runner-ups stay selectable. Only windows inside the selected highlights are used, identical
    /// neighbours are merged, a label never appears twice, steps never overlap in time. Sport footage (no verb-noun actions): the highlight segments themselves.
    /// Nothing is hard-coded: steps, times and confidences all come from the result.
    /// </summary>
    public static class StepPlanner
    {
        public static List<CoachStep> Build(ResultEnvelope r, int maxSteps = 8)
        {
            var o = r.output; float dur = Mathf.Max(0.1f, o.duration_sec);
            var steps = new List<CoachStep>();
            var hl = o.highlights.Select(h => h.segment).ToArray();
            if (o.actions != null && o.actions.Length > 0)
            {
                var groups = o.actions.GroupBy(a => Mathf.RoundToInt(a.Start * 2f)).OrderBy(g => g.Key).ToList();
                var clusters = new List<CoachStep>();
                foreach (var g in groups)
                {
                    var cand = g.OrderByDescending(a => a.score).GroupBy(a => a.verb + "|" + a.noun).Select(x => x.First()).Take(5).ToList();
                    var top = cand[0]; float sum = Mathf.Max(1e-4f, cand.Sum(c => c.score));
                    var st = new CoachStep { start = top.Start, end = Mathf.Min(top.End, top.Start + 6f), score = top.score, share = top.score / sum, verb = top.verb, noun = top.noun, fromAction = true };
                    foreach (var c in cand) st.alts.Add(new Alt { verb = c.verb, noun = c.noun, score = c.score });
                    clusters.Add(st);
                }
                var inHl = clusters.Where(c => hl.Any(h => Mathf.Min(c.end, h[1]) - Mathf.Max(c.start, h[0]) > 0f)).ToList();
                var pool = inHl.Count >= 3 ? inHl : clusters;
                var merged = new List<CoachStep>();
                foreach (var c in pool)
                {
                    var last = merged.Count > 0 ? merged[merged.Count - 1] : null;
                    if (last != null && last.verb == c.verb && last.noun == c.noun && c.start - last.end <= 2f)
                    { last.end = Mathf.Max(last.end, c.end); if (c.score > last.score) { last.score = c.score; last.share = c.share; } continue; }
                    merged.Add(c);
                }
                // one step per distinct label: keep the most confident occurrence
                var uniq = merged.GroupBy(m => m.verb + "|" + m.noun).Select(g2 => g2.OrderByDescending(m => m.share * 0.6f + m.score).First()).ToList();
                if (uniq.Count > maxSteps) uniq = uniq.OrderByDescending(m => m.share * 0.6f + m.score).Take(maxSteps).ToList();
                steps = uniq.OrderBy(m => m.start).ToList();
                foreach (var st in steps) { st.headline = st.alts[0].Text; st.advice = GuidanceBuilder.Advice(st.verb, st.noun); }
            }
            else
            {
                string tip = GuidanceBuilder.CategoryTip(o.label != null ? o.label.name : "");
                int i = 0;
                foreach (var h in hl.OrderBy(x => x[0]).Take(maxSteps))
                {
                    i++;
                    float imp = GuidanceBuilder.ImportanceAt(r, (h[0] + h[1]) / 2f);
                    steps.Add(new CoachStep { start = h[0], end = h[1], score = imp, share = imp, headline = "Key moment " + i, advice = tip });
                }
            }
            if (steps.Count == 0) steps.Add(new CoachStep { start = 0, end = Mathf.Min(dur, 8f), score = 0, headline = "Watch the video", advice = "No key moment was found; watch the whole clip." });
            foreach (var st in steps)                         // every step plays for at least 4 s so it can be watched and repeated
            {
                if (st.end - st.start < 4f) st.end = Mathf.Min(dur, st.start + 4f);
                if (st.end - st.start < 4f) st.start = Mathf.Max(0f, st.end - 4f);
            }
            for (int i = 1; i < steps.Count; i++)             // clips never overlap: a step starts where the previous one ended
                if (steps[i].start < steps[i - 1].end) steps[i].start = steps[i - 1].end;
            steps.RemoveAll(st => st.end - st.start < 2f);
            if (steps.Count == 0) steps.Add(new CoachStep { start = 0, end = Mathf.Min(dur, 8f), headline = "Watch the video", advice = "Watch the whole clip." });
            return steps;
        }
    }
}
