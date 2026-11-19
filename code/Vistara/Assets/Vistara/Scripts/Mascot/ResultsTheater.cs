using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vistara
{
    /// <summary>
    /// The results "theater". Left: the output JSON (the exact fields the engine returns) with the entry that matches the video time lit up.
    /// Centre/right: the summary viewer (video with highlighted key moments, MrHud) and Stara, who acts out whatever action is highlighted right now with a 3D prop.
    /// </summary>
    public class ResultsTheater : MonoBehaviour
    {
        JourneyController journey; Canvas canvas; TextMeshProUGUI body, badge; RectTransform rt;
        List<string> lines = new List<string>(); List<int> actionLine = new List<int>(); List<ActionItem> acts = new List<ActionItem>(); List<float[]> hls = new List<float[]>();
        string builtFor, actKey; float nextTick; const float W = 640f, H = 640f, Sc = 0.00026f; const int Rows = 22;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name == "Journey") Spawn(); };
            if (SceneManager.GetActiveScene().name == "Journey") Spawn();
        }
        static void Spawn() { if (FindFirstObjectByType<ResultsTheater>() == null) new GameObject("ResultsTheater").AddComponent<ResultsTheater>(); }

        void Start() { journey = FindFirstObjectByType<JourneyController>(); Build(); }

        void Build()
        {
            canvas = UIKit.WorldCanvas("JsonPanel", W, H, null, Sc); rt = (RectTransform)canvas.transform;
            var back = UIKit.Box(rt, "Back", new Color(0.03f, 0.05f, 0.12f, 0.92f), 26); UIKit.Fill(back.rectTransform);
            UIKit.Outline(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.55f), 26, 3f);
            UIKit.TextAt(rt, "output.json", 28, 28, 16, 380, 44, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            badge = UIKit.TextAt(rt, "", 22, 330, 16, 290, 44, Theme.Orange, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            body = UIKit.TextAt(rt, "", 21, 28, 72, W - 56, H - 90, new Color(0.82f, 0.93f, 1f), TextAlignmentOptions.TopLeft);
            body.overflowMode = TextOverflowModes.Truncate; body.textWrappingMode = TextWrappingModes.NoWrap; body.richText = true;
            canvas.gameObject.SetActive(false);
        }

        static string F(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        static string Esc(string s) { return (s ?? "").Replace("<", "").Replace(">", ""); }

        void BuildText(ResultEnvelope r)
        {
            var o = r.output; lines.Clear(); actionLine.Clear(); acts = (o.actions ?? new ActionItem[0]).OrderBy(a => a.Start).ToList();
            hls = (o.highlights ?? new SegmentItem[0]).Select(h => h.segment).OrderBy(s => s[0]).ToList();
            lines.Add("{");
            lines.Add("  <color=#7fe0ff>\"video_id\"</color>: \"" + Esc(o.video_id) + "\",");
            lines.Add("  <color=#7fe0ff>\"duration_sec\"</color>: " + F(o.duration_sec) + ",");
            lines.Add("  <color=#7fe0ff>\"label\"</color>: { \"name\": \"" + Esc(o.label != null ? o.label.name : "") + "\", \"score\": " + F2(o.label != null ? o.label.score : 0f) + " },");
            lines.Add("  <color=#7fe0ff>\"highlights\"</color>: [");
            for (int i = 0; i < hls.Count; i++) lines.Add("    { \"segment\": [" + F(hls[i][0]) + ", " + F(hls[i][1]) + "] }" + (i < hls.Count - 1 ? "," : ""));
            lines.Add("  ],");
            lines.Add("  <color=#7fe0ff>\"actions\"</color>: [");
            for (int i = 0; i < acts.Count; i++)
            {
                actionLine.Add(lines.Count);
                var a = acts[i];
                lines.Add("    { \"segment\": [" + F(a.Start) + ", " + F(a.End) + "], \"verb\": \"" + Esc(a.verb) + "\", \"noun\": \"" + Esc(a.noun) + "\" }" + (i < acts.Count - 1 ? "," : ""));
            }
            lines.Add("  ]");
            lines.Add("}");
            builtFor = o.video_id + "|" + acts.Count + "|" + hls.Count;
        }

        void Update()
        {
            var mascot = MascotAssistant.Instance;
            if (journey == null) { journey = FindFirstObjectByType<JourneyController>(); return; }
            bool on = journey.Hud != null && journey.Hud.Visible && AppState.HasResult && journey.Hud.Root != null;
            if (canvas.gameObject.activeSelf != on) canvas.gameObject.SetActive(on);
            if (!on) { if (actKey != null && mascot != null) { mascot.StopActing(); actKey = null; } return; }
            var r = AppState.Result; var o = r.output;
            if (builtFor != o.video_id + "|" + (o.actions != null ? o.actions.Length : 0) + "|" + (o.highlights != null ? o.highlights.Length : 0)) BuildText(r);

            var frame = journey.Hud.Root; float sc = Sc * frame.lossyScale.x; float wM = 1040f * 0.00026f * frame.lossyScale.x;
            canvas.transform.SetPositionAndRotation(frame.TransformPoint(new Vector3(-(1040f * 0.00026f * 0.5f) - W * Sc * 0.5f - 0.02f, 0f, 0f)), frame.rotation);
            canvas.transform.localScale = Vector3.one * sc;

            var vc = journey.Source; if (vc == null || !vc.Prepared) return;
            float t = vc.TimeSec;
            int cur = -1; float best = -1f;
            for (int i = 0; i < acts.Count; i++) if (acts[i].Start <= t && t <= acts[i].End && acts[i].score >= best) { best = acts[i].score; cur = i; }
            int hlIdx = -1; for (int i = 0; i < hls.Count; i++) if (t >= hls[i][0] && t <= hls[i][1]) { hlIdx = i; break; }
            badge.text = hlIdx >= 0 ? "KEY MOMENT " + (hlIdx + 1) + "/" + hls.Count : (cur >= 0 ? "ACTION" : "");

            if (Time.unscaledTime >= nextTick)
            {
                nextTick = Time.unscaledTime + 0.15f;
                int focus = cur >= 0 ? actionLine[cur] : (lines.Count > 0 ? 0 : 0);
                if (cur < 0)
                {
                    int next = acts.FindIndex(a => a.Start > t); focus = next >= 0 ? actionLine[next] : actionLine.Count > 0 ? actionLine[actionLine.Count - 1] : 0;
                }
                int start = Mathf.Clamp(focus - Rows / 2, 0, Mathf.Max(0, lines.Count - Rows)); var sb = new StringBuilder();
                for (int i = start; i < Mathf.Min(lines.Count, start + Rows); i++)
                {
                    string ln = lines[i];
                    if (cur >= 0 && i == actionLine[cur]) ln = "<mark=#ff9a1f55><color=#ffd27a>" + System.Text.RegularExpressions.Regex.Replace(ln, "<[^>]+>", "") + "</color></mark>";
                    sb.AppendLine(ln);
                }
                body.text = sb.ToString();
            }

            string key = cur >= 0 ? acts[cur].verb + "|" + acts[cur].noun : null;
            if (key != actKey && mascot != null)
            {
                actKey = key;
                if (cur >= 0) mascot.ActAs(acts[cur].verb, acts[cur].noun); else mascot.StopActing();
                if (GuideGirl.Instance != null) { if (cur >= 0) GuideGirl.Instance.ActAs(acts[cur].verb, acts[cur].noun); else GuideGirl.Instance.ActAs(null, null); }
            }
        }
    }
}
