using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>
    /// Floating mixed-reality HUD shown after the analysis: the video with a live caption of the action being performed (verb-noun, confidence,
    /// time range), the next predicted action, contextual guidance, the highlight timeline (importance curve + selected highlights + action ticks)
    /// and hand-driven playback. Pinch the grip bar and move to reposition the panel; pinch-drag on the timeline to scrub; swipe left / right
    /// (hand gesture) to jump +/-10 s, or to the previous / next highlight in highlight-skim mode. All content comes from the model result.
    /// </summary>
    public class MrHud : MonoBehaviour
    {
        const float W = 1240, H = 740, TLX = 24, TLY = 520, TLW = 1192, TLH = 100;
        public bool Visible { get; private set; }
        public Transform Root => root;
        public event Action Closed;

        Transform root; Canvas canvas; CanvasGroup group;
        VideoController video; ResultEnvelope res;
        RawImage surface; TextMeshProUGUI titleText, capAction, nowTitle, nowTime, nextText, advice, status, timeLabel, chip, skimLabel;
        Image capBg, nowConf, playhead, hlTag, skimBg, playIcon;
        RectTransform tl;
        bool skim; float nextText_t, lastSwipe;
        static readonly System.Globalization.CultureInfo IC = System.Globalization.CultureInfo.InvariantCulture;

        public void Build(VideoController v)
        {
            video = v;
            var go = new GameObject("MrHud"); go.transform.SetParent(transform, false); root = go.transform;
            canvas = UIKit.WorldCanvas("HudCanvas", W, H, root);
            group = canvas.GetComponent<CanvasGroup>();
            var rt = (RectTransform)canvas.transform;

            var back = UIKit.Box(rt, "Backdrop", new Color(0.03f, 0.05f, 0.12f, 0.86f), 30); UIKit.Fill(back.rectTransform);
            UIKit.Glow(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.4f), 30, 40, 26);
            UIKit.Outline(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.55f), 30, 3f);

            // grip bar (pinch + move to reposition the whole HUD)
            var grip = UIKit.Box(rt, "Grip", new Color(1, 1, 1, 0.07f), 16); UIKit.TL(grip.rectTransform, 24, 14, 740, 54);
            var dh = grip.gameObject.AddComponent<DragHandle>(); dh.target = root;
            var gt = UIKit.TextAt(grip.transform, "VISTARA  MR HUD      pinch here and move to reposition", 22, 18, 0, 700, 54, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            gt.characterSpacing = 4; gt.raycastTarget = false;
            titleText = UIKit.TextAt(rt, "", 20, 780, 14, 340, 54, Theme.TextDim, TextAlignmentOptions.MidlineRight);
            UIKit.Button(rt, "", W - 96, 14, 72, 54, () => Hide(true), Theme.Magenta, Theme.Text, Sprites.Icon.Stop, 20, 16);

            // video + live caption
            var frame = UIKit.Box(rt, "VideoFrame", Color.black, 18); UIKit.TL(frame.rectTransform, 24, 84, 700, 394);
            UIKit.Outline(frame.transform, Theme.Violet * new Color(1, 1, 1, 0.6f), 18, 2f);
            var sr = UIKit.Rect(frame.transform, "Surface"); UIKit.Fill(sr, 4, 4, 4, 4);
            surface = sr.gameObject.AddComponent<RawImage>(); surface.texture = video.Texture; surface.raycastTarget = false;
            var chipBg = UIKit.Box(frame.transform, "ChipBg", new Color(0, 0, 0, 0.55f), 14, false); UIKit.TL(chipBg.rectTransform, 14, 14, 330, 40);
            chip = UIKit.TextAt(chipBg.transform, "", 19, 12, 0, 310, 40, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            var hb = UIKit.Box(frame.transform, "HlTag", Theme.Orange, 14, false); UIKit.TL(hb.rectTransform, 700 - 200, 14, 186, 40); hlTag = hb;
            UIKit.TextAt(hb.transform, "HIGHLIGHT", 19, 0, 0, 186, 40, Theme.Bg, TextAlignmentOptions.Center, FontStyles.Bold);
            capBg = UIKit.Box(frame.transform, "CapBg", new Color(0.02f, 0.04f, 0.1f, 0.72f), 16, false); UIKit.TL(capBg.rectTransform, 14, 322, 672, 58);
            capAction = UIKit.TextAt(capBg.transform, "", 28, 18, 0, 640, 58, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            // now / next / guidelines
            var now = CardUI.Card(rt, "Now", 748, 84, 468, 150, Theme.Orange);
            nowTitle = UIKit.TextAt(now, "-", 30, 22, 50, 424, 44, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            nowTime = UIKit.TextAt(now, "", 20, 22, 96, 424, 28, Theme.TextDim);
            UIKit.Bar(now, 22, 126, 424, 14, Theme.Green, out nowConf);
            var next = CardUI.Card(rt, "Next", 748, 246, 468, 92, Theme.Cyan);
            nextText = UIKit.TextAt(next, "-", 22, 22, 46, 424, 40, Theme.Text, TextAlignmentOptions.MidlineLeft);
            var gd = CardUI.Card(rt, "Guidelines", 748, 350, 468, 128, Theme.Green);
            advice = UIKit.TextAt(gd, "", 21, 22, 44, 424, 50, Theme.Text);
            status = UIKit.TextAt(gd, "", 18, 22, 96, 424, 28, Theme.Green);

            // timeline
            var tlBg = UIKit.Box(rt, "Timeline", new Color(0, 0, 0, 0.35f), 14); UIKit.TL(tlBg.rectTransform, TLX, TLY, TLW, TLH);
            UIKit.Outline(tlBg.transform, Theme.Violet * new Color(1, 1, 1, 0.5f), 14, 2f);
            tl = tlBg.rectTransform;
            tlBg.gameObject.AddComponent<PointerScrub>().Scrub += x => { if (video.Prepared) video.Seek(x * video.Length); };
            var ph = UIKit.Box(tl, "Playhead", Color.white, 0, false); UIKit.TL(ph.rectTransform, 0, 0, 4, TLH); playhead = ph;
            timeLabel = UIKit.TextAt(rt, "00:00 / 00:00", 22, TLX + TLW - 280, TLY - 30, 280, 28, Theme.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UIKit.TextAt(rt, "HIGHLIGHT TIMELINE   pinch + drag to scrub", 16, TLX, TLY - 30, 600, 28, Theme.Violet, TextAlignmentOptions.MidlineLeft, FontStyles.Bold).characterSpacing = 4;

            // transport (hand-pinch buttons)
            float y = 636, s = 78, x = TLX + 8;
            UIKit.Button(rt, "-10s", x, y, 130, s, () => video.SeekRelative(-10), Theme.Card, Theme.Text, Sprites.Icon.Back, 22, 18);
            var pp = UIKit.Button(rt, "", x + 146, y, 110, s, TogglePlay, Theme.Cyan, Theme.Bg, Sprites.Icon.Play, 20, 18, true);
            playIcon = pp.transform.Find("Icon").GetComponent<Image>();
            UIKit.Button(rt, "+10s", x + 272, y, 130, s, () => video.SeekRelative(10), Theme.Card, Theme.Text, Sprites.Icon.Forward, 22, 18);
            UIKit.Button(rt, "", x + 430, y, 90, s, () => JumpHighlight(-1), Theme.Card, Theme.Text, Sprites.Icon.Prev, 20, 18);
            UIKit.Button(rt, "", x + 534, y, 90, s, () => JumpHighlight(+1), Theme.Card, Theme.Text, Sprites.Icon.Next, 20, 18);
            var sk = UIKit.Button(rt, "SKIM HIGHLIGHTS", x + 648, y, 330, s, ToggleSkim, Theme.Card, Theme.Text, Sprites.Icon.Bolt, 22, 18);
            skimBg = sk.GetComponent<Image>(); skimLabel = sk.GetComponentInChildren<TextMeshProUGUI>();
            UIKit.Button(rt, "Details", x + 996, y, 168, s, () => Hide(true), Theme.Violet, Theme.Text, Sprites.Icon.Chart, 22, 18);

            root.gameObject.SetActive(false);
        }

        public void Show(ResultEnvelope r)
        {
            res = r; Visible = true;
            titleText.text = r.output.video_id + "  -  " + r.output.label.name + " " + r.output.label.score.ToString("0.00", IC);
            BuildTimeline();
            Place();
            root.gameObject.SetActive(true);
            skim = false; RefreshSkim();
            HandSwipeDetector.OnSwipe -= OnSwipe; HandSwipeDetector.OnSwipe += OnSwipe;
        }

        public void Hide(bool notify)
        {
            Visible = false; if (root != null) root.gameObject.SetActive(false);
            HandSwipeDetector.OnSwipe -= OnSwipe;
            if (video != null) video.Pause();
            if (notify) Closed?.Invoke();
        }

        void OnDestroy() { HandSwipeDetector.OnSwipe -= OnSwipe; }

        void Place()
        {
            var cam = Camera.main; if (cam == null) return;
            Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg, -25f, 10f) * Mathf.Deg2Rad;
            root.position = cam.transform.position + (f * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * 1.45f + Vector3.down * 0.05f;
            root.rotation = Quaternion.LookRotation(f, Vector3.up);
        }

        // ------------------------------------------------------------------ timeline
        void BuildTimeline()
        {
            for (int i = tl.childCount - 1; i >= 0; i--) { var c = tl.GetChild(i); if (c != playhead.transform) Destroy(c.gameObject); }
            float dur = Mathf.Max(0.1f, res.output.duration_sec);
            var cu = res.analysis?.curve;
            if (cu != null && cu.t != null && cu.t.Length > 1)
            {
                int n = cu.t.Length, bars = Mathf.Min(n, 220); float bw = TLW / bars;
                for (int b = 0; b < bars; b++)
                {
                    int i0 = b * n / bars, i1 = Mathf.Max(i0 + 1, (b + 1) * n / bars); float m = 0;
                    for (int i = i0; i < i1 && i < n; i++) m = Mathf.Max(m, cu.importance[i]);
                    var im = UIKit.Box(tl, "b", Theme.Ramp(m), 0, false);
                    float hh = Mathf.Max(3f, (TLH - 26f) * m);
                    UIKit.TL(im.rectTransform, b * bw, TLH - 14f - hh, Mathf.Max(1f, bw - 1f), hh);
                }
            }
            foreach (var h in res.output.highlights)
            {
                float x0 = h.segment[0] / dur * TLW, w = Mathf.Max(4f, (h.segment[1] - h.segment[0]) / dur * TLW);
                var hi = UIKit.Box(tl, "hl", Theme.Orange * new Color(1, 1, 1, 0.30f), 0, false); UIKit.TL(hi.rectTransform, x0, 0, w, TLH - 12f);
                var top = UIKit.Box(tl, "hlTop", Theme.Orange, 0, false); UIKit.TL(top.rectTransform, x0, 0, w, 4);
            }
            foreach (var a in res.output.actions.OrderByDescending(a => a.score).Take(160))   // action ticks along the bottom lane
            {
                float x0 = a.Start / dur * TLW, w = Mathf.Max(2f, (a.End - a.Start) / dur * TLW);
                var tk = UIKit.Box(tl, "act", Theme.Cyan * new Color(1, 1, 1, 0.35f + 0.6f * a.score), 0, false); UIKit.TL(tk.rectTransform, x0, TLH - 10f, w, 6);
            }
            playhead.transform.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ playback
        void TogglePlay() { if (!video.Prepared) return; if (video.IsPlaying) video.Pause(); else video.Play(); }

        void ToggleSkim() { skim = !skim; RefreshSkim(); if (skim && video.Prepared) { if (!InHighlight(video.TimeSec)) JumpHighlight(+1); video.Play(); } }

        void RefreshSkim()
        {
            if (skimBg == null) return;
            skimBg.color = skim ? Theme.Orange : Theme.Card;
            skimLabel.color = skim ? Theme.Bg : Theme.Text;
            skimLabel.text = skim ? "SKIMMING HIGHLIGHTS" : "SKIM HIGHLIGHTS";
        }

        bool InHighlight(float t) { return res.output.highlights.Any(h => h.segment[0] - 0.05f <= t && t <= h.segment[1]); }

        void JumpHighlight(int dir)
        {
            if (!video.Prepared) return;
            float t = video.TimeSec; var hl = res.output.highlights.Select(h => h.segment).OrderBy(s => s[0]).ToArray();
            if (hl.Length == 0) return;
            float[] target = null;
            if (dir > 0) target = hl.FirstOrDefault(s => s[0] > t + 0.5f);
            else target = hl.LastOrDefault(s => s[0] < t - 1.5f);
            if (target == null) { if (dir > 0) { skim = false; RefreshSkim(); video.Pause(); } return; }
            video.Seek(target[0]);
        }

        void OnSwipe(int dir)
        {
            if (!Visible || !video.Prepared) return;
            if (skim) JumpHighlight(dir); else video.SeekRelative(dir * 10f);
        }

        // ------------------------------------------------------------------ live update
        void Update()
        {
            if (!Visible || res == null) return;
            bool ok = video.Prepared; float t = ok ? video.TimeSec : 0f, len = ok ? video.Length : res.output.duration_sec;
            UIKit.TL(playhead.rectTransform, Mathf.Clamp01(len > 0 ? t / len : 0) * TLW - 2f, 0, 4, TLH);
            timeLabel.text = AppState.FormatTime(t) + " / " + AppState.FormatTime(len);
            if (playIcon != null) playIcon.sprite = Sprites.IconSprite(video.IsPlaying ? Sprites.Icon.Pause : Sprites.Icon.Play);

            if (skim && ok && video.IsPlaying && !InHighlight(t))      // highlight skimming: jump over everything that was not selected
            {
                var nxt = res.output.highlights.Select(h => h.segment).OrderBy(s => s[0]).FirstOrDefault(s => s[0] > t);
                if (nxt != null) video.Seek(nxt[0]); else { skim = false; RefreshSkim(); video.Pause(); }
            }

            if (Time.unscaledTime < nextText_t) return;
            nextText_t = Time.unscaledTime + 0.12f;
            var g = GuidanceBuilder.At(res, t);
            chip.text = res.output.label.name.ToUpper() + "   importance " + g.importance.ToString("0.00", IC);
            hlTag.gameObject.SetActive(g.inHighlight);
            if (g.current != null)
            {
                capBg.gameObject.SetActive(true);
                capAction.text = g.headline + "   <color=#" + ColorUtility.ToHtmlStringRGB(Theme.Ramp(g.current.score)) + ">" + g.current.score.ToString("0.00", IC) + "</color>";
                nowTitle.text = g.headline;
                nowTime.text = AppState.FormatTime(g.current.Start) + " - " + AppState.FormatTime(g.current.End) + "   confidence " + g.current.score.ToString("0.00", IC);
                UIKit.SetBar(nowConf, g.current.score); nowConf.color = Theme.Ramp(g.current.score);
            }
            else
            {
                capBg.gameObject.SetActive(res.output.actions.Length == 0 ? false : true);
                capAction.text = res.output.actions.Length == 0 ? "" : "...";
                nowTitle.text = g.headline; nowTime.text = res.output.actions.Length == 0 ? "no verb-noun actions for this footage type" : "no action at this moment";
                UIKit.SetBar(nowConf, 0);
            }
            nextText.text = g.next != null ? AppState.FormatTime(g.next.Start) + "   " + g.next.verb.Replace('-', ' ') + " " + g.next.noun.Replace(':', ' ') + "   (" + g.next.score.ToString("0.00", IC) + ")" : "no further actions";
            advice.text = g.advice; status.text = g.status;
        }
    }
}
