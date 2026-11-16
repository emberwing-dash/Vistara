using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>
    /// Test Lab: run the REAL trained model on held-out evaluation videos and inspect the output.
    ///  left   - evaluation videos present on disk (EPIC-KITCHENS-100 validation + FPVSum manifest videos), pick one and run
    ///  middle - verb-noun actions (timestamps + confidence), highlight segments, highlight length vs the 15% budget, inference info
    ///  right  - model files (hashes, held-out numbers from training) and the repeatable dataset evaluation (metrics, per-video row)
    /// Everything shown comes from the engine; nothing here is hard-coded.
    /// </summary>
    public class TestLabPanel : IPanel
    {
        readonly WorkspaceController ctx;
        RectTransform videoList, actionList, segList;
        TextMeshProUGUI listInfo, verdict, budgetText, infoText, modelText, evalText, evalState, rowText, hint;
        Image budgetFill, evalFill;
        EvalVideo selected;
        EvalVideoList videos; EvalResults results; ModelInfo model;
        BackendClient Client => BackendClient.Instance;
        bool evalRunning; Coroutine evalCo;

        public TestLabPanel(WorkspaceController c) { ctx = c; }

        public void Build(RectTransform root)
        {
            // ---- left: held-out test videos ----
            var left = CardUI.Card(root, "Held-out test videos", 0, 0, 440, 800, Theme.Cyan);
            listInfo = UIKit.TextAt(left, "Loading...", 18, 24, 50, 392, 50, Theme.TextDim);
            videoList = UIKit.ScrollList(left, 16, 104, 408, 590, 6, new Color(0, 0, 0, 0.2f));
            UIKit.Button(left, "RUN TRAINED MODEL", 16, 706, 408, 78, RunSelected, Theme.Cyan, Theme.Bg, Sprites.Icon.Bolt, 26, 18, true);

            // ---- middle: model output ----
            var mid = CardUI.Card(root, "Model output", 460, 0, 480, 800, Theme.Orange);
            verdict = UIKit.TextAt(mid, "Select a video and press Run.", 24, 24, 52, 432, 64, Theme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            budgetText = UIKit.TextAt(mid, "", 19, 24, 118, 432, 30, Theme.TextDim);
            UIKit.Bar(mid, 24, 152, 432, 20, Theme.Orange, out budgetFill);
            infoText = UIKit.TextAt(mid, "", 18, 24, 182, 432, 62, Theme.TextDim);
            UIKit.TextAt(mid, "HIGHLIGHT SEGMENTS", 16, 24, 250, 432, 24, Theme.Magenta, TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 4;
            segList = UIKit.ScrollList(mid, 16, 276, 448, 150, 4, new Color(0, 0, 0, 0.2f));
            UIKit.TextAt(mid, "VERB-NOUN ACTIONS  (time, confidence)", 16, 24, 436, 432, 24, Theme.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 4;
            actionList = UIKit.ScrollList(mid, 16, 462, 448, 322, 4, new Color(0, 0, 0, 0.2f));
            hint = UIKit.TextAt(mid, "", 20, 24, 600, 432, 80, Theme.TextDim, TextAlignmentOptions.Center);

            // ---- right: model + evaluation ----
            var right = CardUI.Card(root, "Trained model + evaluation", 960, 0, 440, 800, Theme.Green);
            modelText = UIKit.TextAt(right, "Reading model info...", 17, 24, 52, 392, 150, Theme.TextDim);
            UIKit.Button(right, "RUN HELD-OUT EVALUATION", 16, 206, 408, 64, StartEval, Theme.Green, Theme.Bg, Sprites.Icon.Chart, 22, 18, true);
            UIKit.Bar(right, 24, 282, 392, 14, Theme.Green, out evalFill);
            evalState = UIKit.TextAt(right, "", 17, 24, 300, 392, 26, Theme.TextDim);
            evalText = UIKit.TextAt(right, "", 18, 24, 330, 392, 290, Theme.Text);
            UIKit.TextAt(right, "THIS VIDEO (ground truth)", 16, 24, 626, 392, 24, Theme.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 4;
            rowText = UIKit.TextAt(right, "", 17, 24, 652, 392, 130, Theme.TextDim);
        }

        public bool ListReady => videos != null && videos.items != null;
        /// <summary>Used by the automated smoke test: selects the first held-out video of a dataset.</summary>
        public EvalVideo SelectFirst(string dataset)
        {
            var v = videos?.items?.FirstOrDefault(x => x.dataset == dataset);
            if (v != null) Select(v);
            return v;
        }

        public void OnShow() { if (Client != null && Client.Online) ctx.StartCoroutine(Refresh()); }
        public void Reset() { }
        public void Tick() { }

        IEnumerator Refresh()
        {
            string err = null;
            yield return Client.EvalVideos(v => videos = v, e => err = e);
            if (videos != null) BuildList();
            else listInfo.text = "Could not list test videos: " + err;
            yield return Client.ModelInfo(m => model = m, e => err = e);
            ShowModel();
            yield return Client.EvalResults(r => results = r, e => { });
            ShowResults();
        }

        void BuildList()
        {
            UIKit.ClearChildren(videoList);
            listInfo.text = $"{videos.count} of {videos.manifest_total} manifest videos are on this machine.\nTap one to select it.";
            foreach (var v in videos.items)
            {
                var item = v;
                var row = UIKit.Box(videoList, "row", Theme.Card, 12);
                UIKit.Sized(row.gameObject, 62);
                var b = row.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = row;
                row.gameObject.AddComponent<HoverFx>();
                b.onClick.AddListener(() => Select(item));
                bool epic = v.dataset == "epic_kitchens_100";
                UIKit.TextAt(row.transform, epic ? "KITCHEN" : "SPORT", 14, 10, 0, 74, 62, epic ? Theme.Orange : Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                UIKit.TextAt(row.transform, v.video_id, 20, 86, 0, 240, 62, Theme.Text, TextAlignmentOptions.MidlineLeft);
                UIKit.TextAt(row.transform, AppState.FormatTime(v.duration_sec), 18, 316, 0, 80, 62, Theme.TextDim, TextAlignmentOptions.MidlineRight);
            }
        }

        void Select(EvalVideo v)
        {
            selected = v;
            ctx.SelectVideo(v.path, stayOnTab: true);
            verdict.text = $"Selected  <color=#19E3FF>{v.video_id}</color>  ({AppState.FormatTime(v.duration_sec)})\nPress RUN TRAINED MODEL.";
            ShowRow();
        }

        void RunSelected()
        {
            if (selected == null) { ctx.Toast("Select a test video first."); return; }
            ctx.StartPathAnalysis(selected.path);
        }

        // ---------------------------------------------------------------- model output
        public void SetResult(ResultEnvelope r)
        {
            var o = r.output; var a = r.analysis;
            float kept = 0; foreach (var h in o.highlights) kept += h.segment[1] - h.segment[0];
            float frac = kept / Mathf.Max(0.01f, o.duration_sec);
            verdict.text = $"Category: <color=#19E3FF>{o.label.name}</color> ({o.label.score:0.00})   -   {o.actions.Length} actions, {o.highlights.Length} highlights";
            budgetText.text = $"Highlights {AppState.FormatTime(kept)} of {AppState.FormatTime(o.duration_sec)} = {frac * 100:0.0}%   (budget 15%)";
            UIKit.SetBar(budgetFill, Mathf.Clamp01(frac / 0.15f));
            budgetFill.color = frac <= 0.15f ? Theme.Green : Theme.Magenta;
            infoText.text = a?.timings != null
                ? $"Inference {a.timings.total_s:0.0}s (decode {a.timings.decode_s:0.0}s, CLIP {a.timings.embed_s:0.0}s, actions {a.timings.recognise_s:0.0}s)  -  {a.model ?? "CLIP + trained heads"} on {a.device}"
                : "";

            UIKit.ClearChildren(segList);
            foreach (var h in o.highlights)
            {
                var seg = h.segment;
                var row = UIKit.Box(segList, "seg", Theme.Card, 10); UIKit.Sized(row.gameObject, 40);
                var b = row.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = row;
                row.gameObject.AddComponent<HoverFx>();
                b.onClick.AddListener(() => { ctx.JumpTo(seg[0], true); ctx.SwitchTab(Workspace.Tab.Timeline); });
                UIKit.TextAt(row.transform, $"{AppState.FormatTime(seg[0])} - {AppState.FormatTime(seg[1])}   ({seg[1] - seg[0]:0.0}s)", 18, 12, 0, 420, 40, Theme.Orange, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            }

            UIKit.ClearChildren(actionList);
            if (o.actions.Length == 0)
            {
                hint.text = a != null && a.domain != "cooking" ? "Sport / outdoor footage: the engine reports highlights and the category; verb-noun actions are only produced for kitchen video." : "No actions passed the idle gate.";
            }
            else
            {
                hint.text = "";
                foreach (var ac in o.actions.OrderBy(x => x.Start).Take(150))
                {
                    var act = ac;
                    var row = UIKit.Box(actionList, "act", Theme.Card, 10); UIKit.Sized(row.gameObject, 44);
                    var b = row.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = row;
                    row.gameObject.AddComponent<HoverFx>();
                    b.onClick.AddListener(() => { ctx.JumpTo(act.Start, true); ctx.SwitchTab(Workspace.Tab.Timeline); });
                    UIKit.TextAt(row.transform, $"{AppState.FormatTime(act.Start)}-{AppState.FormatTime(act.End)}", 16, 10, 0, 128, 44, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                    UIKit.TextAt(row.transform, $"{act.verb} {act.noun.Replace(':', ' ')}", 18, 140, 0, 220, 44, Theme.Text, TextAlignmentOptions.MidlineLeft);
                    UIKit.TextAt(row.transform, act.score.ToString("0.00"), 17, 364, 0, 64, 44, Theme.Ramp(act.score), TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                }
            }
            ShowRow();
        }

        // ---------------------------------------------------------------- model + evaluation
        void ShowModel()
        {
            if (model == null) { modelText.text = "Engine offline - model info unavailable."; return; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Backbone: {model.backbone}");
            sb.AppendLine($"Device: {model.device}   engine v{model.engine_version}");
            foreach (var h in model.heads)
            {
                bool lab = h.name.StartsWith("label");
                sb.AppendLine($"{h.name}  [{h.sha256_16}]");
                sb.AppendLine(lab ? $"   held-out label acc {h.heldout_zero_shot * 100:0.0}% -> {h.heldout_trained * 100:0.0}% ({h.n_videos} videos)"
                                  : $"   blend {h.blend:0.0}, held-out verb+noun top-1 {h.heldout_zero_shot:0.00} -> {h.heldout_trained:0.00}");
            }
            modelText.text = sb.ToString();
        }

        void ShowResults()
        {
            if (results == null || results.summary == null) { evalText.text = "No evaluation run yet. Press the button above."; return; }
            var s = results.summary; var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#8E9BC4>{s.videos_evaluated} held-out videos, {s.generated}</color>");
            if (s.epic != null && s.epic.videos > 0)
            {
                sb.AppendLine("<color=#FF8A1F>KITCHEN (EPIC-KITCHENS-100)</color>  " + s.epic.videos + " videos, " + s.epic.gt_actions + " GT actions");
                sb.AppendLine($"Action mAP {s.epic.action_map:0.000}    Coverage {s.epic.action_coverage:0.000}");
                sb.AppendLine($"verb {s.epic.verb_acc * 100:0.0}%  noun {s.epic.noun_acc * 100:0.0}%  pair {s.epic.pair_acc * 100:0.0}%");
                sb.AppendLine($"localisation recall@0.1: {s.epic.loc_recall_iou0_1 * 100:0.0}%");
            }
            if (s.fpvsum != null && s.fpvsum.videos > 0)
            {
                sb.AppendLine("<color=#19E3FF>SPORT (FPVSum)</color>  " + s.fpvsum.videos + " videos");
                sb.AppendLine($"Label accuracy {s.fpvsum.label_accuracy * 100:0.0}%    Highlight F1 " + (s.fpvsum.highlight_f1 >= 0 ? s.fpvsum.highlight_f1.ToString("0.000") : "n/a"));
            }
            if (s.all != null)
                sb.AppendLine($"<color=#3DFFA6>schema valid {s.all.schema_valid_rate * 100:0}%, within 15% budget {s.all.within_budget_rate * 100:0}%, mean {s.all.mean_highlight_frac * 100:0.0}% kept, {s.all.mean_infer_sec:0.0}s/video</color>");
            evalText.text = sb.ToString();
        }

        void ShowRow()
        {
            if (selected == null || results?.per_video == null) { rowText.text = results == null ? "" : "Select a video to compare with its ground truth."; return; }
            var r = results.per_video.FirstOrDefault(x => x.video_id == selected.video_id);
            if (r == null) { rowText.text = "This video was not part of the last evaluation run."; return; }
            rowText.text = r.dataset == "epic_kitchens_100"
                ? $"verb {Pct(r.verb_acc)}  noun {Pct(r.noun_acc)}  pair {Pct(r.pair_acc)}\nmAP {r.action_map:0.000}  coverage {r.coverage:0.000}\n{r.infer_sec:0.0}s, {r.highlight_frac * 100:0.0}% kept"
                : $"label {r.label_pred} (true: {r.label_true})\nHighlight F1 " + (r.highlight_f1 >= 0 ? r.highlight_f1.ToString("0.000") : "n/a") + $"\n{r.infer_sec:0.0}s, {r.highlight_frac * 100:0.0}% kept";
        }

        static string Pct(float v) => v < 0 ? "n/a" : (v * 100).ToString("0") + "%";

        void StartEval()
        {
            if (evalRunning) { ctx.Toast("Evaluation already running."); return; }
            if (Client == null || !Client.Online) { ctx.Toast("Engine offline."); return; }
            evalCo = ctx.StartCoroutine(EvalRoutine());
        }

        IEnumerator EvalRoutine()
        {
            evalRunning = true; evalState.text = "Starting evaluation..."; UIKit.SetBar(evalFill, 0);
            string err = null; bool ok = false;
            yield return Client.EvalStart(0, () => ok = true, e => err = e);
            if (!ok) { evalState.text = "<color=#FF2E88>" + err + "</color>"; evalRunning = false; yield break; }
            while (true)
            {
                EvalStatus st = null;
                yield return Client.EvalStatus(s => st = s, e => { });
                if (st != null)
                {
                    UIKit.SetBar(evalFill, st.total > 0 ? (float)st.done / st.total : 0);
                    evalState.text = st.state == "done" ? "Evaluation finished." : st.state == "error" ? "<color=#FF2E88>" + st.message + "</color>" : $"Running the real model: {st.done}/{st.total} {st.video}";
                    if (st.state == "done" || st.state == "error") break;
                }
                yield return new WaitForSeconds(1.5f);
            }
            yield return Client.EvalResults(r => results = r, e => { });
            ShowResults(); ShowRow();
            evalRunning = false;
        }
    }
}
