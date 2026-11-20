using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>
    /// Hand-controlled flow, kept minimal: (1) category carousel (15 categories from manifest.csv), (2) swipeable video carousel + preview + ANALYSE (real trained model),
    /// (3) the "replicate it" overlay (MrHud). Everything is a pinch / finger-poke target; carousels scroll with a pinch-drag, a finger drag or a hand swipe.
    /// The panel is small and close to the head (about 0.75 m) and can be moved with the pinch grips at its sides.
    /// On a headset the videos are streamed from the engine (/media) because the PC's disk is not reachable from the Quest.
    /// </summary>
    public class JourneyController : MonoBehaviour, IVideoHost
    {
        public const float SW = 1700, SH = 940;
        public enum Step { Categories, Videos, Analysing }
        public Step Current { get; private set; } = Step.Categories;
        public VideoController Source { get; private set; }
        public Canvas Stage { get; private set; }
        public bool Running { get; private set; }
        public MrHud Hud { get; private set; }
        public int CategoryCount => catCar != null ? catCar.Count : 0;
        public int FocusIndex => catCar != null ? catCar.Focus : 0;
        public string SelectedCategory => AppState.Category;
        static bool OnHeadset => Application.platform == RuntimePlatform.Android;

        BackendClient client; CanvasGroup stageGroup;
        RectTransform[] stepRoots = new RectTransform[3]; PanelTransition[] stepFx = new PanelTransition[3];
        TextMeshProUGUI statusText; Image statusDot; Button backBtn; Image[] stepDots = new Image[3];
        CanvasGroup toastGroup; TextMeshProUGUI toast; float toastUntil;

        // categories
        CategoryList cats; HCarousel catCar; TextMeshProUGUI catHint;

        // videos
        HCarousel vidCar; readonly List<Image> vidMarks = new List<Image>();
        RawImage previewSurface; TextMeshProUGUI previewHint, vidTime; Slider vidSeek;
        Button analyseBtn; RectTransform fileOverlay; FileBrowserPanel fileBrowser;
        CatVideo selVid; string selPath; CatVideoList videos; float lastSeek;

        // analysis
        Image barFill; TextMeshProUGUI pct, stageMsg; float shown, target;
        ResultEnvelope result;

        void Awake()
        {
            UIKit.CanvasScale = 0.00022f;                      // small panel: 1700 px = about 0.37 m wide
            var pf = GetComponent<PlaceInFront>();             // held close in front of the chest, tilted toward you, within arm's reach for finger touch
            if (pf != null) { pf.near = true; pf.distance = 0.42f; pf.heightOffset = -0.22f; pf.tilt = 25f; }

            client = BackendClient.Ensure();
            var src = new GameObject("SourceVideo"); src.transform.SetParent(transform, false); Source = src.AddComponent<VideoController>();
            var hudGo = new GameObject("HudHost");
            Hud = hudGo.AddComponent<MrHud>(); Hud.Build(Source);
            Hud.Closed += () => { ShowStage(true); SetStep(Step.Videos); };
            if (FindFirstObjectByType<HandOnlyMode>() == null) gameObject.AddComponent<HandOnlyMode>();

            Stage = UIKit.WorldCanvas("Stage", SW, SH, transform);
            stageGroup = Stage.GetComponent<CanvasGroup>();
            BuildFrame();
            BuildCategories(); BuildVideos(); BuildAnalysing(); BuildToast();
            UIKit.SideGrips((RectTransform)Stage.transform, SW, SH, transform);          // pinch a side grip and move your hand to drag the panel
            stageGroup.alpha = 0;
            StartCoroutine(RevealWhenPlaced());
        }

        void OnEnable() { HandSwipeDetector.OnSwipe += OnSwipe; }
        void OnDisable() { HandSwipeDetector.OnSwipe -= OnSwipe; }

        IEnumerator RevealWhenPlaced()
        {
            var pf = GetComponent<PlaceInFront>(); while (pf != null && !pf.Placed) yield return null;
            for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime) { stageGroup.alpha = t / 0.6f; yield return null; }
            stageGroup.alpha = 1f;
        }

        void Start()
        {
            client.OnHealthChanged += OnHealth; OnHealth(client.Online);
            SetStep(Step.Categories, true);
            StartCoroutine(LoadCategories());
        }

        void OnDestroy() { if (client) client.OnHealthChanged -= OnHealth; if (Hud != null) Destroy(Hud.gameObject); }

        void OnHealth(bool online)
        {
            if (statusDot == null) return;
            statusDot.color = online ? Theme.Green : Theme.Magenta;
            statusText.text = online ? (string.IsNullOrEmpty(client.Recognizer) ? "" : "Model: " + client.Recognizer) : "Connecting to the model...";
        }

        /// <summary>Video location for the player: the file itself on a PC, streamed from the engine on a headset.</summary>
        string PlayUrl(string path) { return OnHeadset ? client.BaseUrl + "/media?path=" + UnityWebRequest.EscapeURL(path) : path; }

        // ============================================================ frame
        void BuildFrame()
        {
            var root = (RectTransform)Stage.transform;
            var back = UIKit.Box(root, "Backdrop", new Color(0.03f, 0.05f, 0.12f, 0.80f), 34); UIKit.Fill(back.rectTransform);
            UIKit.Glow(back.transform, Theme.Violet * new Color(1, 1, 1, 0.45f), 34, 40, 30);
            UIKit.Outline(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.5f), 34, 3f);

            var hTitle = UIKit.TextAt(root, "VISTARA", 40, 36, 22, 300, 56, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            hTitle.characterSpacing = 14; hTitle.enableVertexGradient = true; hTitle.colorGradient = new VertexGradient(Theme.Cyan, Theme.Cyan, Theme.Violet, Theme.Magenta);
            backBtn = UIKit.Button(root, "", 360, 22, 80, 56, OnBack, Theme.Card, Theme.Text, Sprites.Icon.ChevronL, 20, 18);
            for (int i = 0; i < 3; i++) { var d = UIKit.Box(root, "Step" + i, Theme.Track, 0, false); d.sprite = Sprites.Circle(); UIKit.TL(d.rectTransform, 760 + i * 44, 42, 18, 18); stepDots[i] = d; }
            statusDot = UIKit.Box(root, "Dot", Theme.Magenta, 0, false); statusDot.sprite = Sprites.Circle(); UIKit.TL(statusDot.rectTransform, SW - 60, 42, 20, 20);
            statusDot.gameObject.AddComponent<Pulse>().alpha = true;
            statusText = UIKit.TextAt(root, "", 20, SW - 460, 22, 390, 56, Theme.TextDim, TextAlignmentOptions.MidlineRight);
            var sep = UIKit.Box(root, "Sep", Theme.Cyan * new Color(1, 1, 1, 0.25f), 0, false); UIKit.TL(sep.rectTransform, 30, 92, SW - 60, 2);
        }

        void BuildToast()
        {
            var root = (RectTransform)Stage.transform;
            var t = UIKit.Box(root, "Toast", new Color(0.05f, 0.08f, 0.2f, 0.96f), 26, false); UIKit.TL(t.rectTransform, SW / 2 - 420, SH - 90, 840, 66);
            UIKit.Outline(t.transform, Theme.Orange, 26, 2f);
            toast = UIKit.TextAt(t.transform, "", 23, 20, 0, 800, 66, Theme.Text, TextAlignmentOptions.Center);
            toastGroup = t.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        }

        public void Toast(string msg, float secs = 3f) { toast.text = msg; toastGroup.alpha = 1; toastUntil = Time.unscaledTime + secs; }

        RectTransform NewStep(Step s)
        {
            var rt = UIKit.Rect(Stage.transform, "Step_" + s); UIKit.TL(rt, 0, 110, SW, SH - 110);
            rt.gameObject.AddComponent<CanvasGroup>();
            stepFx[(int)s] = rt.gameObject.AddComponent<PanelTransition>(); stepRoots[(int)s] = rt;
            return rt;
        }

        // ============================================================ navigation
        public void SetStep(Step s, bool instant = false)
        {
            Current = s;
            for (int i = 0; i < 3; i++)
            {
                bool on = i == (int)s;
                if (stepFx[i] != null) { if (instant) stepFx[i].SetInstant(on); else if (on) stepFx[i].Show(); else if (stepRoots[i].gameObject.activeSelf) stepFx[i].Hide(); }
                stepDots[i].color = i < (int)s ? Theme.Green : (i == (int)s ? Theme.Orange : Theme.Track);
            }
            backBtn.gameObject.SetActive(s != Step.Analysing);
        }

        void OnBack()
        {
            if (Running) return;
            if (fileOverlay != null && fileOverlay.gameObject.activeSelf) { fileOverlay.gameObject.SetActive(false); return; }
            if (Current == Step.Videos) { Source.Pause(); SetStep(Step.Categories); }
            else SceneManager.LoadScene("MainMenu");
        }

        public void ShowStage(bool on) { Stage.gameObject.SetActive(on); }

        void OnSwipe(int dir)
        {
            if (Stage == null || !Stage.gameObject.activeInHierarchy) return;
            if (Current == Step.Categories) catCar.ScrollBy(dir > 0 ? -2 : 2);     // hand moves left -> the carousel advances
            else if (Current == Step.Videos) vidCar.ScrollBy(dir > 0 ? -1 : 1);
        }

        public void ScrollBy(int n) { if (Current == Step.Videos) vidCar.ScrollBy(n); else catCar.ScrollBy(n); }

        // ============================================================ 1. category carousel
        void BuildCategories()
        {
            var root = NewStep(Step.Categories);
            catCar = new HCarousel(this, root, 100, 60, 1500, 580, 330, 520, 28);
            UIKit.Button(root, "", 10, 250, 80, 140, () => catCar.ScrollBy(-1), Theme.Card, Theme.Text, Sprites.Icon.ChevronL, 20, 22);
            UIKit.Button(root, "", SW - 90, 250, 80, 140, () => catCar.ScrollBy(1), Theme.Card, Theme.Text, Sprites.Icon.Chevron, 20, 22);
            catHint = UIKit.TextAt(root, "", 24, 100, 680, 1500, 40, Theme.TextDim, TextAlignmentOptions.Center);
        }

        IEnumerator LoadCategories()
        {
            float t0 = Time.unscaledTime;
            while (!client.Online && Time.unscaledTime - t0 < 180) yield return null;
            if (!client.Online) { catHint.text = "Model engine not reachable at " + client.BaseUrl; yield break; }
            string err = null;
            yield return client.Categories(c => cats = c, e => err = e);
            if (cats == null || cats.items == null) { catHint.text = "Could not load categories: " + err; yield break; }
            catCar.Clear();
            var thumbs = new List<(CategoryInfo, RawImage)>();
            foreach (var c in cats.items) thumbs.Add((c, MakeCategoryCard(c)));
            catHint.text = "";
            yield return null;
            catCar.SnapTo(0, true);
            foreach (var t in thumbs) StartCoroutine(LoadThumb(t.Item1, t.Item2));
        }

        RawImage MakeCategoryCard(CategoryInfo c)
        {
            bool epic = c.dataset == "epic_kitchens_100"; Color acc = epic ? Theme.Orange : Theme.Cyan;
            var info = c;
            var box = catCar.AddCard(acc, () => PickCategory(info));
            var th = UIKit.Rect(box, "Thumb"); UIKit.TL(th, 15, 15, 300, 290);
            var thBg = th.gameObject.AddComponent<Image>(); thBg.color = acc * new Color(1, 1, 1, 0.18f); thBg.raycastTarget = false;
            var ri = UIKit.Rect(th, "Img"); UIKit.Fill(ri); var raw = ri.gameObject.AddComponent<RawImage>(); raw.raycastTarget = false; raw.color = new Color(1, 1, 1, 0);
            UIKit.TextAt(box, c.name.ToUpper(), 36, 22, 322, 286, 56, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold).overflowMode = TextOverflowModes.Overflow;
            UIKit.TextAt(box, c.manifest_count + (c.manifest_count == 1 ? " video" : " videos"), 28, 22, 384, 286, 44, acc, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.TextAt(box, c.available_count + " ready", 22, 22, 432, 286, 36, c.available_count > 0 ? Theme.Green : Theme.TextDim);
            return raw;
        }

        IEnumerator LoadThumb(CategoryInfo c, RawImage raw)
        {
            if (c.available_count <= 0) yield break;
            Texture2D tex = null;
            yield return client.LoadTexture("/thumbnail/category/" + UnityWebRequest.EscapeURL(c.id), t => tex = t);
            if (tex != null && raw != null) { raw.texture = tex; raw.color = Color.white; }
        }

        void PickCategory(CategoryInfo c)
        {
            AppState.Category = c.id; selVid = null; selPath = null; AppState.VideoPath = null; AppState.Result = null;
            vidCar.Clear(); vidMarks.Clear();
            previewHint.gameObject.SetActive(true); previewHint.text = c.available_count > 0 ? "Swipe, then choose a video" : "No video of this category on this PC";
            Source.Pause(); analyseBtn.interactable = false;
            SetStep(Step.Videos);
            StartCoroutine(LoadVideos(c));
        }

        // ============================================================ 2. video carousel + preview
        void BuildVideos()
        {
            var root = NewStep(Step.Videos);
            vidCar = new HCarousel(this, root, 100, 0, 1500, 290, 380, 270, 24);                      // swipe left / right over it, then choose a card
            UIKit.Button(root, "", 10, 70, 80, 140, () => vidCar.ScrollBy(-1), Theme.Card, Theme.Text, Sprites.Icon.ChevronL, 20, 22);
            UIKit.Button(root, "", SW - 90, 70, 80, 140, () => vidCar.ScrollBy(1), Theme.Card, Theme.Text, Sprites.Icon.Chevron, 20, 22);

            var frame = UIKit.Box(root, "Frame", Color.black, 22); UIKit.TL(frame.rectTransform, 250, 310, 800, 450);
            UIKit.Outline(frame.transform, Theme.Cyan * new Color(1, 1, 1, 0.5f), 22, 3f);
            var sr = UIKit.Rect(frame.transform, "Surface"); UIKit.Fill(sr, 5, 5, 5, 5);
            previewSurface = sr.gameObject.AddComponent<RawImage>(); previewSurface.texture = Source.Texture; previewSurface.raycastTarget = false;
            previewHint = UIKit.TextAt(frame.transform, "", 30, 0, 0, 800, 450, Theme.TextDim, TextAlignmentOptions.Center);
            vidSeek = UIKit.Slider(root, 250, 772, 640, 40, 0, 1, 0, v => { if (Time.unscaledTime - lastSeek > 0.08f && Source.Prepared) { lastSeek = Time.unscaledTime; Source.Seek(v * Source.Length); } }, Theme.Orange);
            vidTime = UIKit.TextAt(root, "", 20, 900, 772, 150, 40, Theme.TextDim, TextAlignmentOptions.MidlineRight);

            UIKit.Button(root, "", 1120, 330, 150, 110, () => { if (Source.Prepared) { if (Source.IsPlaying) Source.Pause(); else Source.Play(); } }, Theme.Card, Theme.Text, Sprites.Icon.Play, 20, 22);
            analyseBtn = UIKit.Button(root, "ANALYSE", 1120, 560, 440, 170, StartAnalysis, Theme.Orange, Theme.Bg, Sprites.Icon.Bolt, 36, 28, true);
            analyseBtn.interactable = false;
            if (!OnHeadset) UIKit.Button(root, "", 1290, 330, 150, 110, OpenFileBrowser, Theme.Violet, Theme.Text, Sprites.Icon.Folder, 20, 22);

            // file browser overlay (editor / PC only: choose your own file)
            fileOverlay = UIKit.Rect(Stage.transform, "FileOverlay"); UIKit.TL(fileOverlay, 150, 130, 1400, 860);
            var ob = UIKit.Box(fileOverlay, "Back", new Color(0.03f, 0.05f, 0.14f, 0.98f), 28); UIKit.Fill(ob.rectTransform, -20, -20, -20, -20);
            UIKit.Outline(ob.transform, Theme.Violet, 28, 3f);
            var inner = UIKit.Rect(fileOverlay, "Browser"); UIKit.TL(inner, 0, 70, 1400, 800);
            UIKit.Button(fileOverlay, "", 1320, 0, 80, 56, () => fileOverlay.gameObject.SetActive(false), Theme.Magenta, Theme.Text, Sprites.Icon.Stop, 20, 18);
            fileBrowser = new FileBrowserPanel(this) { showUrlButton = false }; fileBrowser.Build(inner);
            fileOverlay.gameObject.SetActive(false);
        }

        IEnumerator LoadVideos(CategoryInfo c)
        {
            videos = null; string err = null;
            yield return client.CategoryVideos(c.id, v => videos = v, e => err = e);
            if (videos == null) { Toast("Could not list videos: " + err, 5f); yield break; }
            vidCar.Clear(); vidMarks.Clear();
            var thumbs = new List<(string, RawImage)>();
            foreach (var v in videos.items)
            {
                var item = v;
                var card = vidCar.AddCard(Theme.Cyan, () => ChooseVideo(item));
                var th = UIKit.Rect(card, "Th"); UIKit.TL(th, 12, 12, 356, 200);
                var tbg = th.gameObject.AddComponent<Image>(); tbg.color = new Color(0, 0, 0, 0.4f); tbg.raycastTarget = false;
                var ri = UIKit.Rect(th, "Img"); UIKit.Fill(ri); var raw = ri.gameObject.AddComponent<RawImage>(); raw.color = new Color(1, 1, 1, 0); raw.raycastTarget = false;
                UIKit.TextAt(card, AppState.FormatTime(v.duration_sec), 30, 20, 216, 250, 48, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                var mark = UIKit.Box(card, "Mark", Theme.Green, 0, false); mark.sprite = Sprites.IconSprite(Sprites.Icon.Check); UIKit.TL(mark.rectTransform, 318, 220, 40, 40); mark.gameObject.SetActive(false);
                vidMarks.Add(mark);
                thumbs.Add((v.path, raw));
            }
            yield return null;
            vidCar.SnapTo(0, true);
            foreach (var t in thumbs) StartCoroutine(RowThumb(t.Item2, t.Item1));
        }

        IEnumerator RowThumb(RawImage raw, string path)
        {
            Texture2D tex = null;
            yield return client.LoadTexture("/thumbnail?path=" + UnityWebRequest.EscapeURL(path), t => tex = t);
            if (tex != null && raw != null) { raw.texture = tex; raw.color = Color.white; }
        }

        void ChooseVideo(CatVideo v)
        {
            selVid = v;
            int i = videos.items.ToList().IndexOf(v);
            for (int k = 0; k < vidMarks.Count; k++) vidMarks[k].gameObject.SetActive(k == i);
            ApplySelection(v.path);
        }

        void OpenFileBrowser() { fileOverlay.gameObject.SetActive(true); fileBrowser.OnShow(); }

        // IVideoHost (used by the PC file browser)
        void IVideoHost.SelectVideo(string path)
        {
            fileOverlay.gameObject.SetActive(false);
            selVid = videos?.items?.FirstOrDefault(x => string.Equals(x.path, path, StringComparison.OrdinalIgnoreCase));
            for (int k = 0; k < vidMarks.Count; k++) vidMarks[k].gameObject.SetActive(false);
            ApplySelection(path);
        }
        void IVideoHost.StartUrlAnalysis(string url) { }

        void ApplySelection(string path)
        {
            selPath = path; AppState.VideoPath = path;
            previewHint.gameObject.SetActive(false);
            Source.Load(PlayUrl(path), true);                   // plays as soon as it is chosen
            StartCoroutine(PlayableFallback(path));
            analyseBtn.interactable = true;
        }

        /// <summary>If Unity cannot prepare the file (e.g. video-only YouTube downloads), play a transcoded copy from the engine; analysis still uses the original.</summary>
        IEnumerator PlayableFallback(string path)
        {
            float t = 0f;
            while (!Source.Prepared && t < 9f && selPath == path) { t += Time.unscaledDeltaTime; yield return null; }
            if (Source.Prepared || selPath != path || !client.Online) yield break;
            Debug.Log("[Vistara] preview: original does not play in Unity, asking the engine for a playable copy of " + Path.GetFileName(path));
            string copy = null;
            yield return client.Playable(path, p => copy = p);
            if (copy != null && selPath == path) Source.Load(PlayUrl(copy), true);
        }

        // ============================================================ 3. analysis (real engine) -> replicate overlay
        void BuildAnalysing()
        {
            var root = NewStep(Step.Analysing);
            pct = UIKit.TextAt(root, "0%", 150, 0, 150, SW, 200, Theme.Cyan, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Bar(root, 350, 400, 1000, 30, Theme.Cyan, out barFill);
            stageMsg = UIKit.TextAt(root, "", 30, 0, 460, SW, 50, Theme.TextDim, TextAlignmentOptions.Center);
        }

        public void StartAnalysis()
        {
            if (Running) return;
            if (string.IsNullOrEmpty(selPath)) { Toast("Select a video first."); return; }
            if (!OnHeadset && !File.Exists(selPath)) { Toast("Video not found."); return; }
            if (!client.Online) { Toast("Model engine not ready yet."); return; }
            StartCoroutine(RunAnalysis());
        }

        IEnumerator RunAnalysis()
        {
            Running = true; Source.Pause(); shown = target = 0; SetBar(0);
            SetStep(Step.Analysing);
            stageMsg.text = "Starting";
            string jobId = null, err = null;
            if (selVid != null || OnHeadset) yield return client.AnalyzePath(selPath, id => jobId = id, e => err = e);   // the engine reads manifest videos from its own disk
            else yield return client.Analyze(selPath, id => jobId = id, e => err = e);                                    // your own file: uploaded to the local engine
            if (jobId == null) { Fail(err ?? "Could not start the analysis"); yield break; }
            AppState.JobId = jobId;
            ResultEnvelope res = null;
            yield return client.Poll(jobId, s => { target = Mathf.Clamp01(s.progress); stageMsg.text = StageName(s.stage); }, r => res = r, e => err = e);
            if (res == null) { Fail(err ?? "Analysis failed"); yield break; }
            result = res; AppState.Result = res; target = shown = 1; SetBar(1); pct.text = "100%";
            Running = false;
            OpenHud();
        }

        static string StageName(string s)
        {
            switch (s)
            {
                case "decode": return "Reading the video";
                case "embed": return "Understanding the scene";
                case "recognise": return "Recognising actions";
                case "importance": return "Scoring importance";
                case "highlights": return "Selecting highlights";
                case "export": return "Finishing";
                default: return "Working";
            }
        }

        void Fail(string msg) { Running = false; Toast("Error: " + msg, 6f); SetStep(Step.Videos); }

        void SetBar(float v) { UIKit.SetBar(barFill, v); }

        // ---- test hooks (editor preview): show a saved result without running the engine
        public string SelectedPath => selPath;
        public void TestShowResult(ResultEnvelope r) { result = r; AppState.Result = r; Running = false; OpenHud(); }

        public void OpenHud()
        {
            if (result == null) { Toast("Run an analysis first."); return; }
            ShowStage(false);
            Hud.Show(result);
        }

        // ============================================================ per-frame
        void Update()
        {
            if (toastGroup != null && toastGroup.alpha > 0 && Time.unscaledTime > toastUntil) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, 0, Time.unscaledDeltaTime * 2);
            if (client.Online && statusText != null)       // quick diagnosis on the headset: are hands being tracked?
                statusText.text = HandTouchInput.Instance != null && HandTouchInput.Instance.HandsTracked ? "" : "No hands detected";
            if (Current == Step.Categories) catCar.Tick();
            if (Current == Step.Videos)
            {
                vidCar.Tick();
                if (Source.Prepared) { vidSeek.SetValueWithoutNotify(Source.Time01); vidTime.text = AppState.FormatTime(Source.TimeSec) + " / " + AppState.FormatTime(Source.Length); }
                if (Source.Error != null) { previewHint.gameObject.SetActive(true); previewHint.text = "Preview unavailable - analysis still works"; }
            }
            if (Current == Step.Analysing) { shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 0.6f); SetBar(shown); pct.text = Mathf.RoundToInt(shown * 100) + "%"; }
        }

        // test hooks (used by the automated flow test; they call the same code paths as the pinch buttons)
        public void TestPickCategory(string id) { var c = cats?.items?.FirstOrDefault(x => x.id == id); if (c != null) PickCategory(c); }
        public bool VideosReady => videos != null && Current == Step.Videos && vidCar.Count > 0;
        public bool TestPickFirstVideo(int index = 0) { if (videos == null || videos.count == 0) return false; ChooseVideo(videos.items[Mathf.Clamp(index, 0, videos.count - 1)]); return true; }
        public Vector3 TestFocusedCardPos() { return catCar.FocusedWorld(); }
        public bool CategoriesReady => catCar != null && catCar.Count > 0;
    }
}
