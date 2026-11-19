using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>Summary preview: plays the generated highlight reel, shows retained %, detected actions, exports the JSON.</summary>
    public class SummaryPanel : IPanel
    {
        readonly WorkspaceController ctx;
        RawImage surface; TextMeshProUGUI hint, bigPct, subPct, facts, exportInfo;
        RectTransform topActions;
        Image ringFill;
        bool shown;

        public SummaryPanel(WorkspaceController c) { ctx = c; }

        public void Build(RectTransform root)
        {
            var frame = UIKit.Box(root, "Frame", Color.black, 22); UIKit.TL(frame.rectTransform, 0, 0, 900, 506);
            UIKit.Outline(frame.transform, Theme.Orange * new Color(1, 1, 1, 0.7f), 22, 3f);
            var rt = UIKit.Rect(frame.transform, "Surface"); UIKit.Fill(rt, 6, 6, 6, 6);
            surface = rt.gameObject.AddComponent<RawImage>(); surface.texture = ctx.Highlight.Texture; surface.raycastTarget = false;
            hint = UIKit.TextAt(frame.transform, "Highlight reel will appear here\nafter the analysis finishes.", 28, 0, 0, 900, 506, Theme.TextDim, TextAlignmentOptions.Center);

            UIKit.Button(root, "Play highlights", 0, 526, 290, 84, () => { ctx.Highlight.Seek(0); ctx.Highlight.Play(); }, Theme.Orange, Theme.Bg, Sprites.Icon.Play, 25, 30, true);
            UIKit.Button(root, "Export JSON", 305, 526, 290, 84, Export, Theme.Cyan, Theme.Bg, Sprites.Icon.Download, 25, 30, true);
            UIKit.Button(root, "Open folder", 610, 526, 290, 84, OpenFolder, Theme.Card, Theme.Text, Sprites.Icon.Folder, 25, 30);
            exportInfo = UIKit.TextAt(root, "", 20, 0, 630, 900, 60, Theme.TextDim);

            var stat = CardUI.Card(root, "Video retained", 924, 0, 476, 300, Theme.Orange);
            var ring = UIKit.Box(stat, "Ring", Theme.Track, 0, false); ring.sprite = Sprites.Ring(0.1f); UIKit.TL(ring.rectTransform, 24, 56, 220, 220);
            ringFill = UIKit.Box(stat, "RingFill", Theme.Orange, 0, false); ringFill.sprite = Sprites.Ring(0.1f);
            ringFill.type = Image.Type.Filled; ringFill.fillMethod = Image.FillMethod.Radial360; ringFill.fillOrigin = 2; ringFill.fillAmount = 0;
            UIKit.TL(ringFill.rectTransform, 24, 56, 220, 220);
            bigPct = UIKit.TextAt(stat, "-", 56, 24, 56, 220, 220, Theme.Orange, TextAlignmentOptions.Center, FontStyles.Bold);
            subPct = UIKit.TextAt(stat, "of original footage", 20, 266, 66, 200, 60, Theme.TextDim);
            facts = UIKit.TextAt(stat, "", 22, 266, 130, 200, 160, Theme.Text);

            var act = CardUI.Card(root, "Detected actions in highlights", 924, 324, 476, 476, Theme.Magenta);
            topActions = UIKit.ScrollList(act, 14, 58, 448, 404, 6, new Color(0, 0, 0, 0.2f));
        }

        public void OnShow()
        {
            if (!shown && ctx.Highlight.Prepared) { ctx.Highlight.Seek(0); ctx.Highlight.Play(); shown = true; }
        }

        public void OnHighlightPrepared()
        {
            hint.gameObject.SetActive(false);
            if (ctx.Current == Workspace.Tab.Summary) OnShow();
        }

        public void Reset()
        {
            shown = false; hint.gameObject.SetActive(true);
            bigPct.text = "-"; ringFill.fillAmount = 0; facts.text = ""; exportInfo.text = "";
            if (topActions) UIKit.ClearChildren(topActions);
        }

        public void SetResult(ResultEnvelope r)
        {
            var o = r.output; var a = r.analysis;
            float kept = 0; foreach (var h in o.highlights) kept += h.segment[1] - h.segment[0];
            float pct = kept / Mathf.Max(1f, o.duration_sec) * 100f;
            ctx.StartCoroutine(Anim.CountTo(v => { bigPct.text = v.ToString("0.0") + "%"; ringFill.fillAmount = Mathf.Clamp01(v / 30f); /* ring scale: full circle = 30% */ }, 0, pct, 1.2f));
            facts.text = $"{AppState.FormatTime(kept)} of {AppState.FormatTime(o.duration_sec)}\n{o.highlights.Length} segments\n<color=#19E3FF>{o.label.name}</color> ({o.label.score:0.00})";

            UIKit.ClearChildren(topActions);
            if (o.actions == null || o.actions.Length == 0)
            {
                var t = UIKit.Text(topActions, a != null && a.domain != "cooking" ? "Sports / outdoor video:\nhighlights are ranked by motion,\nscene change and distinctiveness." : "No actions detected.", 22, Theme.TextDim, TextAlignmentOptions.Center);
                UIKit.Sized(t.gameObject, 120);
                return;
            }
            var inHl = o.actions.Where(ac => o.highlights.Any(h => ac.End > h.segment[0] && ac.Start < h.segment[1])).OrderByDescending(ac => ac.score).Take(40).OrderBy(ac => ac.Start);
            foreach (var ac in inHl)
            {
                var row = UIKit.Box(topActions, "row", Theme.Card, 12, false);
                UIKit.Sized(row.gameObject, 54);
                UIKit.TextAt(row.transform, AppState.FormatTime(ac.Start), 19, 12, 0, 80, 54, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                UIKit.TextAt(row.transform, $"{ac.verb} {ac.noun.Replace(':', ' ')}", 21, 96, 0, 280, 54, Theme.Text, TextAlignmentOptions.MidlineLeft);
                UIKit.TextAt(row.transform, ac.score.ToString("0.00"), 19, 372, 0, 66, 54, Theme.Ramp(ac.score), TextAlignmentOptions.MidlineRight);
            }
        }

        void Export()
        {
            if (string.IsNullOrEmpty(AppState.JobId)) { ctx.Toast("Nothing to export yet."); return; }
            ctx.StartCoroutine(BackendClient.Instance.ExportJson(AppState.JobId, AppState.Result.output.video_id,
                p => { exportInfo.text = "Saved: " + p; ctx.Toast("Exported JSON to Documents/Vistara/exports"); },
                e => ctx.Toast("Export failed: " + e)));
        }

        void OpenFolder()
        {
            string dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Vistara", "exports");
            System.IO.Directory.CreateDirectory(dir);
            Application.OpenURL("file:///" + dir.Replace('\\', '/'));
        }

        public void Tick() { }
    }
}
