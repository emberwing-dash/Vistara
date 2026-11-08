using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>Main menu / home dashboard: branding, start/import, engine status, recent analysis history.</summary>
    public class MainMenuController : MonoBehaviour
    {
        const float W = 1700, H = 940;
        Canvas canvas; CanvasGroup group;
        Image statusDot; TextMeshProUGUI statusText, deviceText;
        RectTransform historyList; TextMeshProUGUI historyEmpty;
        RectTransform ring1, ring2;
        BackendClient client;

        void Awake()
        {
            client = BackendClient.Ensure();
            canvas = UIKit.WorldCanvas("MenuCanvas", W, H, transform);
            canvas.transform.localPosition = new Vector3(0, 0, 0);
            group = canvas.GetComponent<CanvasGroup>();
            Build();
            group.alpha = 0;          // hidden until the rig has been placed in front of the user
        }

        void Start()
        {
            client.OnHealthChanged += OnHealth;
            OnHealth(client.Online);
            StartCoroutine(RefreshHistory());
            StartCoroutine(Intro());
        }

        void OnDestroy() { if (client) client.OnHealthChanged -= OnHealth; }

        void Build()
        {
            var root = (RectTransform)canvas.transform;

            // ---- backdrop panel ----
            var back = UIKit.Box(root, "Backdrop", new Color(0.03f, 0.05f, 0.12f, 0.78f), 36);
            UIKit.Fill(back.rectTransform);
            UIKit.Glow(back.transform, Theme.Violet * new Color(1, 1, 1, 0.5f), 36, 40, 30);
            UIKit.Outline(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.55f), 36, 3f);

            // ---- holographic rings behind the title ----
            ring1 = UIKit.Rect(root, "Ring1");
            var r1 = ring1.gameObject.AddComponent<Image>(); r1.sprite = Sprites.Ring(0.02f); r1.color = Theme.Cyan * new Color(1, 1, 1, 0.5f); r1.raycastTarget = false;
            UIKit.TL(ring1, 0, 0, 700, 700).pivot = new Vector2(0.5f, 0.5f);
            ring1.anchoredPosition = new Vector2(W / 2, -600);
            ring1.gameObject.AddComponent<Spin>().degPerSec = 8;
            ring2 = UIKit.Rect(root, "Ring2");
            var r2 = ring2.gameObject.AddComponent<Image>(); r2.sprite = Sprites.Ring(0.012f); r2.color = Theme.Magenta * new Color(1, 1, 1, 0.55f); r2.raycastTarget = false;
            UIKit.TL(ring2, 0, 0, 520, 520).pivot = new Vector2(0.5f, 0.5f);
            ring2.anchoredPosition = new Vector2(W / 2, -600);
            ring2.gameObject.AddComponent<Spin>().degPerSec = -14;

            // ---- title ----
            var title = UIKit.TextAt(root, "VISTARA", 170, 0, 36, W, 210, Theme.Cyan, TextAlignmentOptions.Center, FontStyles.Bold);
            title.characterSpacing = 28;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(Theme.Cyan, Theme.Cyan, Theme.Violet, Theme.Magenta);
            title.overflowMode = TextOverflowModes.Overflow;
            title.raycastTarget = false;
            title.gameObject.AddComponent<Pulse>().amount = 0.015f;
            var tag = UIKit.TextAt(root, "ACTION-AWARE EGOCENTRIC VIDEO SUMMARIZATION", 28, 0, 240, W, 44, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            tag.characterSpacing = 8;
            UIKit.TextAt(root, "Understand what the wearer does. Keep what matters. Drop the rest.", 24, 0, 290, W, 36, Theme.TextDim, TextAlignmentOptions.Center);

            // ---- primary actions ----
            UIKit.Button(root, "START ANALYSIS", W / 2 - 330, 440, 660, 110, () => Go(Workspace.Tab.Library), Theme.Cyan, Theme.Bg, Sprites.Icon.Bolt, 38, 52, true);
            UIKit.Button(root, "Open last result", W / 2 - 330, 580, 320, 70, OpenLast, Theme.Card, Theme.Text, Sprites.Icon.Chart, 24, 35);
            UIKit.Button(root, "Quit", W / 2 + 10, 580, 320, 70, Quit, Theme.Card, Theme.Text, null, 24, 35);

            string[] chips = { "Action recognition", "Importance scoring", "15% highlights" };
            for (int i = 0; i < 3; i++)
            {
                var chip = UIKit.Box(root, "Chip" + i, new Color(1, 1, 1, 0.06f), 22, false); UIKit.TL(chip.rectTransform, W / 2 - 330 + i * 226, 700, 208, 44);
                UIKit.Outline(chip.transform, Theme.Violet, 22, 2f);
                UIKit.TextAt(chip.transform, chips[i], 18, 0, 0, 208, 44, Theme.Text, TextAlignmentOptions.Center);
            }
            UIKit.TextAt(root, "Egocentric video in  >  verb-noun actions + 15% highlight reel out", 20, W / 2 - 330, 760, 660, 60, Theme.TextDim, TextAlignmentOptions.Center);

            // ---- left: engine / pipeline card ----
            var left = UIKit.Box(root, "EngineCard", Theme.Panel, 26);
            UIKit.TL(left.rectTransform, 50, 350, 420, 510);
            UIKit.Outline(left.transform, Theme.Violet * new Color(1, 1, 1, 0.6f), 26, 2f);
            UIKit.TextAt(left.transform, "AI ENGINE", 22, 28, 22, 360, 30, Theme.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 6;
            statusDot = UIKit.Box(left.transform, "Dot", Theme.Magenta, 0, false); statusDot.sprite = Sprites.Circle(); UIKit.TL(statusDot.rectTransform, 30, 70, 22, 22);
            statusDot.gameObject.AddComponent<Pulse>().alpha = true;
            statusText = UIKit.TextAt(left.transform, "Connecting...", 26, 64, 62, 340, 38, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            deviceText = UIKit.TextAt(left.transform, "", 20, 30, 108, 370, 30, Theme.TextDim);
            string[] steps = { "1  Frame sampling + optical flow", "2  CLIP scene & object understanding", "3  Verb-noun action localisation", "4  Importance + redundancy scoring", "5  15% highlight reel (original footage)" };
            for (int i = 0; i < steps.Length; i++)
            {
                var b = UIKit.Box(left.transform, "Step" + i, new Color(1, 1, 1, 0.05f), 12, false);
                UIKit.TL(b.rectTransform, 24, 170 + i * 66, 372, 54);
                UIKit.TextAt(b.transform, steps[i], 21, 16, 0, 350, 54, Theme.Text, TextAlignmentOptions.MidlineLeft);
            }
            UIKit.TextAt(left.transform, "Samsung PRISM 2026  -  Track 04", 18, 28, 474, 370, 26, Theme.TextDim);

            // ---- right: recent history ----
            var right = UIKit.Box(root, "HistoryCard", Theme.Panel, 26);
            UIKit.TL(right.rectTransform, W - 470, 350, 420, 510);
            UIKit.Outline(right.transform, Theme.Magenta * new Color(1, 1, 1, 0.6f), 26, 2f);
            UIKit.TextAt(right.transform, "RECENT ANALYSES", 22, 28, 22, 360, 30, Theme.Magenta, TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 6;
            historyList = UIKit.ScrollList(right.transform, 18, 66, 384, 426, 8, new Color(0, 0, 0, 0.18f));
            historyEmpty = UIKit.TextAt(right.transform, "No analyses yet.\nImport a video to begin.", 22, 40, 240, 340, 90, Theme.TextDim, TextAlignmentOptions.Center);

            UIKit.TextAt(root, "Use the XR controller ray (or simulator) to point and select.", 20, 0, H - 56, W, 30, Theme.TextDim, TextAlignmentOptions.Center);
        }

        IEnumerator Intro()
        {
            group.alpha = 0; canvas.transform.localScale = Vector3.one * 0.0009f;
            var pf = GetComponent<PlaceInFront>(); while (pf != null && !pf.Placed) yield return null;
            for (float t = 0; t < 1.1f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 1.1f);
                group.alpha = k; canvas.transform.localScale = Vector3.one * Mathf.Lerp(0.0009f, 0.001f, k);
                yield return null;
            }
            group.alpha = 1; canvas.transform.localScale = Vector3.one * 0.001f;
        }

        void OnHealth(bool online)
        {
            statusDot.color = online ? Theme.Green : Theme.Magenta;
            statusText.text = online ? "Engine online" : "Engine offline";
            deviceText.text = online ? (client.Cuda ? "GPU accelerated (CUDA)  -  http://127.0.0.1:8765" : "CPU mode  -  http://127.0.0.1:8765") : "Starting Python engine... (Backend/start_server.bat)";
            if (online) StartCoroutine(RefreshHistory());
        }

        IEnumerator RefreshHistory()
        {
            JobStatus[] items = null;
            yield return client.History(r => items = r);
            if (historyList == null) yield break;
            UIKit.ClearChildren(historyList);
            historyEmpty.gameObject.SetActive(items == null || items.Length == 0);
            if (items == null) yield break;
            foreach (var j in items)
            {
                var job = j;
                var row = UIKit.Box(historyList, "Row", Theme.Card, 14);
                UIKit.Sized(row.gameObject, 84);
                var btn = row.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.targetGraphic = row;
                row.gameObject.AddComponent<HoverFx>();
                bool done = job.status == "done";
                var dot = UIKit.Box(row.transform, "Dot", done ? Theme.Green : (job.status == "error" ? Theme.Magenta : Theme.Orange), 0, false);
                dot.sprite = Sprites.Circle(); UIKit.TL(dot.rectTransform, 14, 31, 20, 20);
                UIKit.TextAt(row.transform, job.filename ?? job.video_id, 22, 46, 10, 310, 32, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                UIKit.TextAt(row.transform, done ? "Complete - tap to open" : (job.status ?? "unknown") + " " + Mathf.RoundToInt(job.progress * 100) + "%", 18, 46, 44, 320, 28, Theme.TextDim);
                if (done) btn.onClick.AddListener(() => StartCoroutine(OpenJob(job)));
            }
        }

        IEnumerator OpenJob(JobStatus job)
        {
            ResultEnvelope res = null;
            yield return client.FetchResult(job.id, r => res = r, e => Debug.LogWarning(e));
            if (res == null) yield break;
            AppState.Result = res; AppState.JobId = job.id; AppState.LastJob = job; AppState.VideoPath = res.source_path;
            Go(Workspace.Tab.Summary);
        }

        void OpenLast()
        {
            if (AppState.HasResult) Go(Workspace.Tab.Summary);
            else StartCoroutine(OpenMostRecent());
        }

        IEnumerator OpenMostRecent()
        {
            JobStatus[] items = null;
            yield return client.History(r => items = r);
            if (items == null) yield break;
            foreach (var j in items) if (j.status == "done") { yield return OpenJob(j); yield break; }
            Go(Workspace.Tab.Library);
        }

        void Go(Workspace.Tab tab)
        {
            AppState.PendingTab = tab;
            StartCoroutine(FadeAndLoad());
        }

        IEnumerator FadeAndLoad()
        {
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime) { group.alpha = 1 - t / 0.4f; yield return null; }
            SceneManager.LoadScene("Workspace");
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
