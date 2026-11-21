using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>
    /// Video-summary viewer (stage 3). After the analysis you see the WHOLE video with what the model found marked on it:
    ///  * overlay on the video   - sign badge with the action at the current time (verb noun, confidence), object tags, KEY MOMENT flag when the time is inside a highlight
    ///  * full-length timeline    - the selected highlights (orange), action ticks, playhead; tap / pinch-drag anywhere to scrub
    ///  * KEY MOMENTS list        - the highlights with the best action inside each; tap one to jump there
    ///  * FULL VIDEO / HIGHLIGHT REEL - play everything, or only the selected highlights back to back (the summary, cut from the original footage)
    /// It is a summariser, not a tutorial: nothing asks you to repeat what you see. The panel is held close to your chest, follows you when you turn away
    /// and is moved with the pinch grips (sides / bottom bar). Objects are shown as tags (the model gives no pixel positions, so there are no boxes).
    /// </summary>
    public class MrHud : MonoBehaviour
    {
        const float W = 1040, H = 640, TlX = 24, TlY = 430, TlW = 992, TlH = 60, Buckets = 124;
        public bool Visible { get; private set; }
        public Transform Root => root;
        public event Action Closed;
        public int StepIndex => hlIdx;
        public int StepCount => hls == null ? 0 : hls.Count;
        public bool Finished => reelDone;
        public string PhaseName => reel ? "HighlightReel" : "FullVideo";

        Transform root; Canvas canvas; VideoController video; ResultEnvelope res;
        ActionItem[] acts = new ActionItem[0]; List<float[]> hls = new List<float[]>(); float dur = 1f;
        bool reel, reelDone, paused; int hlIdx; float nextOverlay;
        // header / video overlay
        TextMeshProUGUI titleText, statsText, signText, modeText, altText, timeText, objText, reelBtnText;
        Image signBg, signIcon, keyBadge, frameOutline, playhead, playIcon; RectTransform overlayRoot, tl, tlBars, rowsRoot;
        Image[] tagBg = new Image[3]; TextMeshProUGUI[] tagTxt = new TextMeshProUGUI[3];
        // follow-me
        Vector3 offsetLocal, followVel; bool followMoving; float dragUntil; const float Tilt = 38f;

        public void Build(VideoController v)
        {
            video = v;
            var go = new GameObject("SummaryViewer"); go.transform.SetParent(transform, false); root = go.transform;
            canvas = UIKit.WorldCanvas("SummaryCanvas", W, H, root, 0.00026f);
            var rt = (RectTransform)canvas.transform;

            var back = UIKit.Box(rt, "Backdrop", new Color(0.03f, 0.05f, 0.12f, 0.90f), 30); UIKit.Fill(back.rectTransform);
            UIKit.Glow(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.30f), 30, 40, 26);
            UIKit.Outline(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.55f), 30, 3f);
            foreach (var h in UIKit.SideGrips(rt, W, H, root, 90f, 0.25f, 1.2f)) h.Dragged += _ => dragUntil = Time.unscaledTime + 1.5f;

            // header: what was found
            titleText = UIKit.TextAt(rt, "SUMMARY", 34, 28, 10, 300, 48, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            statsText = UIKit.TextAt(rt, "", 20, 330, 10, 610, 48, Theme.TextDim, TextAlignmentOptions.MidlineRight);
            statsText.enableAutoSizing = true; statsText.fontSizeMin = 13; statsText.fontSizeMax = 20;
            UIKit.Button(rt, "", W - 84, 8, 60, 46, () => Hide(true), Theme.Card, Theme.Text, Sprites.Icon.Stop, 16, 14);

            // the whole video + overlay signs
            var frame = UIKit.Box(rt, "VideoFrame", Color.black, 18); UIKit.TL(frame.rectTransform, 24, 64, 620, 349);
            frameOutline = UIKit.Outline(frame.transform, Theme.Violet * new Color(1, 1, 1, 0.6f), 18, 3f);
            var sr = UIKit.Rect(frame.transform, "Surface"); UIKit.Fill(sr, 4, 4, 4, 4);
            var raw = sr.gameObject.AddComponent<RawImage>(); raw.texture = video.Texture; raw.raycastTarget = true;
            var tap = sr.gameObject.AddComponent<Button>(); tap.transition = Selectable.Transition.None; tap.targetGraphic = raw; tap.onClick.AddListener(TogglePause);   // tap the video = pause / play

            overlayRoot = UIKit.Rect(frame.transform, "Overlay"); UIKit.TL(overlayRoot, 0, 0, 620, 349);
            signBg = UIKit.Box(overlayRoot, "Sign", Theme.Orange, 16, false); UIKit.TL(signBg.rectTransform, 12, 12, 470, 58);
            signIcon = UIKit.Icon(signBg.transform, Sprites.Icon.Bolt, Theme.Bg, 12, 10, 38);
            signText = UIKit.TextAt(signBg.transform, "", 26, 60, 0, 402, 58, Theme.Bg, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            signText.enableAutoSizing = true; signText.fontSizeMin = 16; signText.fontSizeMax = 26;
            altText = UIKit.TextAt(overlayRoot, "", 18, 16, 74, 470, 28, Theme.Text, TextAlignmentOptions.MidlineLeft);
            keyBadge = UIKit.Box(overlayRoot, "Key", Theme.Green, 14, false); UIKit.TL(keyBadge.rectTransform, 490, 12, 118, 40);
            UIKit.TextAt(keyBadge.transform, "KEY", 22, 0, 0, 118, 40, Theme.Bg, TextAlignmentOptions.Center, FontStyles.Bold);
            for (int k = 0; k < 3; k++)
            {
                tagBg[k] = UIKit.Box(overlayRoot, "Tag", Theme.Cyan, 14, false); UIKit.TL(tagBg[k].rectTransform, 12 + k * 196, 296, 186, 42);
                tagTxt[k] = UIKit.TextAt(tagBg[k].transform, "", 22, 10, 0, 166, 42, Theme.Bg, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                tagTxt[k].enableAutoSizing = true; tagTxt[k].fontSizeMin = 14; tagTxt[k].fontSizeMax = 22;
            }
            modeText = UIKit.TextAt(overlayRoot, "", 20, 360, 250, 248, 34, Theme.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UIKit.Button(frame.transform, "", 548, 62, 52, 36, () => overlayRoot.gameObject.SetActive(!overlayRoot.gameObject.activeSelf), new Color(0.04f, 0.07f, 0.16f, 0.78f), Theme.Text, Sprites.Icon.Film, 14, 10);

            // key moments + objects
            UIKit.TextAt(rt, "KEY MOMENTS", 22, 668, 64, 340, 30, Theme.Orange, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            rowsRoot = UIKit.Rect(rt, "Rows"); UIKit.TL(rowsRoot, 664, 98, 352, 250);
            objText = UIKit.TextAt(rt, "", 20, 668, 354, 348, 62, Theme.TextDim, TextAlignmentOptions.TopLeft);

            // full-length timeline: highlights, action ticks, playhead
            var tlBg = UIKit.Box(rt, "Timeline", new Color(0, 0, 0, 0.45f), 8); UIKit.TL(tlBg.rectTransform, TlX, TlY, TlW, TlH + 22); tl = tlBg.rectTransform;
            tlBg.gameObject.AddComponent<PointerScrub>().Scrub += OnScrub;
            tlBars = UIKit.Rect(tl, "Bars");
            var ph = UIKit.Box(tl, "Playhead", Color.white, 0, false); UIKit.TL(ph.rectTransform, 0, -4, 4, TlH + 30); playhead = ph;
            timeText = UIKit.TextAt(rt, "", 20, TlX, TlY + TlH + 26, TlW, 28, Theme.TextDim, TextAlignmentOptions.MidlineLeft);

            // controls
            float y = 548, s = 80;
            UIKit.Button(rt, "", 24, y, 110, s, () => JumpHighlight(-1), Theme.Card, Theme.Text, Sprites.Icon.Prev, 20, 22);
            UIKit.Button(rt, "", 146, y, 110, s, () => Skip(-10f), Theme.Card, Theme.Text, Sprites.Icon.Back, 20, 22);
            var pp = UIKit.Button(rt, "", 268, y, 150, s, TogglePause, Theme.Cyan, Theme.Bg, Sprites.Icon.Pause, 20, 22, true);
            playIcon = pp.transform.Find("Icon").GetComponent<Image>();
            UIKit.Button(rt, "", 430, y, 110, s, () => Skip(10f), Theme.Card, Theme.Text, Sprites.Icon.Forward, 20, 22);
            UIKit.Button(rt, "", 552, y, 110, s, () => JumpHighlight(1), Theme.Card, Theme.Text, Sprites.Icon.Next, 20, 22);
            var rb = UIKit.Button(rt, "HIGHLIGHT REEL", 680, y, 336, s, ToggleReel, Theme.Orange, Theme.Bg, Sprites.Icon.Film, 26, 22, true);
            reelBtnText = rb.GetComponentInChildren<TextMeshProUGUI>();
            root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ lifecycle
        public void Show(ResultEnvelope r)
        {
            res = r; dur = Mathf.Max(0.1f, r.output.duration_sec);
            acts = r.output.actions ?? new ActionItem[0];
            hls = (r.output.highlights ?? new SegmentItem[0]).Select(h => h.segment).OrderBy(s0 => s0[0]).ToList();
            reel = false; reelDone = false; paused = false; hlIdx = 0;
            BuildHeader(); BuildTimeline(); BuildRows(); Place();
            Visible = true; root.gameObject.SetActive(true);
            if (video.Prepared) { video.Seek(0f); video.Play(); }
            RefreshMode(); RefreshOverlay();
        }

        public void Hide(bool notify)
        {
            Visible = false; if (root != null) root.gameObject.SetActive(false);
            if (video != null) video.Pause();
            if (notify) Closed?.Invoke();
        }

        /// <summary>Close to the chest, below eye level, tilted back like a tablet; the offset is remembered for the follow-me behaviour.</summary>
        void Place()
        {
            var cam = Camera.main; if (cam == null) return;
            Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            root.localScale = Vector3.one * (UIKit.Reach * 0.72f); root.position = cam.transform.position + f * 0.78f + Vector3.down * 0.40f;          // smaller and lower than the menus, so Vee's scene stays in view above it          // low in your view: the middle of your sight stays free for Vee and the scene
            root.rotation = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(Tilt, 0f, 0f);
            offsetLocal = Quaternion.Inverse(Quaternion.LookRotation(f, Vector3.up)) * (root.position - cam.transform.position);
            followMoving = false;
        }

        // ------------------------------------------------------------------ content
        void BuildHeader()
        {
            var o = res.output; float kept = hls.Sum(h => h[1] - h[0]);
            string lab = o.label != null ? o.label.name.ToUpper() : "VIDEO";
            titleText.text = lab;
            statsText.text = "highlights " + AppState.FormatTime(kept) + " of " + AppState.FormatTime(dur) + " (" + Mathf.RoundToInt(kept / dur * 100f) + "%)  -  " + hls.Count + " key moments  -  " + acts.Length + " action readings";
            var objs = res.analysis?.objects;
            objText.text = objs != null && objs.Length > 0 ? "OBJECTS  " + string.Join(", ", objs.Take(6).Select(x => x.noun.Replace(':', ' ') + " " + Mathf.RoundToInt(x.seconds) + "s")) : "";
        }

        void BuildTimeline()
        {
            UIKit.ClearChildren(tlBars);
            UIKit.TL(tlBars, 0, 0, TlW, TlH + 22);
            foreach (var h in hls)                                // selected highlights: orange strip along the bottom
            {
                var b = UIKit.Box(tlBars, "hl", Theme.Orange * new Color(1, 1, 1, 0.85f), 4, false);
                UIKit.TL(b.rectTransform, h[0] / dur * TlW, 14, Mathf.Max(6f, (h[1] - h[0]) / dur * TlW), TlH - 22);
            }
            foreach (var a in acts.OrderByDescending(a => a.score).Take(40))      // the most confident actions as ticks along the top
            {
                var tk = UIKit.Box(tlBars, "tick", Theme.Cyan, 0, false); UIKit.TL(tk.rectTransform, (a.Start + a.End) / 2f / dur * TlW - 1.5f, 0, 3, 8);
            }
            playhead.transform.SetAsLastSibling();
        }

        void BuildRows()
        {
            UIKit.ClearChildren(rowsRoot);
            int n = Mathf.Min(4, hls.Count); float rh = 58;
            for (int r = 0; r < n; r++)
            {
                int hi = hls.Count <= 4 ? r : Mathf.RoundToInt(r * (hls.Count - 1) / 3f);       // spread through the highlights when there are more than fit
                var seg = hls[hi]; int idx = hi;
                var best = acts.Where(a => a.End > seg[0] && a.Start < seg[1]).OrderByDescending(a => a.score).FirstOrDefault();
                string label = best != null ? best.verb.Replace('-', ' ') + " " + best.noun.Replace(':', ' ') : "key moment " + (hi + 1);
                var bt = UIKit.Button(rowsRoot, "", 0, r * (rh + 6), 352, rh, () => SeekHighlight(idx), Theme.Card, Theme.Text, null, 20, 14);
                UIKit.TextAt(bt.transform, AppState.FormatTime(seg[0]), 22, 14, 0, 80, rh, Theme.Orange, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                var lt = UIKit.TextAt(bt.transform, label, 22, 98, 0, 244, rh, Theme.Text, TextAlignmentOptions.MidlineLeft);
                lt.enableAutoSizing = true; lt.fontSizeMin = 14; lt.fontSizeMax = 22;
            }
        }

        // ------------------------------------------------------------------ overlay on the video (what the model found at this time)
        static Color VerbColor(string v)
        {
            switch (v)
            {
                case "take": case "put": case "move": case "remove": case "insert": case "throw": return Theme.Cyan;
                case "open": case "close": case "press": case "turn-on": case "turn-off": return Theme.Violet;
                case "cut": case "peel": case "squeeze": case "shake": return Theme.Magenta;
                case "wash": case "pour": case "fill": case "dry": case "clean": return Theme.Green;
                default: return Theme.Orange;
            }
        }

        static Sprites.Icon VerbIcon(string v)
        {
            switch (v)
            {
                case "take": case "remove": case "open": return Sprites.Icon.Up;
                case "put": case "pour": case "fill": case "insert": return Sprites.Icon.Download;
                case "mix": case "stir": case "wash": case "turn-on": case "turn-off": case "shake": case "flip": return Sprites.Icon.Refresh;
                case "check": case "close": return Sprites.Icon.Check;
                case "move": case "throw": return Sprites.Icon.Forward;
                default: return Sprites.Icon.Bolt;
            }
        }

        bool InHighlight(float t) { foreach (var h in hls) if (t >= h[0] - 0.05f && t <= h[1]) return true; return false; }

        void RefreshOverlay()
        {
            if (res == null) return;
            float t = video.Prepared ? video.TimeSec : 0f; bool inH = InHighlight(t);
            keyBadge.gameObject.SetActive(inH);
            frameOutline.color = inH ? Theme.Green : Theme.Violet * new Color(1, 1, 1, 0.6f);
            foreach (var g in tagBg) g.gameObject.SetActive(false);
            altText.text = "";
            if (acts.Length > 0)
            {
                var cand = acts.Where(a => a.Start <= t && t <= a.End).OrderByDescending(a => a.score)
                               .GroupBy(a => a.verb + "|" + a.noun).Select(g => g.First()).Take(5).ToList();
                if (cand.Count > 0)
                {
                    var top = cand[0]; float share = top.score / Mathf.Max(1e-4f, cand.Sum(a => a.score));
                    signBg.color = VerbColor(top.verb); signIcon.sprite = Sprites.IconSprite(VerbIcon(top.verb));
                    signText.text = top.verb.Replace('-', ' ') + " " + top.noun.Replace(':', ' ') + "  " + Mathf.RoundToInt(share * 100) + "%";
                    if (cand.Count > 1) altText.text = "or: " + string.Join(", ", cand.Skip(1).Take(2).Select(a => a.verb.Replace('-', ' ') + " " + a.noun.Replace(':', ' ')));
                    var nouns = cand.Select(a => a.noun).Distinct().Take(3).ToList();
                    Color[] pal = { Theme.Cyan, Theme.Orange, Theme.Magenta };
                    for (int k = 0; k < nouns.Count; k++) { tagBg[k].gameObject.SetActive(true); tagBg[k].color = pal[k]; tagTxt[k].text = nouns[k].Replace(':', ' '); }
                }
                else { signBg.color = new Color(0.25f, 0.3f, 0.4f, 0.9f); signIcon.sprite = Sprites.IconSprite(Sprites.Icon.Film); signText.text = "no action recognised here"; }
            }
            else
            {
                float imp = GuidanceBuilder.ImportanceAt(res, t);
                signBg.color = inH ? Theme.Orange : new Color(0.25f, 0.3f, 0.4f, 0.9f); signIcon.sprite = Sprites.IconSprite(Sprites.Icon.Bolt);
                signText.text = (res.output.label != null ? res.output.label.name : "video") + "  importance " + Mathf.RoundToInt(imp * 100) + "%";
            }
        }

        void RefreshMode()
        {
            reelBtnText.text = reel ? "FULL VIDEO" : "HIGHLIGHT REEL";
            modeText.text = reel ? (reelDone ? "REEL FINISHED" : "HIGHLIGHT REEL  " + (hlIdx + 1) + " / " + hls.Count) : "FULL VIDEO";
        }

        // ------------------------------------------------------------------ controls
        void ToggleReel()
        {
            reel = !reel; reelDone = false;
            if (reel && hls.Count > 0) { hlIdx = 0; SeekTo(hls[0][0]); }
            RefreshMode();
        }

        void SeekTo(float t) { if (video.Prepared) { video.Seek(Mathf.Clamp(t, 0f, dur)); if (!paused) video.Play(); } }
        void Skip(float d) { if (video.Prepared) { if (reel) { reel = false; RefreshMode(); } SeekTo(video.TimeSec + d); } }
        void OnScrub(float x01) { if (reel) { reel = false; RefreshMode(); } SeekTo(x01 * dur); }
        void SeekHighlight(int i) { if (i < 0 || i >= hls.Count) return; hlIdx = i; reelDone = false; SeekTo(hls[i][0]); RefreshMode(); }

        /// <summary>Previous / next highlight (in the reel: previous / next segment of the reel).</summary>
        void JumpHighlight(int dir)
        {
            if (hls.Count == 0) return;
            if (reel) { SeekHighlight(Mathf.Clamp(hlIdx + dir, 0, hls.Count - 1)); return; }
            float t = video.Prepared ? video.TimeSec : 0f; int target = -1;
            if (dir > 0) { for (int i = 0; i < hls.Count; i++) if (hls[i][0] > t + 0.5f) { target = i; break; } }
            else { for (int i = hls.Count - 1; i >= 0; i--) if (hls[i][0] < t - 1.5f) { target = i; break; } }
            if (target >= 0) { hlIdx = target; SeekTo(hls[target][0]); RefreshMode(); }
        }

        void TogglePause()
        {
            if (reelDone && reel) { SeekHighlight(0); paused = false; return; }
            paused = !paused;
            if (paused) video.Pause(); else video.Play();
        }

        // smoke-test / compatibility hooks
        public void Next() { JumpHighlight(1); }
        public void Prev() { JumpHighlight(-1); }
        public void Replay() { if (hls.Count > 0) SeekHighlight(0); }
        public void DoneStep() { ToggleReel(); }

        // ------------------------------------------------------------------ live update
        void Update()
        {
            if (!Visible || res == null || video == null) return;
            if (playIcon != null) playIcon.sprite = Sprites.IconSprite(paused ? Sprites.Icon.Play : Sprites.Icon.Pause);
            if (PanelDock.Follow) FollowMe();
            if (video.Prepared)
            {
                float t = video.TimeSec;
                UIKit.TL(playhead.rectTransform, Mathf.Clamp01(t / dur) * TlW - 2f, -4, 4, TlH + 30);
                timeText.text = AppState.FormatTime(t) + " / " + AppState.FormatTime(dur);
                if (Time.unscaledTime >= nextOverlay) { nextOverlay = Time.unscaledTime + 0.2f; RefreshOverlay(); }
                if (reel && !reelDone && video.Settled && hls.Count > 0)              // summary playback: only the highlights, one after another
                {
                    var seg = hls[Mathf.Clamp(hlIdx, 0, hls.Count - 1)];
                    if (t >= seg[1] - 0.05f)
                    {
                        if (hlIdx < hls.Count - 1) { hlIdx++; SeekTo(hls[hlIdx][0]); RefreshMode(); }
                        else { reelDone = true; video.Pause(); RefreshMode(); }
                    }
                    else if (t < seg[0] - 0.6f) SeekTo(seg[0]);
                }
            }
        }

        /// <summary>Stays put while you look around, but glides back in front of your chest when you turn away or walk off.</summary>
        void FollowMe()
        {
            var cam = Camera.main; if (cam == null || Time.unscaledTime < dragUntil) { if (cam != null && Time.unscaledTime < dragUntil) UpdateOffset(cam); return; }
            Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) return; f.Normalize();
            var yaw = Quaternion.LookRotation(f, Vector3.up);
            Vector3 desired = cam.transform.position + yaw * offsetLocal;
            Vector3 toRoot = root.position - cam.transform.position; toRoot.y = 0;
            float ang = toRoot.sqrMagnitude > 1e-4f ? Vector3.Angle(f, toRoot.normalized) : 0f;
            if (ang > 24f || Vector3.Distance(root.position, desired) > 0.24f) followMoving = true;       // tight follow: it stays attached to your sight
            if (!followMoving) return;
            root.position = Vector3.SmoothDamp(root.position, desired, ref followVel, 0.26f);
            root.rotation = Quaternion.Slerp(root.rotation, yaw * Quaternion.Euler(Tilt, 0f, 0f), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            if (Vector3.Distance(root.position, desired) < 0.04f) followMoving = false;
        }

        void UpdateOffset(Camera cam)
        {
            Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) return; f.Normalize();
            offsetLocal = Quaternion.Inverse(Quaternion.LookRotation(f, Vector3.up)) * (root.position - cam.transform.position);
            followMoving = false;
        }
    }
}
