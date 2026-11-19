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
            UIKit.CanvasScale = 0.00022f;                      // small panel held close to the chest (about 0.42 m)
            var pf = GetComponent<PlaceInFront>(); if (pf != null) { pf.near = true; pf.distance = 0.42f; pf.heightOffset = -0.22f; pf.tilt = 25f; }
            if (FindFirstObjectByType<HandOnlyMode>() == null) gameObject.AddComponent<HandOnlyMode>();
            client = BackendClient.Ensure();
            canvas = UIKit.WorldCanvas("MenuCanvas", W, H, transform);
            canvas.transform.localPosition = new Vector3(0, 0, 0);
            group = canvas.GetComponent<CanvasGroup>();
            Build();
            UIKit.SideGrips((RectTransform)canvas.transform, W, H, transform);      // pinch a side grip to drag the panel
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

            // ---- primary actions ----
            UIKit.Button(root, "START", W / 2 - 330, 440, 660, 110, StartJourney, Theme.Cyan, Theme.Bg, Sprites.Icon.Bolt, 44, 52, true);
            // minimal start screen: title, one line, START and an engine status dot
            statusDot = UIKit.Box(root, "Dot", Theme.Magenta, 0, false); statusDot.sprite = Sprites.Circle(); UIKit.TL(statusDot.rectTransform, W - 70, 40, 22, 22);
            statusDot.gameObject.AddComponent<Pulse>().alpha = true;
            statusText = UIKit.TextAt(root, "", 20, W - 470, 22, 390, 56, Theme.TextDim, TextAlignmentOptions.MidlineRight);
            deviceText = UIKit.TextAt(root, "", 18, W / 2 - 400, 600, 800, 30, Theme.TextDim, TextAlignmentOptions.Center);
        }

        IEnumerator Intro()
        {
            group.alpha = 0; canvas.transform.localScale = Vector3.one * UIKit.CanvasScale * 0.9f;
            var pf = GetComponent<PlaceInFront>(); while (pf != null && !pf.Placed) yield return null;
            for (float t = 0; t < 1.1f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 1.1f);
                group.alpha = k; canvas.transform.localScale = Vector3.one * Mathf.Lerp(UIKit.CanvasScale * 0.9f, UIKit.CanvasScale, k);
                yield return null;
            }
            group.alpha = 1; canvas.transform.localScale = Vector3.one * UIKit.CanvasScale;
        }

        void OnHealth(bool online)
        {
            statusDot.color = online ? Theme.Green : Theme.Magenta;
            statusText.text = online ? "" : "Connecting to the model...";
            deviceText.text = online ? "" : "Engine: " + client.BaseUrl;
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

        void StartJourney() { if (SpaceShift.Instance != null) SpaceShift.Instance.Dive("Journey"); else StartCoroutine(FadeAndLoad("Journey")); }          // the spatial shift, flung outward, into the app

        void Go(Workspace.Tab tab)
        {
            AppState.PendingTab = tab;
            StartCoroutine(FadeAndLoad("Workspace"));
        }

        IEnumerator FadeAndLoad(string scene)
        {
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime) { group.alpha = 1 - t / 0.4f; yield return null; }
            SceneManager.LoadScene(scene);
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
