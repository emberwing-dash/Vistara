using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>
    /// "Do it yourself" coach (stage 3). After the analysis the video becomes a short list of steps built from the model's predictions
    /// (verb-noun actions inside the highlights; highlight moments for sport footage). The coach walks through them on its own:
    ///   GET READY  - the objects you need (nouns from the predictions)
    ///   per step   - WATCH: the clip of that step plays once -> YOUR TURN: it pauses and counts down while you repeat the step in real life
    ///                -> the next step starts automatically (DONE skips the wait, replay watches the clip again)
    /// It is a small panel held close to your chest that follows you when you turn away; pinch a side grip to move it. Hand swipes: left = next, right = previous.
    /// </summary>
    public class MrHud : MonoBehaviour
    {
        const float W = 900, H = 540, StripX = 24, StripY = 368, StripW = 852;
        enum Phase { Intro, Watch, Turn, Finished }
        public bool Visible { get; private set; }
        public Transform Root => root;
        public event Action Closed;
        public int StepIndex => idx;
        public int StepCount => steps == null ? 0 : steps.Count;
        public bool Finished => phase == Phase.Finished;
        public string PhaseName => phase.ToString();

        Transform root; Canvas canvas;
        VideoController video; ResultEnvelope res;
        List<CoachStep> steps; bool[] done; int idx; Phase phase; bool paused; float turnLeft, turnTotal, watchTimer, introLeft;
        string gather;
        TextMeshProUGUI stepLabel, headline, advice, conf, timeRange, capText;
        Image signBg, signIcon, frameOutline; TextMeshProUGUI signText; Button[] altBtn = new Button[2]; TextMeshProUGUI[] altTxt = new TextMeshProUGUI[2]; RectTransform overlayRoot; TextMeshProUGUI nextText;
        Image nowConf, playhead, doneIcon, playIcon, hlTag, turnBar; Image[] dots = new Image[0]; RectTransform dotsRoot, strip;
        // follow-me
        Vector3 offsetLocal, followVel; bool followMoving; float dragUntil; const float Tilt = 25f;

        public void Build(VideoController v)
        {
            video = v;
            var go = new GameObject("DoItCoach"); go.transform.SetParent(transform, false); root = go.transform;
            canvas = UIKit.WorldCanvas("CoachCanvas", W, H, root, 0.00026f);
            var rt = (RectTransform)canvas.transform;

            var back = UIKit.Box(rt, "Backdrop", new Color(0.03f, 0.05f, 0.12f, 0.88f), 30); UIKit.Fill(back.rectTransform);
            UIKit.Glow(back.transform, Theme.Green * new Color(1, 1, 1, 0.35f), 30, 40, 26);
            UIKit.Outline(back.transform, Theme.Green * new Color(1, 1, 1, 0.6f), 30, 3f);
            foreach (var h in UIKit.SideGrips(rt, W, H, root, 80f, 0.25f, 1.2f)) h.Dragged += _ => dragUntil = Time.unscaledTime + 1.5f;      // pinch a side grip to move the coach
            dotsRoot = UIKit.Rect(rt, "Dots"); UIKit.TL(dotsRoot, 24, 18, W - 130, 26);
            UIKit.Button(rt, "", W - 84, 8, 60, 42, () => Hide(true), Theme.Card, Theme.Text, Sprites.Icon.Stop, 16, 14);

            // the clip of the current step + live caption (and the "your turn" countdown bar)
            var frame = UIKit.Box(rt, "VideoFrame", Color.black, 18); UIKit.TL(frame.rectTransform, 24, 60, 520, 292);
            frameOutline = UIKit.Outline(frame.transform, Theme.Violet * new Color(1, 1, 1, 0.6f), 18, 3f);
            var sr = UIKit.Rect(frame.transform, "Surface"); UIKit.Fill(sr, 4, 4, 4, 4);
            var raw = sr.gameObject.AddComponent<RawImage>(); raw.texture = video.Texture; raw.raycastTarget = true;
            var tap = sr.gameObject.AddComponent<Button>(); tap.transition = Selectable.Transition.None; tap.targetGraphic = raw; tap.onClick.AddListener(TogglePause);     // tap the video = pause / resume
            hlTag = UIKit.Box(frame.transform, "HlTag", Theme.Orange, 12, false); UIKit.TL(hlTag.rectTransform, 520 - 62, 10, 50, 32);
            UIKit.Icon(hlTag.transform, Sprites.Icon.Bolt, Theme.Bg, 9, 3, 26);
            var capBg = UIKit.Box(frame.transform, "CapBg", new Color(0.02f, 0.04f, 0.1f, 0.72f), 14, false); UIKit.TL(capBg.rectTransform, 10, 230, 500, 52);
            capText = UIKit.TextAt(capBg.transform, "", 24, 14, 0, 472, 46, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Bar(capBg.transform, 14, 42, 472, 6, Theme.Orange, out turnBar);

            // overlay signs on the video: what the model sees (sign badge) + the runner-up readings you can tap if the best guess is wrong
            overlayRoot = UIKit.Rect(frame.transform, "Overlay"); UIKit.TL(overlayRoot, 0, 0, 520, 292);
            signBg = UIKit.Box(overlayRoot, "Sign", Theme.Orange, 16, false); UIKit.TL(signBg.rectTransform, 12, 12, 400, 58);
            signIcon = UIKit.Icon(signBg.transform, Sprites.Icon.Bolt, Theme.Bg, 12, 10, 38);
            signText = UIKit.TextAt(signBg.transform, "", 26, 60, 0, 332, 58, Theme.Bg, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            signText.enableAutoSizing = true; signText.fontSizeMin = 16; signText.fontSizeMax = 26;
            for (int k = 0; k < 2; k++)
            {
                int ai = k + 1;
                altBtn[k] = UIKit.Button(overlayRoot, "", 12, 78 + k * 46, 330, 40, () => PickAlt(ai), new Color(0.04f, 0.07f, 0.16f, 0.78f), Theme.Text, null, 20, 12);
                altTxt[k] = UIKit.TextAt(altBtn[k].transform, "", 20, 14, 0, 302, 40, Theme.Text, TextAlignmentOptions.MidlineLeft);
            }
            nextText = UIKit.TextAt(overlayRoot, "", 18, 12, 176, 330, 28, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            UIKit.Button(frame.transform, "", 404, 10, 46, 32, () => overlayRoot.gameObject.SetActive(!overlayRoot.gameObject.activeSelf), new Color(0.04f, 0.07f, 0.16f, 0.78f), Theme.Text, Sprites.Icon.Film, 14, 10);

            // what to do now
            var info = UIKit.Box(rt, "Info", Theme.Panel, 22); UIKit.TL(info.rectTransform, 568, 60, 308, 292);
            UIKit.Outline(info.transform, Theme.Green * new Color(1, 1, 1, 0.55f), 22, 2f);
            stepLabel = UIKit.TextAt(info.transform, "", 20, 20, 14, 268, 26, Theme.TextDim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            headline = UIKit.TextAt(info.transform, "", 34, 20, 42, 270, 90, Theme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIKit.Bar(info.transform, 20, 136, 200, 14, Theme.Green, out nowConf);
            conf = UIKit.TextAt(info.transform, "", 20, 228, 128, 70, 30, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            advice = UIKit.TextAt(info.transform, "", 24, 20, 164, 270, 100, Theme.Green);
            timeRange = UIKit.TextAt(info.transform, "", 18, 20, 262, 270, 24, Theme.TextDim, TextAlignmentOptions.MidlineLeft);

            // thin strip: where the steps are in the video, with the playhead
            var st = UIKit.Box(rt, "Strip", new Color(0, 0, 0, 0.4f), 6, false); UIKit.TL(st.rectTransform, StripX, StripY, StripW, 24); strip = st.rectTransform;
            var ph = UIKit.Box(strip, "Playhead", Color.white, 0, false); UIKit.TL(ph.rectTransform, 0, -3, 3, 30); playhead = ph;

            // controls: previous step, watch again, DONE (next), skip, pause / resume the coach
            float y = 410, s = 96;
            UIKit.Button(rt, "", 24, y, 120, s, Prev, Theme.Card, Theme.Text, Sprites.Icon.Prev, 20, 22);
            UIKit.Button(rt, "", 156, y, 120, s, Replay, Theme.Card, Theme.Text, Sprites.Icon.Refresh, 20, 22);
            var db = UIKit.Button(rt, "", 288, y, 300, s, DoneStep, Theme.Green, Theme.Bg, Sprites.Icon.Check, 20, 26, true);
            doneIcon = db.transform.Find("Icon").GetComponent<Image>();
            UIKit.Button(rt, "", 600, y, 120, s, Next, Theme.Card, Theme.Text, Sprites.Icon.Next, 20, 22);
            var pp = UIKit.Button(rt, "", 732, y, 144, s, TogglePause, Theme.Cyan, Theme.Bg, Sprites.Icon.Pause, 20, 22);
            playIcon = pp.transform.Find("Icon").GetComponent<Image>();

            root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ lifecycle
        public void Show(ResultEnvelope r)
        {
            res = r; steps = StepPlanner.Build(r); done = new bool[steps.Count]; paused = false;
            gather = GatherList();
            BuildDots(); BuildStrip(); Place();
            Visible = true; root.gameObject.SetActive(true);
            HandSwipeDetector.OnSwipe -= OnSwipe; HandSwipeDetector.OnSwipe += OnSwipe;
            if (gather.Length > 0) EnterIntro(); else GoTo(0);
        }

        public void Hide(bool notify)
        {
            Visible = false; if (root != null) root.gameObject.SetActive(false);
            HandSwipeDetector.OnSwipe -= OnSwipe;
            if (video != null) video.Pause();
            if (notify) Closed?.Invoke();
        }

        void OnDestroy() { HandSwipeDetector.OnSwipe -= OnSwipe; }

        /// <summary>Close to the chest, below eye level, tilted back like a tablet; the offset is remembered for the follow-me behaviour.</summary>
        void Place()
        {
            var cam = Camera.main; if (cam == null) return;
            Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            root.position = cam.transform.position + f * 0.45f + Vector3.down * 0.20f;
            root.rotation = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(Tilt, 0f, 0f);
            offsetLocal = Quaternion.Inverse(Quaternion.LookRotation(f, Vector3.up)) * (root.position - cam.transform.position);
            followMoving = false;
        }

        // ------------------------------------------------------------------ steps
        string GatherList()
        {
            var nouns = new List<string>();
            foreach (var st in steps) if (st.fromAction && !string.IsNullOrEmpty(st.noun)) nouns.Add(st.noun.Replace(':', ' '));
            if (res.analysis?.objects != null) foreach (var o in res.analysis.objects.Take(4)) nouns.Add(o.noun.Replace(':', ' '));
            return string.Join(", ", nouns.Distinct().Take(5));
        }

        void BuildDots()
        {
            UIKit.ClearChildren(dotsRoot);
            dots = new Image[steps.Count];
            float gap = Mathf.Min(34f, (W - 160f) / Mathf.Max(1, steps.Count));
            for (int i = 0; i < steps.Count; i++)
            {
                var d = UIKit.Box(dotsRoot, "d", Theme.Track, 0, false); d.sprite = Sprites.Circle(); UIKit.TL(d.rectTransform, i * gap, 4, 18, 18); dots[i] = d;
            }
        }

        void BuildStrip()
        {
            for (int i = strip.childCount - 1; i >= 0; i--) { var c = strip.GetChild(i); if (c != playhead.transform) Destroy(c.gameObject); }
            float dur = Mathf.Max(0.1f, res.output.duration_sec);
            for (int si = 0; si < steps.Count; si++)
            {
                var st = steps[si]; int target = si;
                var b = UIKit.Box(strip, "seg", Theme.Orange * new Color(1, 1, 1, 0.85f), 4, true);        // tap a block = jump to that step
                UIKit.TL(b.rectTransform, st.start / dur * StripW, 0, Mathf.Max(14f, (st.end - st.start) / dur * StripW), 24);
                var bt = b.gameObject.AddComponent<Button>(); bt.transition = Selectable.Transition.None; bt.targetGraphic = b; bt.onClick.AddListener(() => { if (steps != null) GoTo(target); });
            }
            playhead.transform.SetAsLastSibling();
        }

        void EnterIntro()
        {
            idx = -1; phase = Phase.Intro; introLeft = 8f;
            var first = steps[0]; if (video.Prepared) { video.Seek(first.start); video.Pause(); }
            Refresh();
        }

        void GoTo(int i)
        {
            idx = Mathf.Clamp(i, 0, steps.Count - 1); phase = Phase.Watch; watchTimer = 0f;
            var st = steps[idx];
            if (video.Prepared) { video.Seek(st.start); if (!paused) video.Play(); }
            Refresh();
        }

        void EnterTurn()
        {
            phase = Phase.Turn; video.Pause();
            var st = steps[idx]; turnTotal = Mathf.Clamp((st.end - st.start) * 1.5f, 6f, 12f); turnLeft = turnTotal;
            Refresh();
        }

        void CompleteStep()
        {
            if (idx >= 0) done[idx] = true;
            if (idx < steps.Count - 1) GoTo(idx + 1);
            else { phase = Phase.Finished; video.Pause(); Refresh(); }
        }

        void Refresh()
        {
            for (int i = 0; i < dots.Length; i++) dots[i].color = done[i] ? Theme.Green : (i == idx && phase != Phase.Finished ? Theme.Orange : Theme.Track);
            UIKit.SetBar(turnBar, 0f);
            doneIcon.sprite = Sprites.IconSprite(phase == Phase.Finished ? Sprites.Icon.Refresh : Sprites.Icon.Check);
            headline.color = Theme.Text;
            if (phase == Phase.Intro || phase == Phase.Finished) { signBg.gameObject.SetActive(false); altBtn[0].gameObject.SetActive(false); altBtn[1].gameObject.SetActive(false); nextText.text = ""; frameOutline.color = Theme.Violet * new Color(1, 1, 1, 0.6f); }
            if (phase == Phase.Intro)
            {
                stepLabel.text = "GET READY"; headline.text = "Gather"; advice.text = gather;
                conf.text = ""; UIKit.SetBar(nowConf, 0f); timeRange.text = ""; capText.text = "Get ready"; hlTag.gameObject.SetActive(false);
                return;
            }
            if (phase == Phase.Finished)
            {
                int n = done.Count(d => d);
                stepLabel.text = ""; headline.text = "Well done"; headline.color = Theme.Green; advice.text = n + " of " + steps.Count + " steps repeated.";
                conf.text = ""; UIKit.SetBar(nowConf, 1f); nowConf.color = Theme.Green; timeRange.text = ""; capText.text = "Well done"; hlTag.gameObject.SetActive(false);
                return;
            }
            var st = steps[idx];
            RefreshOverlay(st);
            stepLabel.text = "STEP " + (idx + 1) + " / " + steps.Count + (phase == Phase.Turn ? "   YOUR TURN" : "");
            headline.text = st.headline; advice.text = st.advice;
            UIKit.SetBar(nowConf, st.score); nowConf.color = Theme.Ramp(st.score);
            conf.text = Mathf.RoundToInt(st.score * 100) + "%";
            timeRange.text = AppState.FormatTime(st.start) + " - " + AppState.FormatTime(st.end);
            capText.text = phase == Phase.Turn ? "YOUR TURN" : st.headline + "   <color=#" + ColorUtility.ToHtmlStringRGB(Theme.Ramp(st.score)) + ">" + Mathf.RoundToInt(st.score * 100) + "%</color>";
            hlTag.gameObject.SetActive(res.output.highlights.Any(h => h.segment[0] <= st.start + 0.5f && st.start <= h.segment[1]));
        }

        // ------------------------------------------------------------------ overlay signs
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

        void RefreshOverlay(CoachStep st)
        {
            bool act = st.fromAction;
            Color c = act ? VerbColor(st.verb) : Theme.Orange;
            signBg.gameObject.SetActive(true); signBg.color = c; signIcon.sprite = Sprites.IconSprite(act ? VerbIcon(st.verb) : Sprites.Icon.Bolt); signIcon.color = Theme.Bg;
            signText.text = st.headline + (act ? "  " + Mathf.RoundToInt(st.share * 100) + "%" : "");
            for (int k = 0; k < 2; k++)
            {
                bool has = act && st.alts.Count > k + 1;
                altBtn[k].gameObject.SetActive(has);
                if (has) altTxt[k].text = "not right? " + st.alts[k + 1].Text;
            }
            nextText.text = idx + 1 < steps.Count ? "NEXT: " + steps[idx + 1].headline : "LAST STEP";
            frameOutline.color = phase == Phase.Turn ? Theme.Orange : (res.output.highlights.Any(h => h.segment[0] <= st.start + 0.5f && st.start <= h.segment[1]) ? Theme.Green : Theme.Violet);
        }

        /// <summary>Tap one of the runner-up readings: it becomes the label of this step (a local correction; the exported JSON is untouched).</summary>
        void PickAlt(int i)
        {
            if (steps == null || idx < 0 || idx >= steps.Count) return;
            steps[idx].Pick(i); Refresh();
        }

        // ------------------------------------------------------------------ controls (touch, pinch or swipe)
        public void Next() { if (steps == null) return; if (phase == Phase.Finished) { GoTo(0); return; } if (phase == Phase.Intro) { GoTo(0); return; } GoTo(idx + 1); }
        public void Prev() { if (steps == null) return; if (phase == Phase.Finished) { GoTo(steps.Count - 1); return; } if (phase == Phase.Intro) { GoTo(0); return; } GoTo(idx - 1); }
        public void Replay() { if (steps == null) return; if (phase == Phase.Intro || phase == Phase.Finished) { GoTo(0); return; } GoTo(idx); }

        /// <summary>DONE: you repeated the step (or skipped the wait) - on to the next one. After the last step it restarts.</summary>
        public void DoneStep()
        {
            if (steps == null) return;
            if (phase == Phase.Finished) { done = new bool[steps.Count]; EnterIntroOrFirst(); return; }
            if (phase == Phase.Intro) { GoTo(0); return; }
            CompleteStep();
        }

        void EnterIntroOrFirst() { if (gather.Length > 0) EnterIntro(); else GoTo(0); }

        void TogglePause()
        {
            paused = !paused;
            if (paused) video.Pause(); else if (phase == Phase.Watch) video.Play();
        }

        void OnSwipe(int dir) { if (!Visible) return; if (dir < 0) Next(); else Prev(); }      // hand swipes left -> next step

        // ------------------------------------------------------------------ live update
        void Update()
        {
            if (!Visible || steps == null || video == null) return;
            if (playIcon != null) playIcon.sprite = Sprites.IconSprite(paused ? Sprites.Icon.Play : Sprites.Icon.Pause);
            FollowMe();
            float dt = Time.unscaledDeltaTime;
            if (video.Prepared)
            {
                float dur = Mathf.Max(0.1f, video.Length);
                UIKit.TL(playhead.rectTransform, Mathf.Clamp01(video.TimeSec / dur) * StripW - 1.5f, -3, 3, 30);
            }
            switch (phase)
            {
                case Phase.Intro:
                    if (paused) break;
                    introLeft -= dt; UIKit.SetBar(turnBar, Mathf.Clamp01(introLeft / 8f));
                    if (introLeft <= 0f) GoTo(0);
                    break;
                case Phase.Watch:
                    if (paused) break;
                    if (video.Prepared && !video.Settled) { watchTimer = 0f; break; }          // wait until the seek has really arrived (the old time would end the clip at once and the parts would "loop")
                    watchTimer += dt;
                    var st = steps[idx];
                    bool clipEnded = video.Prepared ? video.TimeSec >= st.end - 0.05f && watchTimer > 0.6f : watchTimer > (st.end - st.start) + 1.5f;
                    if (video.Prepared && watchTimer > (st.end - st.start) + 4f) clipEnded = true;            // never get stuck on a stalled player
                    if (clipEnded) EnterTurn();
                    break;
                case Phase.Turn:
                    if (paused) break;
                    turnLeft -= dt; UIKit.SetBar(turnBar, Mathf.Clamp01(turnLeft / turnTotal));
                    if (turnLeft <= 0f) CompleteStep();                                                          // on to the next step by itself
                    break;
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
            if (ang > 45f || Vector3.Distance(root.position, desired) > 0.45f) followMoving = true;
            if (!followMoving) return;
            root.position = Vector3.SmoothDamp(root.position, desired, ref followVel, 0.4f);
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
