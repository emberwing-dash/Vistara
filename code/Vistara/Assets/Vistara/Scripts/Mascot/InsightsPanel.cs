using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>
    /// Data visualisation under the summary viewer: what the engine found, at a glance.
    ///  - "Story" strip: how interesting each moment is (bars), the actions in time (coloured by verb), the key moments (gold), and a moving playhead.
    ///  - "Top verbs" / "Top objects": the five most frequent, as bars. Short labels only.
    /// Built from AppState.Result (output + analysis) and rebuilt when a new result arrives.
    /// </summary>
    public class InsightsPanel : MonoBehaviour
    {
        const float W = 1040f, H = 300f, Sc = 0.00026f;
        const float SX = 24f, SY = 70f, SW = 600f;           // story strip origin / width
        JourneyController journey; Canvas canvas; RectTransform rt, plot;
        RectTransform playhead; TextMeshProUGUI timeLbl; string builtFor; float dur = 1f;
        static readonly Color[] Pal = { Theme.Cyan, Theme.Magenta, Theme.Violet, Theme.Orange, Theme.Green, Theme.Hex("FFD84A"), Theme.Hex("5B9BFF"), Theme.Hex("FF6B6B") };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name == "Journey") Spawn(); };
            if (SceneManager.GetActiveScene().name == "Journey") Spawn();
        }
        static void Spawn() { if (FindFirstObjectByType<InsightsPanel>() == null) new GameObject("InsightsPanel").AddComponent<InsightsPanel>(); }

        void Start()
        {
            journey = FindFirstObjectByType<JourneyController>();
            canvas = UIKit.WorldCanvas("InsightsPanel", W, H, null, Sc); rt = (RectTransform)canvas.transform;
            canvas.gameObject.SetActive(false);
        }

        static Color VerbColor(string v) { int h = 17; foreach (char c in v ?? "") h = h * 31 + c; return Pal[Mathf.Abs(h) % Pal.Length]; }

        void Rebuild(ResultEnvelope r)
        {
            UIKit.ClearChildren(rt);
            var o = r.output; var a = r.analysis; dur = Mathf.Max(1f, o.duration_sec);
            var back = UIKit.Box(rt, "Back", new Color(0.03f, 0.05f, 0.12f, 0.92f), 26, false); UIKit.Fill(back.rectTransform);
            UIKit.Outline(back.transform, Theme.Violet * new Color(1, 1, 1, 0.6f), 26, 3f);

            // ---------------- story strip
            UIKit.TextAt(rt, "Story", 26, SX, 16, 200, 40, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            string len = Mathf.FloorToInt(dur / 60f) + ":" + Mathf.FloorToInt(dur % 60f).ToString("00");
            UIKit.TextAt(rt, len + " total", 20, SX + SW - 200, 16, 200, 40, Theme.TextDim, TextAlignmentOptions.MidlineRight);
            plot = UIKit.Rect(rt, "Plot"); UIKit.TL(plot, SX, SY, SW, 200);
            var track = UIKit.Box(plot, "Track", Theme.Track * new Color(1, 1, 1, 0.45f), 8, false); UIKit.TL(track.rectTransform, 0, 0, SW, 120);

            if (a != null && a.curve != null && a.curve.t != null && a.curve.t.Length > 1)
            {
                var imp = a.curve.importance ?? a.curve.activity; int n = a.curve.t.Length; int cols = Mathf.Min(n, 120);
                float mx = 0.001f; for (int i = 0; i < imp.Length; i++) mx = Mathf.Max(mx, imp[i]);
                float cw = SW / cols;
                for (int c = 0; c < cols; c++)
                {
                    int i0 = c * n / cols, i1 = Mathf.Max(i0 + 1, (c + 1) * n / cols); float v = 0; for (int i = i0; i < i1 && i < imp.Length; i++) v = Mathf.Max(v, imp[i]);
                    float hh = Mathf.Max(3f, 112f * v / mx);
                    var b = UIKit.Box(plot, "c", Color.Lerp(Theme.Violet, Theme.Cyan, v / mx), 0, false); UIKit.TL(b.rectTransform, c * cw + 0.5f, 118f - hh, cw - 1f, hh);
                }
            }
            // action strip (coloured by verb)
            UIKit.TextAt(plot, "actions", 15, 0, 124, 120, 22, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            foreach (var act in (o.actions ?? new ActionItem[0]))
            {
                float x = act.Start / dur * SW, w = Mathf.Max(2f, (act.End - act.Start) / dur * SW);
                var b = UIKit.Box(plot, "a", VerbColor(act.verb), 3, false); UIKit.TL(b.rectTransform, x, 146, w - 1f, 26);
            }
            // key moments strip (gold)
            UIKit.TextAt(plot, "key moments", 15, 0, 174, 160, 22, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            foreach (var h in (o.highlights ?? new SegmentItem[0]))
            {
                float x = h.segment[0] / dur * SW, w = Mathf.Max(3f, (h.segment[1] - h.segment[0]) / dur * SW);
                var b = UIKit.Box(plot, "h", Theme.Hex("FFD84A"), 3, false); UIKit.TL(b.rectTransform, x, 182, w, 14);
            }
            var ph = UIKit.Box(plot, "Playhead", Color.white, 0, false); playhead = ph.rectTransform; UIKit.TL(playhead, 0, -4, 3, 204);
            timeLbl = UIKit.TextAt(rt, "", 20, SX, 268, SW, 28, Theme.Text, TextAlignmentOptions.MidlineLeft);

            // ---------------- top verbs / objects
            Bars("Top verbs", 664, (o.actions ?? new ActionItem[0]).GroupBy(x => x.verb).Select(g => (g.Key, g.Sum(x => Mathf.Max(0.5f, x.End - x.Start)))).OrderByDescending(p => p.Item2).Take(5).ToList(), true);
            var nouns = a != null && a.objects != null && a.objects.Length > 0
                ? a.objects.OrderByDescending(x => x.seconds > 0 ? x.seconds : x.count).Take(5).Select(x => (x.noun, x.seconds > 0 ? x.seconds : (float)x.count)).ToList()
                : (o.actions ?? new ActionItem[0]).GroupBy(x => x.noun).Select(g => (g.Key, g.Sum(x => Mathf.Max(0.5f, x.End - x.Start)))).OrderByDescending(p => p.Item2).Take(5).ToList();
            Bars("Top objects", 664, nouns, false, 160);
            builtFor = o.video_id + "|" + (o.actions != null ? o.actions.Length : 0);
        }

        void Bars(string title, float x, List<(string, float)> items, bool byVerb, float yOff = 0f)
        {
            UIKit.TextAt(rt, title, 22, x, 14 + yOff, 340, 34, byVerb ? Theme.Magenta : Theme.Orange, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            if (items.Count == 0) return; float mx = items.Max(p => p.Item2);
            for (int i = 0; i < items.Count; i++)
            {
                float y = 50 + yOff + i * 20f; string lbl = items[i].Item1;
                UIKit.TextAt(rt, lbl, 17, x, y - 3, 110, 22, Theme.Text, TextAlignmentOptions.MidlineLeft);
                var tr = UIKit.Box(rt, "t", Theme.Track * new Color(1, 1, 1, 0.5f), 0, false); UIKit.TL(tr.rectTransform, x + 116, y + 2, 230, 12);
                var f = UIKit.Box(rt, "f", byVerb ? VerbColor(lbl) : Theme.Orange, 0, false); UIKit.TL(f.rectTransform, x + 116, y + 2, Mathf.Max(6f, 230f * items[i].Item2 / mx), 12);
            }
        }

        void Update()
        {
            if (journey == null) { journey = FindFirstObjectByType<JourneyController>(); return; }
            bool on = journey.Hud != null && journey.Hud.Visible && AppState.HasResult && journey.Hud.Root != null && canvas != null;
            if (canvas == null) return;
            if (canvas.gameObject.activeSelf != on) canvas.gameObject.SetActive(on);
            if (!on) return;
            var r = AppState.Result; var o = r.output;
            if (builtFor != o.video_id + "|" + (o.actions != null ? o.actions.Length : 0)) Rebuild(r);
            var frame = journey.Hud.Root; float s = frame.lossyScale.x;
            canvas.transform.SetPositionAndRotation(frame.TransformPoint(new Vector3(0f, (640f * Sc * 0.5f) + (H * Sc * 0.5f) + 0.02f, 0f)), frame.rotation);
            canvas.transform.localScale = Vector3.one * (Sc * s);
            var vc = journey.Source; if (vc == null || !vc.Prepared || playhead == null) return;
            float t = vc.TimeSec; playhead.anchoredPosition = new Vector2(t / dur * SW, playhead.anchoredPosition.y);
            string now = "";
            if (o.actions != null) { ActionItem best = null; foreach (var a in o.actions) if (a.Start <= t && t <= a.End && (best == null || a.score >= best.score)) best = a; if (best != null) now = best.verb + " " + best.noun; }
            timeLbl.text = now.Length > 0 ? "Now: <b>" + now.Replace("<", "") + "</b>" : "";
        }
    }
}
