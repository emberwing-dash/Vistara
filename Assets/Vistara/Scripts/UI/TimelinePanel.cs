using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>Click / drag on the timeline strip -> seek (works with XR ray via pointer events).</summary>
    public class TimelineClick : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IDragHandler
    {
        public System.Action<float> OnSeek01;
        public void OnPointerClick(PointerEventData e) { Seek(e); }
        public void OnPointerDown(PointerEventData e) { Seek(e); }
        public void OnDrag(PointerEventData e) { Seek(e); }
        void Seek(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera != null ? e.pressEventCamera : e.enterEventCamera, out var local))
                OnSeek01?.Invoke(Mathf.Clamp01((local.x - rt.rect.xMin) / rt.rect.width));
        }
    }

    /// <summary>Interactive highlight timeline: importance bars over the original footage, highlighted segments, playhead.</summary>
    public class TimelinePanel : IPanel
    {
        readonly WorkspaceController ctx;
        RawImage preview; TextMeshProUGUI hint;
        RectTransform strip, content, playhead, segList;
        TextMeshProUGUI statsText, ticksHolder, cursorLabel;
        float duration;
        const float SW = 1360, SH = 250;

        public TimelinePanel(WorkspaceController c) { ctx = c; }

        public void Build(RectTransform root)
        {
            var frame = UIKit.Box(root, "Preview", Color.black, 20); UIKit.TL(frame.rectTransform, 0, 0, 640, 360);
            UIKit.Outline(frame.transform, Theme.Cyan * new Color(1, 1, 1, 0.5f), 20, 3f);
            var rt = UIKit.Rect(frame.transform, "Surface"); UIKit.Fill(rt, 5, 5, 5, 5);
            preview = rt.gameObject.AddComponent<RawImage>(); preview.texture = ctx.Source.Texture; preview.raycastTarget = false;

            var stats = CardUI.Card(root, "Highlight budget", 664, 0, 736, 360, Theme.Orange);
            statsText = UIKit.TextAt(stats, "Run an analysis to build the timeline.", 26, 28, 64, 680, 280, Theme.TextDim);
            statsText.lineSpacing = 12;

            var tl = CardUI.Card(root, "Importance timeline  -  click to jump", 0, 384, 1400, 416, Theme.Violet);
            var stripBg = UIKit.Box(tl, "Strip", new Color(0, 0, 0, 0.35f), 12);
            UIKit.TL(stripBg.rectTransform, 20, 62, SW, SH);
            strip = stripBg.rectTransform;
            stripBg.gameObject.AddComponent<TimelineClick>().OnSeek01 = v => { if (duration > 0) ctx.JumpTo(v * duration); };
            content = UIKit.Rect(strip, "Content"); UIKit.Fill(content);
            playhead = UIKit.Box(strip, "Playhead", Color.white, 0, false).rectTransform;
            playhead.anchorMin = new Vector2(0, 0); playhead.anchorMax = new Vector2(0, 1); playhead.sizeDelta = new Vector2(4, 0); playhead.pivot = new Vector2(0.5f, 0.5f);
            ticksHolder = UIKit.TextAt(tl, "", 18, 20, 318, SW, 24, Theme.TextDim);
            segList = UIKit.Rect(tl, "Segs"); UIKit.TL(segList, 20, 346, SW, 56);
            hint = UIKit.TextAt(strip.transform, "No analysis yet", 28, 0, 0, SW, SH, Theme.TextDim, TextAlignmentOptions.Center);
            cursorLabel = UIKit.TextAt(tl, "", 20, 1100, 18, 280, 28, Theme.TextDim, TextAlignmentOptions.TopRight);
        }

        public void OnShow() { }
        public void OnSourcePrepared() { }

        public void Reset()
        {
            duration = 0; UIKit.ClearChildren(content); UIKit.ClearChildren(segList);
            hint.gameObject.SetActive(true); statsText.text = "Run an analysis to build the timeline."; statsText.color = Theme.TextDim; ticksHolder.text = "";
        }

        public void SetResult(ResultEnvelope r)
        {
            Reset();
            hint.gameObject.SetActive(false);
            var o = r.output; var a = r.analysis;
            duration = o.duration_sec;
            statsText.color = Theme.Text;
            float hl = 0; foreach (var h in o.highlights) hl += h.segment[1] - h.segment[0];
            statsText.text = $"<color=#8E9BC4>Video length</color>   {AppState.FormatTime(duration)}\n" +
                             $"<color=#8E9BC4>Highlights kept</color>   {AppState.FormatTime(hl)}  (<color=#FF8A1F>{hl / duration * 100f:0.0}%</color>)\n" +
                             $"<color=#8E9BC4>Budget (15%)</color>   {AppState.FormatTime(a.budget_seconds)}\n" +
                             $"<color=#8E9BC4>Segments</color>   {o.highlights.Length}\n" +
                             $"<color=#8E9BC4>Category</color>   {o.label.name}  ({o.label.score:0.00})";

            // importance bars (original footage representation)
            var c = a.curve;
            if (c != null && c.t != null && c.t.Length > 1)
            {
                int n = c.t.Length;
                float bw = SW / n;
                for (int i = 0; i < n; i++)
                {
                    var bar = UIKit.Box(content, "b", Theme.Ramp(c.importance[i]) * new Color(1, 1, 1, 0.8f), 0, false);
                    bar.rectTransform.anchorMin = new Vector2(i / (float)n, 0); bar.rectTransform.anchorMax = new Vector2((i + 1) / (float)n, 0);
                    bar.rectTransform.pivot = new Vector2(0.5f, 0);
                    bar.rectTransform.offsetMin = new Vector2(0.5f, 0); bar.rectTransform.offsetMax = new Vector2(-0.5f, Mathf.Max(6, c.importance[i] * (SH - 40)));
                }
            }
            // highlighted segments
            if (a.segments != null)
            {
                for (int i = 0; i < a.segments.Length; i++)
                {
                    var s = a.segments[i];
                    float x0 = s.start / duration, x1 = s.end / duration;
                    var band = UIKit.Box(content, "seg", Theme.Orange * new Color(1, 1, 1, 0.22f), 6, false);
                    band.rectTransform.anchorMin = new Vector2(x0, 0); band.rectTransform.anchorMax = new Vector2(x1, 1);
                    band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;
                    var cap = UIKit.Box(band.transform, "cap", Theme.Orange, 6, false);
                    cap.rectTransform.anchorMin = new Vector2(0, 1); cap.rectTransform.anchorMax = new Vector2(1, 1);
                    cap.rectTransform.pivot = new Vector2(0.5f, 1); cap.rectTransform.sizeDelta = new Vector2(0, 14);
                    UIKit.Outline(band.transform, Theme.Orange, 6, 2f);
                }
            }
            // action ticks
            if (o.actions != null)
                foreach (var ac in o.actions)
                {
                    var tick = UIKit.Box(content, "t", Theme.Magenta * new Color(1, 1, 1, 0.6f), 0, false);
                    tick.rectTransform.anchorMin = new Vector2(ac.Start / duration, 0); tick.rectTransform.anchorMax = new Vector2(ac.End / duration, 0);
                    tick.rectTransform.pivot = new Vector2(0.5f, 0); tick.rectTransform.sizeDelta = new Vector2(0, 5); tick.rectTransform.anchoredPosition = Vector2.zero;
                }
            playhead.SetAsLastSibling();

            // time ticks
            string ts = ""; int steps = 6;
            for (int i = 0; i <= steps; i++) ts += AppState.FormatTime(duration * i / steps).PadRight(i == steps ? 0 : 0) + (i < steps ? new string(' ', 56) : "");
            ticksHolder.text = ts; ticksHolder.alignment = TextAlignmentOptions.TopLeft; ticksHolder.overflowMode = TextOverflowModes.Overflow;
            ticksHolder.textWrappingMode = TextWrappingModes.NoWrap;

            // segment jump buttons
            UIKit.ClearChildren(segList);
            if (a.segments != null)
            {
                int show = Mathf.Min(a.segments.Length, 9);
                float bw = (SW - (show - 1) * 8) / Mathf.Max(1, show);
                for (int i = 0; i < show; i++)
                {
                    var s = a.segments[i];
                    UIKit.Button(segList, $"{AppState.FormatTime(s.start)}  ({s.importance:0.00})", i * (bw + 8), 0, bw, 52, () => { ctx.JumpTo(s.start, true); }, Theme.Card, Theme.Orange, null, 17, 14);
                }
            }
        }

        public void Tick()
        {
            if (duration <= 0 || playhead == null) return;
            float t = ctx.Source.Prepared ? ctx.Source.TimeSec : 0;
            playhead.anchorMin = new Vector2(t / duration, 0); playhead.anchorMax = new Vector2(t / duration, 1); playhead.anchoredPosition = Vector2.zero;
            cursorLabel.text = AppState.FormatTime(t);
        }
    }
}
