using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vistara
{
    /// <summary>
    /// Contextual guidance for the MR HUD. Everything that is SHOWN comes from the model output for the current playback time:
    /// the action being performed (verb, noun, confidence, time range), the next predicted action, the importance score and whether the
    /// moment is inside a selected highlight. Only the wording of the advice sentence is a fixed template keyed by the predicted verb / category label.
    /// </summary>
    public struct Guidance
    {
        public ActionItem current, next;
        public float importance;          // 0..1 from the analysis importance curve at this time
        public bool inHighlight;
        public string headline;           // e.g. "take cupboard" or "ski footage"
        public string advice;             // template sentence for the predicted verb / category
        public string status;             // highlight / importance statement from the model output
    }

    public static class GuidanceBuilder
    {
        static readonly Dictionary<string, string> VerbAdvice = new Dictionary<string, string>
        {
            { "take", "Reach for the {n} and grip it firmly before moving it." },
            { "put", "Place the {n} where it belongs and release it gently." },
            { "open", "Open the {n} fully before reaching inside." },
            { "close", "Close the {n} completely so it stays shut." },
            { "wash", "Wash the {n} thoroughly and rinse off any residue." },
            { "cut", "Keep your fingers clear of the blade while cutting the {n}." },
            { "mix", "Mix the {n} until it is evenly combined." },
            { "pour", "Pour the {n} slowly to avoid spills." },
            { "turn-on", "Turn on the {n} and check that it is running." },
            { "turn-off", "Turn off the {n} before leaving it." },
            { "move", "Move the {n} carefully to the new position." },
            { "remove", "Remove the {n} and set it aside safely." },
            { "insert", "Insert the {n} straight and push it in fully." },
            { "dry", "Dry the {n} completely before storing it." },
            { "peel", "Peel the {n} away from your body." },
            { "squeeze", "Squeeze the {n} with steady pressure." },
            { "shake", "Shake the {n} firmly a few times." },
            { "fill", "Fill the {n} up to a safe level." },
            { "throw", "Throw the {n} away in the right bin." },
            { "stir", "Stir the {n} gently and keep going until it is smooth." },
            { "press", "Press the {n} firmly and hold for a moment." },
            { "scoop", "Scoop the {n} with a smooth motion." },
            { "flip", "Flip the {n} carefully using a steady hand." },
            { "check", "Check the {n} before the next step." },
            { "clean", "Clean the {n} and wipe it dry." },
        };

        static readonly Dictionary<string, string> CategoryAdvice = new Dictionary<string, string>
        {
            { "bike", "Smooth line and steady speed: this footage is ranked by motion and scene change." },
            { "bikepolo", "Fast direction changes: highlights favour the busiest passages." },
            { "boxing", "Bursts of activity: highlights favour the exchanges." },
            { "horse", "Watch the gait changes; the model keeps the most active passages." },
            { "jumping", "Take-off and landing moments carry the most importance." },
            { "longboarding", "Carving and speed changes score highest." },
            { "motor", "Speed and cornering moments are kept; steady cruising is trimmed." },
            { "parkour", "Obstacle and landing moments score highest." },
            { "plane", "Take-off, landing and manoeuvres are kept; long cruising is trimmed." },
            { "rockclimbing", "Hard moves and reaches score highest; resting is trimmed." },
            { "scuba", "New scenery and close encounters are kept; repeated views are trimmed." },
            { "skate", "Tricks and landings score highest." },
            { "ski", "Turns and speed changes are kept; slow flats are trimmed." },
            { "surfing", "Take-offs and rides score highest; paddling is trimmed." },
        };

        public static Guidance At(ResultEnvelope r, float t)
        {
            var g = new Guidance();
            var o = r.output;
            if (o?.actions != null)
            {
                g.current = o.actions.Where(a => a.Start <= t && t <= a.End).OrderByDescending(a => a.score).FirstOrDefault();
                g.next = o.actions.Where(a => a.Start > t).OrderBy(a => a.Start).FirstOrDefault();
            }
            if (o?.highlights != null) g.inHighlight = o.highlights.Any(h => h.segment[0] <= t && t <= h.segment[1]);
            g.importance = ImportanceAt(r, t);
            string label = o?.label != null ? o.label.name : "";
            if (g.current != null)
            {
                string n = g.current.noun.Replace(':', ' '), v = g.current.verb;
                g.headline = v.Replace('-', ' ') + " " + n;
                g.advice = VerbAdvice.TryGetValue(v, out var tpl) ? tpl.Replace("{n}", n) : $"Keep your focus on the {n} while you {v.Replace('-', ' ')}.";
            }
            else if (o?.actions != null && o.actions.Length > 0)
            {
                g.headline = "between actions";
                g.advice = g.next != null ? $"Get ready: next is {g.next.verb.Replace('-', ' ')} {g.next.noun.Replace(':', ' ')} at {AppState.FormatTime(g.next.Start)}." : "No further actions detected.";
            }
            else
            {
                g.headline = label + " footage";
                g.advice = CategoryAdvice.TryGetValue(label, out var ca) ? ca : "Highlights are ranked by motion, scene change and distinctiveness.";
            }
            g.status = g.inHighlight ? "Key moment: inside the highlight reel."
                     : g.importance >= 0.6f ? "High importance, but outside the 15% budget."
                     : g.importance < 0.25f ? "Low activity: skipped in the summary."
                     : "Moderate importance.";
            return g;
        }

        public static float ImportanceAt(ResultEnvelope r, float t)
        {
            var c = r.analysis?.curve;
            if (c == null || c.t == null || c.t.Length == 0) return 0f;
            int i = System.Array.BinarySearch(c.t, t);
            if (i < 0) i = ~i;
            i = Mathf.Clamp(i, 0, c.t.Length - 1);
            return c.importance[i];
        }
    }
}
