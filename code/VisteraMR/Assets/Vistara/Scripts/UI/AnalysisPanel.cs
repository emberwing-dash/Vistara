using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>AI analysis: live progress, detected activities / objects, action timestamps, importance scores.</summary>
    public class AnalysisPanel : IPanel
    {
        readonly WorkspaceController ctx;
        Image barFill; TextMeshProUGUI pct, stageText, msg, labelText, timingText, placeholder;
        Image[] stageDots; TextMeshProUGUI[] stageNames;
        RectTransform verbArea, objArea, actionList;
        float shown, target;
        static readonly string[] Stages = { "upload", "decode", "embed", "recognise", "importance", "highlights", "export" };
        static readonly string[] StageTitles = { "Upload / download", "Decode + motion", "CLIP embeddings", "Action recognition", "Importance scoring", "Highlight selection", "Render reel" };

        public AnalysisPanel(WorkspaceController c) { ctx = c; }

        public void Build(RectTransform root)
        {
            var prog = CardUI.Card(root, "Pipeline", 0, 0, 440, 800, Theme.Cyan);
            pct = UIKit.TextAt(prog, "0%", 84, 0, 54, 440, 110, Theme.Cyan, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Bar(prog, 28, 176, 384, 22, Theme.Cyan, out barFill);
            msg = UIKit.TextAt(prog, "Waiting for a video.", 22, 28, 210, 384, 60, Theme.TextDim, TextAlignmentOptions.TopLeft);
            stageDots = new Image[Stages.Length]; stageNames = new TextMeshProUGUI[Stages.Length];
            for (int i = 0; i < Stages.Length; i++)
            {
                var d = UIKit.Box(prog, "d", Theme.Track, 0, false); d.sprite = Sprites.Circle(); UIKit.TL(d.rectTransform, 34, 292 + i * 52, 22, 22);
                stageDots[i] = d;
                stageNames[i] = UIKit.TextAt(prog, StageTitles[i], 23, 70, 286 + i * 52, 340, 34, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            }
            labelText = UIKit.TextAt(prog, "", 24, 28, 664, 384, 40, Theme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            timingText = UIKit.TextAt(prog, "", 19, 28, 706, 384, 70, Theme.TextDim);

            var act = CardUI.Card(root, "Detected activities", 460, 0, 440, 388, Theme.Orange);
            verbArea = UIKit.Rect(act, "Verbs"); UIKit.TL(verbArea, 20, 56, 400, 320);
            var obj = CardUI.Card(root, "Detected objects", 460, 412, 440, 388, Theme.Green);
            objArea = UIKit.Rect(obj, "Objs"); UIKit.TL(objArea, 20, 56, 400, 320);

            var al = CardUI.Card(root, "Action timestamps", 920, 0, 480, 800, Theme.Magenta);
            actionList = UIKit.ScrollList(al, 16, 60, 448, 722, 6, new Color(0, 0, 0, 0.2f));
            placeholder = UIKit.TextAt(al, "Run the analysis to see\nverb-noun actions with\ntimestamps and scores.", 24, 20, 300, 440, 140, Theme.TextDim, TextAlignmentOptions.Center);
        }

        public void OnShow() { }

        public void Reset()
        {
            target = shown = 0; SetBar(0); msg.text = "Waiting for a video."; pct.text = "0%"; labelText.text = ""; timingText.text = "";
            UIKit.ClearChildren(verbArea); UIKit.ClearChildren(objArea); UIKit.ClearChildren(actionList);
            if (placeholder) placeholder.gameObject.SetActive(true);
            for (int i = 0; i < Stages.Length; i++) { stageDots[i].color = Theme.Track; stageNames[i].color = Theme.TextDim; }
        }

        public void SetProgress(float p, string stage, string message)
        {
            target = Mathf.Clamp01(p); msg.text = message;
            int cur = stage == "download" ? 0 : System.Array.IndexOf(Stages, stage);
            for (int i = 0; i < Stages.Length; i++)
            {
                stageDots[i].color = i < cur ? Theme.Green : (i == cur ? Theme.Orange : Theme.Track);
                stageNames[i].color = i <= cur ? Theme.Text : Theme.TextDim;
            }
        }

        public void SetError(string e) { msg.text = "<color=#FF2E88>" + e + "</color>"; }

        public void Tick()
        {
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 0.6f);
            SetBar(shown); pct.text = Mathf.RoundToInt(shown * 100) + "%";
        }

        void SetBar(float v) { UIKit.SetBar(barFill, v); }

        public void SetResult(ResultEnvelope r)
        {
            target = shown = 1; SetBar(1); pct.text = "100%"; msg.text = "Analysis complete.";
            for (int i = 0; i < Stages.Length; i++) { stageDots[i].color = Theme.Green; stageNames[i].color = Theme.Text; }
            var o = r.output; var a = r.analysis;
            labelText.text = $"Category:  <color=#19E3FF>{o.label.name}</color>  ({o.label.score:0.00})";
            if (a?.timings != null) timingText.text = $"{a.model} on {a.device}\nprocessed in {a.timings.total_s:0.0}s  (decode {a.timings.decode_s:0.0}s, embed {a.timings.embed_s:0.0}s)";
            placeholder.gameObject.SetActive(false);

            UIKit.ClearChildren(verbArea);
            if (a?.verbs != null && a.verbs.Length > 0)
            {
                float max = a.verbs.Max(v => v.seconds);
                for (int i = 0; i < Mathf.Min(7, a.verbs.Length); i++)
                {
                    var v = a.verbs[i]; float y = i * 44;
                    UIKit.TextAt(verbArea, v.verb, 22, 0, y, 150, 36, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                    UIKit.Bar(verbArea, 150, y + 8, 180, 20, Theme.Orange, out var f); UIKit.SetBar(f, v.seconds / max);
                    UIKit.TextAt(verbArea, v.seconds.ToString("0") + "s", 20, 340, y, 60, 36, Theme.TextDim, TextAlignmentOptions.MidlineRight);
                }
            }
            else UIKit.TextAt(verbArea, a != null && a.domain != "cooking" ? "Sports / outdoor footage:\nverb-noun detection is only\nreported for kitchen video." : "No activities detected.", 22, 0, 90, 400, 120, Theme.TextDim, TextAlignmentOptions.Center);

            UIKit.ClearChildren(objArea);
            if (a?.objects != null)
            {
                float x = 0, y = 0;
                foreach (var ob in a.objects.Take(10))
                {
                    string t = $"{ob.noun.Replace(':', ' ')}  {ob.seconds:0}s";
                    float w = 30 + t.Length * 12.2f;
                    if (x + w > 400) { x = 0; y += 54; }
                    var chip = UIKit.Box(objArea, "chip", Theme.Green * new Color(1, 1, 1, 0.16f), 20, false); UIKit.TL(chip.rectTransform, x, y, w, 44);
                    UIKit.Outline(chip.transform, Theme.Green * new Color(1, 1, 1, 0.7f), 20, 2f);
                    UIKit.TextAt(chip.transform, t, 20, 0, 0, w, 44, Theme.Text, TextAlignmentOptions.Center);
                    x += w + 10;
                }
            }

            UIKit.ClearChildren(actionList);
            if (o.actions == null || o.actions.Length == 0)
            {
                placeholder.gameObject.SetActive(true);
                placeholder.text = a != null && a.domain != "cooking" ? "No verb-noun actions for\nthis footage type.\nSee Timeline for highlights." : "No actions passed the idle gate.";
                return;
            }
            foreach (var ac in o.actions.OrderByDescending(x => x.score).Take(120).OrderBy(x => x.Start))
            {
                var act = ac;
                var row = UIKit.Box(actionList, "row", Theme.Card, 12);
                UIKit.Sized(row.gameObject, 62);
                var b = row.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = row;
                row.gameObject.AddComponent<HoverFx>();
                b.onClick.AddListener(() => { ctx.JumpTo(act.Start); ctx.SwitchTab(Workspace.Tab.Timeline); });
                UIKit.TextAt(row.transform, $"{AppState.FormatTime(act.Start)}-{AppState.FormatTime(act.End)}", 19, 12, 0, 130, 62, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                UIKit.TextAt(row.transform, $"{act.verb} {act.noun.Replace(':', ' ')}", 21, 142, 0, 230, 62, Theme.Text, TextAlignmentOptions.MidlineLeft);
                UIKit.Bar(row.transform, 372, 24, 56, 14, Theme.Ramp(act.score), out var f); UIKit.SetBar(f, act.score);
            }
        }
    }
}
