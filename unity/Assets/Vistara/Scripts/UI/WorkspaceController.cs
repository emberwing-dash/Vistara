using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>
    /// Workspace scene: nav rail + 5 tabs (Library / Player / Analysis / Timeline / Summary) on a world-space stage,
    /// plus a tilted control deck (transport, seek, volume, Analyze). Orchestrates upload -> poll -> result.
    /// </summary>
    public class WorkspaceController : MonoBehaviour, IVideoHost
    {
        public const float SW = 1700, SH = 940;      // stage size (canvas px)
        public const float CX = 270, CY = 118, CW = 1400, CH = 800;   // content area inside stage

        public VideoController Source { get; private set; }
        public VideoController Highlight { get; private set; }
        public Canvas Stage { get; private set; }
        public Canvas Deck { get; private set; }

        public FileBrowserPanel Library; public PlayerPanel Player; public AnalysisPanel Analysis; public TimelinePanel Timeline; public SummaryPanel Summary; public TestLabPanel TestLab;
        const int NTabs = 6;
        RectTransform[] tabRoots = new RectTransform[NTabs];
        PanelTransition[] tabFx = new PanelTransition[NTabs];
        Button[] navButtons = new Button[NTabs];
        Image[] navBg = new Image[NTabs];
        public Workspace.Tab Current { get; private set; } = Workspace.Tab.Library;

        TextMeshProUGUI headerTitle, headerFile, statusText, toast;
        Image statusDot;
        CanvasGroup toastGroup;
        BackendClient client;
        Coroutine runCo;

        // deck
        Slider seek, volume; TextMeshProUGUI timeLabel; Image playIcon; Button analyzeBtn; TextMeshProUGUI analyzeLabel;
        float lastSeek;
        public bool Running { get; private set; }

        VideoController Active => Current == Workspace.Tab.Summary ? Highlight : Source;

        void Awake()
        {
            client = BackendClient.Ensure();
            var srcGo = new GameObject("SourceVideo"); srcGo.transform.SetParent(transform, false); Source = srcGo.AddComponent<VideoController>();
            var hlGo = new GameObject("HighlightVideo"); hlGo.transform.SetParent(transform, false); Highlight = hlGo.AddComponent<VideoController>();

            Stage = UIKit.WorldCanvas("Stage", SW, SH, transform);
            Stage.transform.localPosition = new Vector3(0, 0.0f, 0);
            Deck = UIKit.WorldCanvas("Deck", 1500, 330, transform);
            Deck.transform.localPosition = new Vector3(0, -0.74f, -0.42f);
            Deck.transform.localRotation = Quaternion.Euler(32f, 0, 0);

            BuildStage();
            BuildDeck();
            Stage.GetComponent<CanvasGroup>().alpha = 0; Deck.GetComponent<CanvasGroup>().alpha = 0;
            StartCoroutine(RevealWhenPlaced());
        }

        IEnumerator RevealWhenPlaced()
        {
            var pf = GetComponent<PlaceInFront>(); while (pf != null && !pf.Placed) yield return null;
            var a = Stage.GetComponent<CanvasGroup>(); var b = Deck.GetComponent<CanvasGroup>();
            for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime) { a.alpha = b.alpha = t / 0.6f; yield return null; }
            a.alpha = b.alpha = 1f;
        }

        void Start()
        {
            client.OnHealthChanged += OnHealth; OnHealth(client.Online);
            Source.OnPrepared += () => { Player.OnSourcePrepared(); Timeline.OnSourcePrepared(); };
            Highlight.OnPrepared += () => Summary.OnHighlightPrepared();

            if (AppState.HasVideo) { Source.Load(AppState.VideoPath); Player.SetVideo(AppState.VideoPath); }
            if (AppState.HasResult)
            {
                ApplyResult(AppState.Result, false);
            }
            SwitchTab(AppState.PendingTab == Workspace.Tab.Library && AppState.HasVideo && !AppState.HasResult ? Workspace.Tab.Player : AppState.PendingTab, true);
        }

        void OnDestroy() { if (client) client.OnHealthChanged -= OnHealth; }

        // ============================================================ stage
        void BuildStage()
        {
            var root = (RectTransform)Stage.transform;
            var back = UIKit.Box(root, "Backdrop", new Color(0.03f, 0.05f, 0.12f, 0.80f), 34);
            UIKit.Fill(back.rectTransform);
            UIKit.Glow(back.transform, Theme.Violet * new Color(1, 1, 1, 0.45f), 34, 40, 30);
            UIKit.Outline(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.5f), 34, 3f);

            // header
            var hTitle = UIKit.TextAt(root, "VISTARA", 40, 36, 24, 320, 56, Theme.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            hTitle.characterSpacing = 14; hTitle.enableVertexGradient = true; hTitle.colorGradient = new VertexGradient(Theme.Cyan, Theme.Cyan, Theme.Violet, Theme.Magenta);
            headerTitle = UIKit.TextAt(root, "Library", 30, CX, 24, 520, 56, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            headerFile = UIKit.TextAt(root, "", 22, CX + 440, 24, 640, 56, Theme.TextDim, TextAlignmentOptions.MidlineRight);
            statusDot = UIKit.Box(root, "Dot", Theme.Magenta, 0, false); statusDot.sprite = Sprites.Circle(); UIKit.TL(statusDot.rectTransform, SW - 250, 42, 20, 20);
            statusDot.gameObject.AddComponent<Pulse>().alpha = true;
            statusText = UIKit.TextAt(root, "Engine offline", 22, SW - 222, 24, 200, 56, Theme.Text, TextAlignmentOptions.MidlineLeft);
            var sep = UIKit.Box(root, "Sep", Theme.Cyan * new Color(1, 1, 1, 0.25f), 0, false); UIKit.TL(sep.rectTransform, 30, 96, SW - 60, 2);

            // nav rail
            string[] names = { "Library", "Player", "Analysis", "Timeline", "Summary", "Test Lab" };
            Sprites.Icon[] icons = { Sprites.Icon.Folder, Sprites.Icon.Film, Sprites.Icon.Bolt, Sprites.Icon.Chart, Sprites.Icon.Check, Sprites.Icon.Refresh };
            for (int i = 0; i < NTabs; i++)
            {
                int idx = i;
                var b = UIKit.Button(root, names[i], 30, CY + i * 90, 214, 76, () => SwitchTab((Workspace.Tab)idx), Theme.Card, Theme.Text, icons[i], 25, 22);
                navButtons[i] = b; navBg[i] = b.GetComponent<Image>();
            }
            UIKit.Button(root, "Home", 30, SH - 190, 214, 66, GoHome, Theme.Card, Theme.TextDim, Sprites.Icon.Home, 22, 20);
            UIKit.Button(root, "Re-center", 30, SH - 112, 214, 66, () => GetComponent<PlaceInFront>()?.Recenter(), Theme.Card, Theme.TextDim, Sprites.Icon.Refresh, 22, 20);

            // tabs
            Library = new FileBrowserPanel(this); Player = new PlayerPanel(this); Analysis = new AnalysisPanel(this); Timeline = new TimelinePanel(this); Summary = new SummaryPanel(this); TestLab = new TestLabPanel(this);
            var panels = new IPanel[] { Library, Player, Analysis, Timeline, Summary, TestLab };
            for (int i = 0; i < NTabs; i++)
            {
                var rt = UIKit.Rect(root, "Tab_" + names[i]);
                UIKit.TL(rt, CX, CY, CW, CH);
                rt.gameObject.AddComponent<CanvasGroup>();
                tabFx[i] = rt.gameObject.AddComponent<PanelTransition>();
                tabRoots[i] = rt;
                panels[i].Build(rt);
                tabFx[i].SetInstant(false);
            }

            // toast
            var t = UIKit.Box(root, "Toast", new Color(0.05f, 0.08f, 0.2f, 0.96f), 26, false);
            UIKit.TL(t.rectTransform, SW / 2 - 420, SH - 100, 840, 70);
            UIKit.Outline(t.transform, Theme.Orange, 26, 2f);
            toast = UIKit.TextAt(t.transform, "", 24, 20, 0, 800, 70, Theme.Text, TextAlignmentOptions.Center);
            toastGroup = t.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        }

        // ============================================================ deck
        void BuildDeck()
        {
            var root = (RectTransform)Deck.transform;
            var back = UIKit.Box(root, "Backdrop", new Color(0.03f, 0.05f, 0.12f, 0.88f), 40);
            UIKit.Fill(back.rectTransform);
            UIKit.Glow(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.35f), 40, 40, 26);
            UIKit.Outline(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.5f), 40, 3f);
            UIKit.TextAt(root, "VIDEO CONTROLS", 22, 0, 12, 1500, 30, Theme.TextDim, TextAlignmentOptions.Center, FontStyles.Bold).characterSpacing = 8;

            UIKit.Button(root, "Library", 36, 52, 190, 62, () => SwitchTab(Workspace.Tab.Library), Theme.Card, Theme.Text, Sprites.Icon.Folder, 22, 18);
            analyzeBtn = UIKit.Button(root, "ANALYZE", 1274, 52, 190, 62, StartAnalysis, Theme.Orange, Theme.Bg, Sprites.Icon.Bolt, 24, 18, true);
            analyzeLabel = analyzeBtn.GetComponentInChildren<TextMeshProUGUI>();

            timeLabel = UIKit.TextAt(root, "00:00 / 00:00", 26, 560, 56, 380, 50, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            seek = UIKit.Slider(root, 60, 118, 1380, 40, 0, 1, 0, v => { if (Time.unscaledTime - lastSeek > 0.08f) { lastSeek = Time.unscaledTime; var a = Active; if (a.Prepared) a.Seek(v * a.Length); } }, Theme.Orange);

            float y = 186, s = 96, g = 26, x0 = 90;
            UIKit.Button(root, "", x0, y, s + 20, s, () => Active.SeekRelative(-10), Theme.Card, Theme.Text, Sprites.Icon.Back, 20, 26);
            UIKit.Button(root, "", x0 + (s + 20 + g), y, s + 20, s, () => Active.Play(), Theme.Cyan, Theme.Bg, Sprites.Icon.Play, 20, 26, true);
            UIKit.Button(root, "", x0 + 2 * (s + 20 + g), y, s + 20, s, () => Active.Pause(), Theme.Card, Theme.Text, Sprites.Icon.Pause, 20, 26);
            UIKit.Button(root, "", x0 + 3 * (s + 20 + g), y, s + 20, s, () => Active.SeekRelative(10), Theme.Card, Theme.Text, Sprites.Icon.Forward, 20, 26);

            UIKit.TextAt(root, "VOLUME", 20, 1000, 190, 120, 28, Theme.TextDim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            volume = UIKit.Slider(root, 1000, 224, 440, 40, 0, 1, 1f, v => { Source.SetVolume(v); Highlight.SetVolume(v); }, Theme.Cyan);
            UIKit.TextAt(root, "Seek -10s / Play / Pause / +10s", 18, 90, 292, 700, 26, Theme.TextDim);
        }

        void Update()
        {
            var a = Active;
            if (a != null && a.Prepared)
            {
                seek.SetValueWithoutNotify(a.Time01);
                timeLabel.text = AppState.FormatTime(a.TimeSec) + " / " + AppState.FormatTime(a.Length);
            }
            else if (timeLabel) timeLabel.text = "00:00 / 00:00";
            if (toastGroup.alpha > 0 && Time.unscaledTime > toastUntil) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, 0, Time.unscaledDeltaTime * 2);
            Analysis.Tick(); Timeline.Tick(); Summary.Tick(); Player.Tick();
        }

        float toastUntil;
        public void Toast(string msg, float secs = 3f) { toast.text = msg; toastGroup.alpha = 1; toastUntil = Time.unscaledTime + secs; }

        void OnHealth(bool online)
        {
            statusDot.color = online ? Theme.Green : Theme.Magenta;
            statusText.text = online ? (client.Cuda ? "Engine online (GPU)" : "Engine online (CPU)") : "Engine offline";
        }

        // ============================================================ navigation
        public void SwitchTab(Workspace.Tab tab, bool instant = false)
        {
            int ni = (int)tab;
            for (int i = 0; i < NTabs; i++)
            {
                bool on = i == ni;
                navBg[i].color = on ? Theme.Violet : Theme.Card;
                navButtons[i].GetComponent<HoverFx>().SetBase(on ? Theme.Violet : Theme.Card);
                if (on) { if (instant) tabFx[i].SetInstant(true); else tabFx[i].Show(); }
                else if (i == (int)Current || tabRoots[i].gameObject.activeSelf) { if (instant) tabFx[i].SetInstant(false); else tabFx[i].Hide(); }
            }
            var prev = Current;
            Current = tab;
            headerTitle.text = new[] { "Video Library", "Video Workspace", "AI Analysis", "Highlight Timeline", "Summary Preview", "Model Test Lab" }[ni];
            if (prev == Workspace.Tab.Summary && tab != Workspace.Tab.Summary) Highlight.Pause();
            if (tab == Workspace.Tab.Summary) { Source.Pause(); Summary.OnShow(); }
            if (tab == Workspace.Tab.Library) Library.OnShow();
            if (tab == Workspace.Tab.Timeline) Timeline.OnShow();
            if (tab == Workspace.Tab.Analysis) Analysis.OnShow();
            if (tab == Workspace.Tab.TestLab) TestLab.OnShow();
        }

        void GoHome() { StartCoroutine(HomeCo()); }
        IEnumerator HomeCo()
        {
            var cg = Stage.GetComponent<CanvasGroup>();
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime) { cg.alpha = 1 - t / 0.3f; yield return null; }
            SceneManager.LoadScene("MainMenu");
        }

        // ============================================================ workflow
        void IVideoHost.SelectVideo(string path) { SelectVideo(path); }
        public void SelectVideo(string path, bool stayOnTab = false)
        {
            if (Running) { Toast("Analysis in progress - please wait."); return; }
            AppState.VideoPath = path; AppState.Result = null; AppState.JobId = null;
            headerFile.text = Path.GetFileName(path);
            Source.Load(path);
            Player.SetVideo(path);
            Analysis.Reset(); Timeline.Reset(); Summary.Reset();
            if (!stayOnTab) SwitchTab(Workspace.Tab.Player);
            Toast("Loaded " + Path.GetFileName(path));
        }

        bool learning;

        /// <summary>Live adaptation: vote on a highlight, the engine re-weights importance, re-selects and re-renders the reel.</summary>
        public void SendFeedback(float start, float end, int vote)
        {
            if (learning || Running) { Toast("Please wait - the engine is busy."); return; }
            if (string.IsNullOrEmpty(AppState.JobId)) { Toast("Run an analysis first."); return; }
            StartCoroutine(FeedbackCo(start, end, vote));
        }

        IEnumerator FeedbackCo(float start, float end, int vote)
        {
            learning = true;
            Toast(vote > 0 ? "Learning: more footage like this..." : "Learning: dropping this footage...", 20f);
            ResultEnvelope res = null; string err = null;
            yield return client.Feedback(AppState.JobId, start, end, vote, r => res = r, e => err = e);
            learning = false;
            if (res == null) { Toast("Feedback failed: " + err, 6f); yield break; }
            ApplyResult(res, true);
            Toast($"Updated highlights ({res.analysis.feedback_votes} vote{(res.analysis.feedback_votes == 1 ? "" : "s")} learned).", 4f);
        }

        public void StartUrlAnalysis(string url)
        {
            if (Running) { Toast("Analysis already running."); return; }
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http")) { Toast("Copy a YouTube link first, then press Paste."); return; }
            if (!client.Online) { Toast("Engine offline - try again in a few seconds."); return; }
            runCo = StartCoroutine(RunAnalysis(url.Trim(), null));
        }

        /// <summary>Run the trained model on a video that is already on the engine's disk (held-out test videos): no upload.</summary>
        public void StartPathAnalysis(string path)
        {
            if (Running) { Toast("Analysis already running."); return; }
            if (!client.Online) { Toast("Engine offline - try again in a few seconds."); return; }
            if (!File.Exists(path)) { Toast("Video not found: " + path, 5f); return; }
            if (AppState.VideoPath != path) { AppState.VideoPath = path; Source.Load(path); Player.SetVideo(path); }
            runCo = StartCoroutine(RunAnalysis(null, path));
        }

        public void StartAnalysis()
        {
            if (Running) { Toast("Analysis already running."); return; }
            if (!AppState.HasVideo) { Toast("Pick a video in the Library first."); SwitchTab(Workspace.Tab.Library); return; }
            if (!client.Online) { Toast("Engine offline - starting it, try again in a few seconds."); return; }
            runCo = StartCoroutine(RunAnalysis(null, null));
        }

        IEnumerator RunAnalysis(string url, string localPath)
        {
            Running = true; analyzeLabel.text = "RUNNING";
            Source.Pause();
            Analysis.Reset(); Timeline.Reset(); Summary.Reset();
            if (localPath == null) SwitchTab(Workspace.Tab.Analysis);
            string jobId = null, err = null;
            if (url != null)
            {
                AppState.VideoPath = null; AppState.Result = null;
                Analysis.SetProgress(0, "download", "Fetching a small copy of the video...");
                yield return client.AnalyzeUrl(url, id => jobId = id, e => err = e);
            }
            else if (localPath != null)
            {
                Analysis.SetProgress(0, "decode", "Running the trained model on the held-out video...");
                yield return client.AnalyzePath(localPath, id => jobId = id, e => err = e);
            }
            else
            {
                Analysis.SetProgress(0, "upload", "Sending video to the AI engine...");
                yield return client.Analyze(AppState.VideoPath, id => jobId = id, e => err = e);
            }
            if (jobId == null) { Fail(err ?? "Upload failed"); yield break; }
            AppState.JobId = jobId;
            ResultEnvelope res = null;
            yield return client.Poll(jobId, s => { Analysis.SetProgress(s.progress, s.stage, s.message); if (localPath != null) Toast($"Running trained model... {s.progress * 100:0}%  {s.message}", 2f); },
                r => res = r, e => err = e);
            if (res == null) { Fail(err ?? "Analysis failed"); yield break; }
            ApplyResult(res, true);
            Running = false; analyzeLabel.text = "ANALYZE";
            Toast("Analysis complete - explore the timeline and summary.");
        }

        void Fail(string msg)
        {
            Running = false; analyzeLabel.text = "ANALYZE";
            Analysis.SetError(msg); Toast("Error: " + msg, 6f);
        }

        void ApplyResult(ResultEnvelope res, bool fresh)
        {
            AppState.Result = res;
            Analysis.SetResult(res);
            Timeline.SetResult(res);
            Summary.SetResult(res);
            TestLab.SetResult(res);
            headerFile.text = res.output != null ? res.output.video_id : "";
            if (!string.IsNullOrEmpty(res.highlight_path) && File.Exists(res.highlight_path)) Highlight.Load(res.highlight_path);
            if (!string.IsNullOrEmpty(res.source_path) && File.Exists(res.source_path) && AppState.VideoPath != res.source_path && (!fresh || !AppState.HasVideo))
            { AppState.VideoPath = res.source_path; Source.Load(res.source_path); Player.SetVideo(res.source_path); }
        }

        public void JumpTo(float seconds, bool play = false)
        {
            if (!Source.Prepared) { Toast("Video is still preparing..."); return; }
            Source.Seek(seconds);
            if (play) Source.Play();
        }
    }

    public interface IPanel { void Build(RectTransform root); }

    /// <summary>Shared look for cards.</summary>
    public static class CardUI
    {
        public static RectTransform Card(Transform parent, string title, float x, float y, float w, float h, Color accent)
        {
            var c = UIKit.Box(parent, "Card_" + title, Theme.Panel, 24);
            UIKit.TL(c.rectTransform, x, y, w, h);
            UIKit.Outline(c.transform, accent * new Color(1, 1, 1, 0.55f), 24, 2f);
            if (!string.IsNullOrEmpty(title))
                UIKit.TextAt(c.transform, title.ToUpper(), 20, 24, 16, w - 48, 28, accent, TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 6;
            return c.rectTransform;
        }
    }
}
