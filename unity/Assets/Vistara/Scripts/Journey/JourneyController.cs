using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>
    /// Hand-controlled flow: Category carousel (15 categories from manifest.csv) -> video selection + preview -> AI analysis with the real trained model
    /// -> results -> floating MR HUD. Built with the Vistara UI kit; every interaction is a pinch-selectable button, a pinch-drag, or a hand swipe.
    /// </summary>
    public class JourneyController : MonoBehaviour, IVideoHost
    {
        public const float SW = 1700, SH = 940;
        public enum Step { Categories, Videos, Analysing, Results }
        public Step Current { get; private set; } = Step.Categories;
        public VideoController Source { get; private set; }
        public Canvas Stage { get; private set; }
        public bool Running { get; private set; }
        public MrHud Hud { get; private set; }
        public int CategoryCount => cards.Count;
        public int FocusIndex => focus;
        public string SelectedCategory => AppState.Category;

        BackendClient client; CanvasGroup stageGroup;
        RectTransform[] stepRoots = new RectTransform[4]; PanelTransition[] stepFx = new PanelTransition[4];
        TextMeshProUGUI title, subtitle, statusText; Image statusDot; Button backBtn; Image[] stepDots = new Image[4];
        CanvasGroup toastGroup; TextMeshProUGUI toast; float toastUntil;

        // categories
        CategoryList cats; readonly List<CategoryCard> cards = new List<CategoryCard>(); RectTransform catContent, catViewport; TextMeshProUGUI catPos, catHint;
        int focus; Coroutine snapCo;
        const float CardW = 330, CardH = 560, Gap = 28, ViewW = 1500, Pad = (ViewW - CardW) / 2f;
        class CategoryCard { public CategoryInfo info; public RectTransform rt; public CanvasGroup cg; public RawImage thumb; }

        // videos
        RectTransform videoList; TextMeshProUGUI vidHeader, vidInfo, vidTime; RawImage previewSurface; TextMeshProUGUI previewHint;
        Slider vidSeek; Image playIcon; Button analyseBtn; RectTransform fileOverlay; FileBrowserPanel fileBrowser;
        CatVideo selVid; string selPath; CatVideoList videos; float lastSeek;

        // analysis
        Image barFill; TextMeshProUGUI pct, stageMsg, anaTitle; float shown, target; Image[] stageDots; TextMeshProUGUI[] stageNames;
        static readonly string[] Stages = { "decode", "embed", "recognise", "importance", "highlights", "export" };
        static readonly string[] StageTitles = { "Decode + motion", "CLIP embeddings", "Action recognition", "Importance scoring", "Highlight selection", "Render reel" };

        // results
        RectTransform resActions, resObjects, resHighlights; TextMeshProUGUI resSummary, resBudget, resMatch; Image resBudgetFill; ResultEnvelope result;

        void Awake()
        {
            client = BackendClient.Ensure();
            var src = new GameObject("SourceVideo"); src.transform.SetParent(transform, false); Source = src.AddComponent<VideoController>();
            var hudGo = new GameObject("HudHost");
            Hud = hudGo.AddComponent<MrHud>(); Hud.Build(Source);
            Hud.Closed += () => { ShowStage(true); SetStep(Step.Results); };
            if (FindFirstObjectByType<HandOnlyMode>() == null) gameObject.AddComponent<HandOnlyMode>();

            Stage = UIKit.WorldCanvas("Stage", SW, SH, transform);
            stageGroup = Stage.GetComponent<CanvasGroup>();
            BuildFrame();
            BuildCategories(); BuildVideos(); BuildAnalysing(); BuildResults(); BuildToast();
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
            statusText.text = online ? (client.Cuda ? "Model online (GPU)" : "Model online (CPU)") : "Starting model...";
        }

        // ============================================================ frame (header, step dots, back, toast)
        void BuildFrame()
        {
            var root = (RectTransform)Stage.transform;
            var back = UIKit.Box(root, "Backdrop", new Color(0.03f, 0.05f, 0.12f, 0.80f), 34); UIKit.Fill(back.rectTransform);
            UIKit.Glow(back.transform, Theme.Violet * new Color(1, 1, 1, 0.45f), 34, 40, 30);
            UIKit.Outline(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.5f), 34, 3f);

            var hTitle = UIKit.TextAt(root, "VISTARA", 40, 36, 22, 300, 56, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            hTitle.characterSpacing = 14; hTitle.enableVertexGradient = true; hTitle.colorGradient = new VertexGradient(Theme.Cyan, Theme.Cyan, Theme.Violet, Theme.Magenta);
            backBtn = UIKit.Button(root, "Back", 360, 22, 160, 56, OnBack, Theme.Card, Theme.Text, Sprites.Icon.ChevronL, 22, 18);
            title = UIKit.TextAt(root, "Choose a category", 30, 550, 22, 560, 56, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            string[] st = { "Category", "Video", "Analyse", "Results" };
            for (int i = 0; i < 4; i++)
            {
                var d = UIKit.Box(root, "Step" + i, Theme.Track, 0, false); d.sprite = Sprites.Circle(); UIKit.TL(d.rectTransform, 1130 + i * 108, 42, 16, 16); stepDots[i] = d;
                UIKit.TextAt(root, st[i], 15, 1150 + i * 108, 30, 90, 40, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            }
            statusDot = UIKit.Box(root, "Dot", Theme.Magenta, 0, false); statusDot.sprite = Sprites.Circle(); UIKit.TL(statusDot.rectTransform, SW - 250, 40, 20, 20);
            statusDot.gameObject.AddComponent<Pulse>().alpha = true;
            statusText = UIKit.TextAt(root, "Starting model...", 21, SW - 222, 22, 200, 56, Theme.Text, TextAlignmentOptions.MidlineLeft);
            var sep = UIKit.Box(root, "Sep", Theme.Cyan * new Color(1, 1, 1, 0.25f), 0, false); UIKit.TL(sep.rectTransform, 30, 92, SW - 60, 2);
            subtitle = UIKit.TextAt(root, "", 22, 40, 100, SW - 80, 36, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
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
            var rt = UIKit.Rect(Stage.transform, "Step_" + s); UIKit.TL(rt, 0, 140, SW, SH - 140);
            rt.gameObject.AddComponent<CanvasGroup>();
            stepFx[(int)s] = rt.gameObject.AddComponent<PanelTransition>(); stepRoots[(int)s] = rt;
            return rt;
        }

        // ============================================================ navigation
        public void SetStep(Step s, bool instant = false)
        {
            Current = s;
            for (int i = 0; i < 4; i++)
            {
                bool on = i == (int)s;
                if (stepFx[i] != null) { if (instant) stepFx[i].SetInstant(on); else if (on) stepFx[i].Show(); else if (stepRoots[i].gameObject.activeSelf) stepFx[i].Hide(); }
                stepDots[i].color = i < (int)s ? Theme.Green : (i == (int)s ? Theme.Orange : Theme.Track);
            }
            title.text = new[] { "Choose a category", "Choose a video", "AI analysis", "Results" }[(int)s];
            subtitle.text = s == Step.Categories ? "Swipe or pinch-drag to browse, pinch a card to select.  Source: manifest.csv video ids"
                          : s == Step.Videos ? "Category: " + AppState.Category + "   -   pick a video, check the preview, then press ANALYSE"
                          : s == Step.Analysing ? "Running the trained model on the real video - no mock predictions"
                          : "Detected actions, objects and highlights from the model";
            backBtn.gameObject.SetActive(s != Step.Analysing);
        }

        void OnBack()
        {
            if (Running) return;
            if (fileOverlay != null && fileOverlay.gameObject.activeSelf) { fileOverlay.gameObject.SetActive(false); return; }
            switch (Current)
            {
                case Step.Videos: Source.Pause(); SetStep(Step.Categories); break;
                case Step.Results: SetStep(Step.Videos); break;
                default: SceneManager.LoadScene("MainMenu"); break;
            }
        }

        public void ShowStage(bool on) { Stage.gameObject.SetActive(on); }

        // ============================================================ 1. category carousel
        void BuildCategories()
        {
            var root = NewStep(Step.Categories);
            catViewport = UIKit.Rect(root, "Viewport"); UIKit.TL(catViewport, 100, 30, ViewW, 600);
            catViewport.gameObject.AddComponent<RectMask2D>();
            var bg = catViewport.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.001f);       // raycast target for swipe-drag on empty space
            catContent = UIKit.Rect(catViewport, "Content");
            catContent.anchorMin = new Vector2(0, 1); catContent.anchorMax = new Vector2(0, 1); catContent.pivot = new Vector2(0, 1); catContent.anchoredPosition = Vector2.zero; catContent.sizeDelta = new Vector2(0, 580);
            var h = catContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = Gap; h.padding = new RectOffset((int)Pad, (int)Pad, 10, 10); h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            catContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = catViewport.gameObject.AddComponent<ScrollRect>();
            sr.content = catContent; sr.viewport = catViewport; sr.horizontal = true; sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.inertia = false; sr.scrollSensitivity = 0f;
            catViewport.gameObject.AddComponent<CarouselSnap>().OnEnd = () => SnapTo(NearestIndex());

            UIKit.Button(root, "", 10, 250, 80, 140, () => ScrollBy(-1), Theme.Card, Theme.Text, Sprites.Icon.ChevronL, 20, 22);
            UIKit.Button(root, "", SW - 90, 250, 80, 140, () => ScrollBy(1), Theme.Card, Theme.Text, Sprites.Icon.Chevron, 20, 22);
            catPos = UIKit.TextAt(root, "", 26, SW / 2 - 200, 650, 400, 40, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            catHint = UIKit.TextAt(root, "Waiting for the model engine...", 22, 100, 700, ViewW, 40, Theme.TextDim, TextAlignmentOptions.Center);
        }

        IEnumerator LoadCategories()
        {
            float t0 = Time.unscaledTime;
            while (!client.Online && Time.unscaledTime - t0 < 180) yield return null;
            if (!client.Online) { catHint.text = "Model engine offline - see README (Assets/Model/Engine~)."; yield break; }
            string err = null;
            yield return client.Categories(c => cats = c, e => err = e);
            if (cats == null || cats.items == null) { catHint.text = "Could not load categories: " + err; yield break; }
            UIKit.ClearChildren(catContent); cards.Clear();
            foreach (var c in cats.items) cards.Add(MakeCard(c));
            catHint.text = $"{cats.items.Length} categories from {cats.source}  ({cats.manifest_total} manifest videos)";
            yield return null;
            SnapTo(0, true);
            foreach (var cd in cards) StartCoroutine(LoadThumb(cd));
        }

        CategoryCard MakeCard(CategoryInfo c)
        {
            bool epic = c.dataset == "epic_kitchens_100"; Color acc = epic ? Theme.Orange : Theme.Cyan;
            var box = UIKit.Box(catContent, "Card_" + c.id, Theme.Panel, 26);
            UIKit.Sized(box.gameObject, CardH, CardW); box.rectTransform.sizeDelta = new Vector2(CardW, CardH);
            var rt = box.rectTransform; var cg = box.gameObject.AddComponent<CanvasGroup>();
            UIKit.Outline(box.transform, acc * new Color(1, 1, 1, 0.7f), 26, 2.5f);
            var btn = box.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.targetGraphic = box;
            box.gameObject.AddComponent<HoverFx>();
            var info = c; btn.onClick.AddListener(() => PickCategory(info));

            var th = UIKit.Rect(box.transform, "Thumb"); UIKit.TL(th, 15, 15, CardW - 30, 250);
            var thBg = th.gameObject.AddComponent<Image>(); thBg.color = acc * new Color(1, 1, 1, 0.18f); thBg.raycastTarget = false;
            var ri = UIKit.Rect(th, "Img"); UIKit.Fill(ri); var raw = ri.gameObject.AddComponent<RawImage>(); raw.raycastTarget = false; raw.color = new Color(1, 1, 1, 0);
            UIKit.TextAt(box.transform, c.name.ToUpper(), 34, 20, 280, CardW - 40, 50, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold).overflowMode = TextOverflowModes.Overflow;
            UIKit.TextAt(box.transform, epic ? "EPIC-KITCHENS-100" : "FPVSum", 18, 20, 332, CardW - 40, 28, acc, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.TextAt(box.transform, c.manifest_count + (c.manifest_count == 1 ? " video" : " videos"), 30, 20, 366, CardW - 40, 44, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.TextAt(box.transform, c.available_count + " available on this PC", 20, 20, 412, CardW - 40, 30, c.available_count > 0 ? Theme.Green : Theme.TextDim);
            var sel = UIKit.Box(box.transform, "Sel", acc, 18, false); UIKit.TL(sel.rectTransform, 20, 470, CardW - 40, 62);
            UIKit.TextAt(sel.transform, c.available_count > 0 ? "PINCH TO SELECT" : "UPLOAD A VIDEO", 22, 0, 0, CardW - 40, 62, Theme.Bg, TextAlignmentOptions.Center, FontStyles.Bold);
            return new CategoryCard { info = c, rt = rt, cg = cg, thumb = raw };
        }

        IEnumerator LoadThumb(CategoryCard cd)
        {
            if (cd.info.available_count <= 0) yield break;
            Texture2D tex = null;
            yield return client.LoadTexture("/thumbnail/category/" + UnityWebRequest_Escape(cd.info.id), t => tex = t);
            if (tex != null && cd.thumb != null) { cd.thumb.texture = tex; cd.thumb.color = Color.white; }
        }

        static string UnityWebRequest_Escape(string s) { return UnityEngine.Networking.UnityWebRequest.EscapeURL(s); }

        float CardCenterX(int i) { return Pad + i * (CardW + Gap) + CardW / 2f; }
        float ContentWidth() { return 2 * Pad + cards.Count * CardW + Mathf.Max(0, cards.Count - 1) * Gap; }
        float TargetX(int i) { return Mathf.Clamp(ViewW / 2f - CardCenterX(i), ViewW - ContentWidth(), 0f); }
        int NearestIndex()
        {
            if (cards.Count == 0) return 0;
            float centre = ViewW / 2f - catContent.anchoredPosition.x;
            return Mathf.Clamp(Mathf.RoundToInt((centre - Pad - CardW / 2f) / (CardW + Gap)), 0, cards.Count - 1);
        }

        public void ScrollBy(int n) { if (cards.Count == 0) return; SnapTo(Mathf.Clamp(NearestIndex() + n, 0, cards.Count - 1)); }

        void SnapTo(int idx, bool instant = false)
        {
            if (cards.Count == 0) return;
            focus = idx;
            if (snapCo != null) StopCoroutine(snapCo);
            if (instant) { catContent.anchoredPosition = new Vector2(TargetX(idx), 0); return; }
            snapCo = StartCoroutine(SnapRoutine(TargetX(idx)));
        }

        IEnumerator SnapRoutine(float x)
        {
            while (Mathf.Abs(catContent.anchoredPosition.x - x) > 0.5f)
            {
                var p = catContent.anchoredPosition; p.x = Mathf.Lerp(p.x, x, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f)); catContent.anchoredPosition = p; yield return null;
            }
            catContent.anchoredPosition = new Vector2(x, 0);
        }

        void OnSwipe(int dir)
        {
            if (Current != Step.Categories || Stage == null || !Stage.gameObject.activeInHierarchy) return;
            ScrollBy(dir > 0 ? -2 : 2);          // hand moves left -> carousel advances to the right
        }

        void PickCategory(CategoryInfo c)
        {
            AppState.Category = c.id; selVid = null; selPath = null; AppState.VideoPath = null; AppState.Result = null;
            vidHeader.text = c.name.ToUpper() + "  -  " + c.available_count + " of " + c.manifest_count + " manifest videos on this PC";
            UIKit.ClearChildren(videoList); vidInfo.text = "No video selected."; previewHint.gameObject.SetActive(true); previewHint.text = "Select a video from the list\nor browse for your own file.";
            Source.Pause(); analyseBtn.interactable = false;
            SetStep(Step.Videos);
            StartCoroutine(LoadVideos(c));
        }

        // ============================================================ 2. video selection + preview
        void BuildVideos()
        {
            var root = NewStep(Step.Videos);
            var left = CardUI.Card(root, "Videos", 40, 10, 470, 760, Theme.Cyan);
            vidHeader = UIKit.TextAt(left, "", 17, 24, 50, 422, 44, Theme.TextDim);
            videoList = UIKit.ScrollList(left, 16, 100, 438, 560, 6, new Color(0, 0, 0, 0.2f));
            UIKit.Button(left, "Browse files...", 16, 676, 438, 70, OpenFileBrowser, Theme.Violet, Theme.Text, Sprites.Icon.Folder, 24, 18);

            var frame = UIKit.Box(root, "Frame", Color.black, 22); UIKit.TL(frame.rectTransform, 540, 10, 760, 428);
            UIKit.Outline(frame.transform, Theme.Cyan * new Color(1, 1, 1, 0.5f), 22, 3f);
            var sr = UIKit.Rect(frame.transform, "Surface"); UIKit.Fill(sr, 5, 5, 5, 5);
            previewSurface = sr.gameObject.AddComponent<RawImage>(); previewSurface.texture = Source.Texture; previewSurface.raycastTarget = false;
            previewHint = UIKit.TextAt(frame.transform, "", 26, 0, 0, 760, 428, Theme.TextDim, TextAlignmentOptions.Center);

            UIKit.Button(root, "", 540, 452, 120, 84, () => { if (Source.Prepared) { if (Source.IsPlaying) Source.Pause(); else Source.Play(); } }, Theme.Cyan, Theme.Bg, Sprites.Icon.Play, 20, 20, true);
            vidSeek = UIKit.Slider(root, 680, 470, 480, 44, 0, 1, 0, v => { if (Time.unscaledTime - lastSeek > 0.08f && Source.Prepared) { lastSeek = Time.unscaledTime; Source.Seek(v * Source.Length); } }, Theme.Orange);
            vidTime = UIKit.TextAt(root, "00:00 / 00:00", 22, 1170, 470, 130, 44, Theme.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            vidInfo = UIKit.TextAt(root, "", 22, 540, 556, 760, 160, Theme.TextDim);

            var right = CardUI.Card(root, "Analyse", 1330, 10, 330, 760, Theme.Orange);
            UIKit.TextAt(right, "The trained model runs on this exact file. Results appear as verb-noun actions, objects, timestamps and highlights, then in the floating MR HUD.", 20, 24, 60, 282, 300, Theme.TextDim);
            analyseBtn = UIKit.Button(right, "ANALYSE", 24, 560, 282, 160, StartAnalysis, Theme.Orange, Theme.Bg, Sprites.Icon.Bolt, 34, 26, true);
            analyseBtn.interactable = false;

            // file browser overlay (upload / select your own file)
            fileOverlay = UIKit.Rect(Stage.transform, "FileOverlay"); UIKit.TL(fileOverlay, 150, 130, 1400, 800 + 60);
            var ob = UIKit.Box(fileOverlay, "Back", new Color(0.03f, 0.05f, 0.14f, 0.98f), 28); UIKit.Fill(ob.rectTransform, -20, -20, -20, -20);
            UIKit.Outline(ob.transform, Theme.Violet, 28, 3f);
            var inner = UIKit.Rect(fileOverlay, "Browser"); UIKit.TL(inner, 0, 70, 1400, 800);
            UIKit.TextAt(fileOverlay, "Select a video file (it should belong to the category you picked)", 24, 0, 6, 1000, 50, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Button(fileOverlay, "Close", 1240, 0, 160, 56, () => fileOverlay.gameObject.SetActive(false), Theme.Magenta, Theme.Text, null, 22, 18);
            fileBrowser = new FileBrowserPanel(this) { showUrlButton = false }; fileBrowser.Build(inner);
            fileOverlay.gameObject.SetActive(false);
        }

        IEnumerator LoadVideos(CategoryInfo c)
        {
            videos = null; string err = null;
            yield return client.CategoryVideos(c.id, v => videos = v, e => err = e);
            if (videos == null) { Toast("Could not list videos: " + err, 5f); yield break; }
            UIKit.ClearChildren(videoList);
            if (videos.count == 0) { vidInfo.text = "No manifest video of this category is on this PC.\nUse Browse files... to pick your own."; }
            foreach (var v in videos.items)
            {
                var item = v;
                var row = UIKit.Box(videoList, "row", Theme.Card, 12); UIKit.Sized(row.gameObject, 84);
                var b = row.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = row;
                row.gameObject.AddComponent<HoverFx>(); b.onClick.AddListener(() => ChooseVideo(item));
                var th = UIKit.Rect(row.transform, "Th"); UIKit.TL(th, 10, 10, 110, 64);
                var tbg = th.gameObject.AddComponent<Image>(); tbg.color = new Color(0, 0, 0, 0.4f); tbg.raycastTarget = false;
                var ri = UIKit.Rect(th, "Img"); UIKit.Fill(ri); var raw = ri.gameObject.AddComponent<RawImage>(); raw.color = new Color(1, 1, 1, 0); raw.raycastTarget = false;
                UIKit.TextAt(row.transform, v.video_id, 20, 132, 6, 220, 40, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                UIKit.TextAt(row.transform, AppState.FormatTime(v.duration_sec) + "   " + SizeStr(v.path), 18, 132, 44, 280, 32, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
                StartCoroutine(RowThumb(raw, v.path));
            }
        }

        IEnumerator RowThumb(RawImage raw, string path)
        {
            Texture2D tex = null;
            yield return client.LoadTexture("/thumbnail?path=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(path), t => tex = t);
            if (tex != null && raw != null) { raw.texture = tex; raw.color = Color.white; }
        }

        static string SizeStr(string p) { try { long b = new FileInfo(p).Length; return (b / (1024f * 1024f)).ToString("0") + " MB"; } catch { return ""; } }

        void ChooseVideo(CatVideo v) { selVid = v; ApplySelection(v.path, true); }

        void OpenFileBrowser() { fileOverlay.gameObject.SetActive(true); fileBrowser.OnShow(); }

        // IVideoHost (used by the file browser)
        void IVideoHost.SelectVideo(string path)
        {
            fileOverlay.gameObject.SetActive(false);
            selVid = videos?.items?.FirstOrDefault(x => string.Equals(x.path, path, StringComparison.OrdinalIgnoreCase));
            ApplySelection(path, selVid != null);
        }
        void IVideoHost.StartUrlAnalysis(string url) { Toast("Link import is available in the Workspace scene.", 3f); }

        void ApplySelection(string path, bool belongs)
        {
            selPath = path; AppState.VideoPath = path;
            previewHint.gameObject.SetActive(false);
            Source.Load(path);
            StartCoroutine(PlayableFallback(path));
            analyseBtn.interactable = true;
            vidInfo.text = (belongs ? "<color=#3DFFA6>Manifest video of category " + AppState.Category + "</color>\n"
                                    : "<color=#FF8A1F>Your own file: the model decides the category itself.</color>\n")
                         + Path.GetFileName(path) + "\n" + SizeStr(path);
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
            if (copy != null && File.Exists(copy) && selPath == path) Source.Load(copy);
        }

        // ============================================================ 3. analysis (real engine)
        void BuildAnalysing()
        {
            var root = NewStep(Step.Analysing);
            var c = CardUI.Card(root, "Trained model running", 360, 30, 980, 700, Theme.Cyan);
            pct = UIKit.TextAt(c, "0%", 120, 0, 70, 980, 160, Theme.Cyan, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Bar(c, 90, 250, 800, 28, Theme.Cyan, out barFill);
            stageMsg = UIKit.TextAt(c, "Waiting...", 24, 90, 296, 800, 40, Theme.TextDim, TextAlignmentOptions.Center);
            stageDots = new Image[Stages.Length]; stageNames = new TextMeshProUGUI[Stages.Length];
            for (int i = 0; i < Stages.Length; i++)
            {
                var d = UIKit.Box(c, "d", Theme.Track, 0, false); d.sprite = Sprites.Circle(); UIKit.TL(d.rectTransform, 300, 372 + i * 48, 24, 24); stageDots[i] = d;
                stageNames[i] = UIKit.TextAt(c, StageTitles[i], 26, 346, 366 + i * 48, 500, 36, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            }
        }

        public void StartAnalysis()
        {
            if (Running) return;
            if (string.IsNullOrEmpty(selPath) || !File.Exists(selPath)) { Toast("Select a video first."); return; }
            if (!client.Online) { Toast("Model engine not ready yet."); return; }
            StartCoroutine(RunAnalysis());
        }

        IEnumerator RunAnalysis()
        {
            Running = true; Source.Pause(); shown = target = 0; SetBar(0);
            for (int i = 0; i < Stages.Length; i++) { stageDots[i].color = Theme.Track; stageNames[i].color = Theme.TextDim; }
            SetStep(Step.Analysing);
            string jobId = null, err = null;
            stageMsg.text = "Starting...";
            if (selVid != null) yield return client.AnalyzePath(selPath, id => jobId = id, e => err = e);   // manifest video: the engine reads it from disk
            else yield return client.Analyze(selPath, id => jobId = id, e => err = e);                       // your own file: uploaded to the local engine
            if (jobId == null) { Fail(err ?? "Could not start the analysis"); yield break; }
            AppState.JobId = jobId;
            ResultEnvelope res = null;
            yield return client.Poll(jobId, s =>
            {
                target = Mathf.Clamp01(s.progress); stageMsg.text = s.message;
                int cur = Array.IndexOf(Stages, s.stage);
                for (int i = 0; i < Stages.Length; i++) { stageDots[i].color = i < cur ? Theme.Green : (i == cur ? Theme.Orange : Theme.Track); stageNames[i].color = i <= cur ? Theme.Text : Theme.TextDim; }
            }, r => res = r, e => err = e);
            if (res == null) { Fail(err ?? "Analysis failed"); yield break; }
            result = res; AppState.Result = res; target = shown = 1; SetBar(1); pct.text = "100%";
            FillResults(res);
            Running = false;
            SetStep(Step.Results);
        }

        void Fail(string msg) { Running = false; Toast("Error: " + msg, 6f); SetStep(Step.Videos); }

        void SetBar(float v) { UIKit.SetBar(barFill, v); }

        // ============================================================ 4. results
        void BuildResults()
        {
            var root = NewStep(Step.Results);
            var a = CardUI.Card(root, "Detected actions  (time, confidence)", 40, 10, 560, 700, Theme.Magenta);
            resActions = UIKit.ScrollList(a, 16, 60, 528, 624, 6, new Color(0, 0, 0, 0.2f));
            var o = CardUI.Card(root, "Detected objects + activities", 620, 10, 520, 700, Theme.Green);
            resSummary = UIKit.TextAt(o, "", 22, 24, 52, 472, 92, Theme.Text);
            resMatch = UIKit.TextAt(o, "", 19, 24, 146, 472, 52, Theme.TextDim);
            resObjects = UIKit.Rect(o, "Objs"); UIKit.TL(resObjects, 20, 210, 480, 470);
            var h = CardUI.Card(root, "Highlights", 1160, 10, 500, 700, Theme.Orange);
            resBudget = UIKit.TextAt(h, "", 20, 24, 52, 452, 56, Theme.Text);
            UIKit.Bar(h, 24, 118, 452, 18, Theme.Orange, out resBudgetFill);
            resHighlights = UIKit.ScrollList(h, 16, 156, 468, 528, 6, new Color(0, 0, 0, 0.2f));
            UIKit.Button(root, "ENTER MR HUD", 40, 730, 760, 100, OpenHud, Theme.Cyan, Theme.Bg, Sprites.Icon.Bolt, 38, 30, true);
            UIKit.Button(root, "Analyse another video", 830, 730, 400, 100, () => SetStep(Step.Videos), Theme.Card, Theme.Text, Sprites.Icon.Film, 24, 30);
            UIKit.Button(root, "Choose another category", 1250, 730, 410, 100, () => SetStep(Step.Categories), Theme.Card, Theme.Text, Sprites.Icon.Folder, 24, 30);
        }

        void FillResults(ResultEnvelope r)
        {
            var o = r.output; var a = r.analysis;
            float kept = o.highlights.Sum(x => x.segment[1] - x.segment[0]); float frac = kept / Mathf.Max(0.01f, o.duration_sec);
            resSummary.text = $"Model category: <color=#19E3FF>{o.label.name}</color> ({o.label.score:0.00})\n{o.actions.Length} actions, {o.highlights.Length} highlights, {a?.timings?.total_s:0.0}s on {a?.device}";
            string want = AppState.Category == "Kitchen" ? "cooking" : (AppState.Category ?? "").ToLowerInvariant();
            resMatch.text = string.IsNullOrEmpty(want) ? "" : (o.label.name == want ? "<color=#3DFFA6>Matches the category you selected.</color>" : $"<color=#FF8A1F>You selected {AppState.Category}, the model says {o.label.name}.</color>");
            resBudget.text = $"{AppState.FormatTime(kept)} of {AppState.FormatTime(o.duration_sec)} = {frac * 100:0.0}%   (budget 15%)";
            UIKit.SetBar(resBudgetFill, Mathf.Clamp01(frac / 0.15f)); resBudgetFill.color = frac <= 0.15f ? Theme.Green : Theme.Magenta;

            UIKit.ClearChildren(resActions);
            if (o.actions.Length == 0) UIKit.TextAt(resActions, "No verb-noun actions for this footage type\n(the engine reports them for kitchen video).", 20, 10, 10, 500, 120, Theme.TextDim);
            foreach (var ac in o.actions.OrderBy(x => x.Start).Take(160))
            {
                var act = ac; var row = UIKit.Box(resActions, "row", Theme.Card, 12); UIKit.Sized(row.gameObject, 52);
                UIKit.TextAt(row.transform, AppState.FormatTime(act.Start) + "-" + AppState.FormatTime(act.End), 18, 10, 0, 130, 52, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                UIKit.TextAt(row.transform, act.verb.Replace('-', ' ') + " " + act.noun.Replace(':', ' '), 20, 144, 0, 300, 52, Theme.Text, TextAlignmentOptions.MidlineLeft);
                UIKit.Bar(row.transform, 452, 19, 56, 14, Theme.Ramp(act.score), out var f); UIKit.SetBar(f, act.score);
            }
            UIKit.ClearChildren(resObjects);
            float y = 0;
            if (a?.verbs != null && a.verbs.Length > 0)
            {
                float max = a.verbs.Max(v => v.seconds);
                for (int i = 0; i < Mathf.Min(5, a.verbs.Length); i++, y += 40)
                {
                    var v = a.verbs[i];
                    UIKit.TextAt(resObjects, v.verb, 20, 0, y, 150, 34, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                    UIKit.Bar(resObjects, 150, y + 8, 250, 18, Theme.Orange, out var f); UIKit.SetBar(f, v.seconds / max);
                    UIKit.TextAt(resObjects, v.seconds.ToString("0") + "s", 18, 410, y, 60, 34, Theme.TextDim, TextAlignmentOptions.MidlineRight);
                }
                y += 14;
            }
            if (a?.objects != null)
            {
                float x = 0;
                foreach (var ob in a.objects.Take(10))
                {
                    string t = $"{ob.noun.Replace(':', ' ')}  {ob.seconds:0}s"; float w = 30 + t.Length * 11.5f;
                    if (x + w > 470) { x = 0; y += 50; }
                    var chip = UIKit.Box(resObjects, "chip", Theme.Green * new Color(1, 1, 1, 0.16f), 20, false); UIKit.TL(chip.rectTransform, x, y, w, 42);
                    UIKit.Outline(chip.transform, Theme.Green * new Color(1, 1, 1, 0.7f), 20, 2f);
                    UIKit.TextAt(chip.transform, t, 19, 0, 0, w, 42, Theme.Text, TextAlignmentOptions.Center);
                    x += w + 10;
                }
            }
            UIKit.ClearChildren(resHighlights);
            foreach (var h in o.highlights)
            {
                var seg = h.segment; var row = UIKit.Box(resHighlights, "seg", Theme.Card, 12); UIKit.Sized(row.gameObject, 52);
                UIKit.TextAt(row.transform, AppState.FormatTime(seg[0]) + " - " + AppState.FormatTime(seg[1]) + "    " + (seg[1] - seg[0]).ToString("0.0") + " s", 20, 14, 0, 440, 52, Theme.Orange, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            }
        }

        public void OpenHud()
        {
            if (result == null) { Toast("Run an analysis first."); return; }
            ShowStage(false);
            Hud.Show(result);
            if (Source.Prepared) { Source.Seek(0f); Source.Play(); }
        }

        // ============================================================ per-frame
        void Update()
        {
            if (toastGroup != null && toastGroup.alpha > 0 && Time.unscaledTime > toastUntil) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, 0, Time.unscaledDeltaTime * 2);
            if (Current == Step.Categories && cards.Count > 0)
            {
                int near = NearestIndex();
                for (int i = 0; i < cards.Count; i++)
                {
                    float s = Mathf.Lerp(cards[i].rt.localScale.x, i == near ? 1.06f : 0.92f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
                    cards[i].rt.localScale = Vector3.one * s; cards[i].cg.alpha = Mathf.Lerp(cards[i].cg.alpha, i == near ? 1f : 0.72f, 0.2f);
                }
                catPos.text = $"{near + 1} / {cards.Count}   {cards[near].info.name}";
            }
            if (Current == Step.Analysing) { shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 0.6f); SetBar(shown); pct.text = Mathf.RoundToInt(shown * 100) + "%"; }
            if (Current == Step.Videos && Source != null)
            {
                if (Source.Prepared) { vidSeek.SetValueWithoutNotify(Source.Time01); vidTime.text = AppState.FormatTime(Source.TimeSec) + " / " + AppState.FormatTime(Source.Length); }
                if (Source.Error != null) { previewHint.gameObject.SetActive(true); previewHint.text = "Preview unavailable in Unity's player\n(" + Source.Error + ")\nAnalysis still works."; }
            }
        }

        // test hooks (used by the automated flow test; they call the same code paths as the pinch buttons)
        public void TestPickCategory(string id) { var c = cats?.items?.FirstOrDefault(x => x.id == id); if (c != null) PickCategory(c); }
        public bool VideosReady => videos != null && Current == Step.Videos;
        public bool TestPickFirstVideo(int index = 0) { if (videos == null || videos.count == 0) return false; ChooseVideo(videos.items[Mathf.Clamp(index, 0, videos.count - 1)]); return true; }
        public bool CategoriesReady => cards.Count > 0;
    }

    /// <summary>Raises a callback when a drag on the carousel ends so it can snap to the nearest card.</summary>
    public class CarouselSnap : MonoBehaviour, IEndDragHandler
    {
        public Action OnEnd;
        public void OnEndDrag(PointerEventData e) { OnEnd?.Invoke(); }
    }
}
