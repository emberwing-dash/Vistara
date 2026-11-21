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

        static readonly bool Sim = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-previewSim") >= 0; float simT0 = -1f; string lastMode;

        public static string[] DefaultAct(string c)
        {
            switch (c)
            {
                case "Kitchen": return new[] { "take", "milk" }; case "Boxing": return new[] { "punch", "punching bag" }; case "Horse": return new[] { "ride", "horse" }; case "Jumping": return new[] { "jump", "box" };
                case "Longboarding": return new[] { "ride", "board" }; case "Parkour": return new[] { "jump", "box" }; case "Plane": return new[] { "fly", "plane" }; case "RockClimbing": return new[] { "climb", "wall" };
                case "Scuba": return new[] { "swim", "fish" }; case "Skate": return new[] { "ride", "skateboard" }; case "Ski": return new[] { "ski", "skis" }; case "Surfing": return new[] { "surf", "surfboard" };
                case "BikePolo": return new[] { "ride", "bike" }; case "Motor": return new[] { "ride", "motorbike" }; default: return new[] { "ride", "bike" };
            }
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

            var vc = journey.Source; float t;
            if (Sim) { if (simT0 < 0f) simT0 = Time.unscaledTime; t = (hls.Count > 0 ? hls[0][0] : 0f) + (Time.unscaledTime - simT0); }          // editor test only (-previewSim): a clock stands in for a video that is not loaded
            else { if (vc == null || !vc.Prepared) return; t = vc.TimeSec; }
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

            // she acts ONLY what is highlighted in this video: inside a key moment, and only something that fits the video. Kitchen: the recognised action of that moment.
            // Other categories: the recognised vocabulary is kitchen verbs, which would be irrelevant there, so she acts what the category is about (swim for scuba, ride for bike ...).
            string actVerb = null, actNoun = null; string catNow = AppState.Category;
            if (hlIdx >= 0 && !string.IsNullOrEmpty(catNow))
            {
                if (catNow == "Kitchen") { if (cur >= 0) { actVerb = acts[cur].verb; actNoun = acts[cur].noun; } }
                else { var d = DefaultAct(catNow); actVerb = d[0]; actNoun = d[1]; }
            }
            string sceneMode = null; { var scn = r.analysis != null ? r.analysis.scene : null; if (scn != null && scn.Length > 0 && catNow != "Kitchen") { sceneMode = "move"; foreach (var x in scn) if (t >= x.t0 && t < x.t1) { sceneMode = x.action; break; } } }
            { var g0 = GuideGirl.Instance; var rn = g0 != null ? g0.GetComponent<Reenactor>() : null; var cu = o.actions != null ? r.analysis?.curve : r.analysis?.curve;
              if (rn != null && sceneMode != null)
              {
                  rn.Mode = sceneMode; rn.Tempo = sceneMode == "fast" ? 1.7f : (sceneMode == "stop" || sceneMode == "idle") ? 0.25f : sceneMode == "jump" ? 1.3f : 1f;
                  if (sceneMode != lastMode) { lastMode = sceneMode; if (actVerb != null) g0.SayMode(sceneMode); }
              }
              else if (rn != null) { float a01 = 0.5f; if (cu != null && cu.activity != null && cu.activity.Length > 1) { int ix = Mathf.Clamp(Mathf.FloorToInt(t), 0, cu.activity.Length - 1); float mx = 1e-4f; foreach (var v in cu.activity) if (v > mx) mx = v; a01 = Mathf.Clamp01(cu.activity[ix] / mx); } rn.Tempo = Mathf.Lerp(0.7f, 1.7f, a01); } }
            string key = actVerb != null ? actVerb + "|" + actNoun : null;
            if (key != actKey && mascot != null)
            {
                actKey = key;
                Debug.Log("[Vistara] re-enact: " + (actVerb != null ? actVerb + " " + actNoun : "(nothing)") + " at " + t.ToString("0.0") + " s, category " + catNow + ", key moment " + hlIdx);
                try { if (actVerb != null) mascot.ActAs(actVerb, actNoun); else mascot.StopActing(); } catch (System.Exception ex) { Debug.LogWarning("[Vistara] Stara could not act: " + ex.Message); }          // her problem must never stop Vee
                if (GuideGirl.Instance != null) { if (actVerb != null) GuideGirl.Instance.ActAs(actVerb, actNoun); else GuideGirl.Instance.ActAs(null, null); }
            }
        }
    }
}
