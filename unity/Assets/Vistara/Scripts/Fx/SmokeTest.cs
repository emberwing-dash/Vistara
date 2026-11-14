using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vistara
{
    /// <summary>
    /// Dev-only end-to-end check. Active only when Unity is launched with:  -vistaraSmoke &lt;outDir&gt; [-vistaraVideo &lt;mp4&gt;]
    /// Screenshots every screen and drives a real analysis through the live Python engine.
    /// </summary>
    public class SmokeTest : MonoBehaviour
    {
        string outDir, video, url;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-vistaraSmoke");
            if (i < 0 || i + 1 >= a.Length) return;
            var go = new GameObject("SmokeTest"); DontDestroyOnLoad(go);
            var s = go.AddComponent<SmokeTest>(); s.outDir = a[i + 1];
            int u = Array.IndexOf(a, "-vistaraUrl"); if (u >= 0 && u + 1 < a.Length) s.url = a[u + 1];
            int v = Array.IndexOf(a, "-vistaraVideo"); if (v >= 0 && v + 1 < a.Length) s.video = a[v + 1];
            Directory.CreateDirectory(s.outDir);
        }

        void Start() { StartCoroutine(Run()); }

        IEnumerator Shot(string name, float wait)
        {
            yield return new WaitForSeconds(wait);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"), 2);
            yield return null; yield return null;
            Debug.Log("[Smoke] shot " + name);
        }

        IEnumerator Run()
        {
            Debug.Log("[Smoke] start, scene=" + SceneManager.GetActiveScene().name);
            // wait for the engine (auto-launched by BackendLauncher)
            float t0 = Time.time;
            while ((BackendClient.Instance == null || !BackendClient.Instance.Online) && Time.time - t0 < 120) yield return null;
            Debug.Log("[Smoke] engine online=" + (BackendClient.Instance != null && BackendClient.Instance.Online) + " after " + (Time.time - t0) + "s");
            var pf0 = FindFirstObjectByType<PlaceInFront>(); float tp = Time.time;
            while (pf0 != null && !pf0.Placed && Time.time - tp < 15) yield return null;
            yield return new WaitForSeconds(2.0f);
            yield return Shot("01_menu", 0.2f);

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-vistaraJourney") >= 0) { yield return JourneyRun(); Finish(0); yield break; }

            SceneManager.LoadScene("Workspace");
            yield return new WaitForSeconds(1.0f);
            var ws = FindFirstObjectByType<WorkspaceController>();
            if (ws == null) { Debug.LogError("[Smoke] no WorkspaceController"); Finish(1); yield break; }
            var pf1 = FindFirstObjectByType<PlaceInFront>(); tp = Time.time;
            while (pf1 != null && !pf1.Placed && Time.time - tp < 15) yield return null;
            yield return Shot("02_library", 2.0f);

            if (!string.IsNullOrEmpty(video) && File.Exists(video))
            {
                ws.SelectVideo(video);
                yield return Shot("03_player", 3.5f);
                ws.StartAnalysis();
                yield return Shot("04_analysis_progress", 6f);
                float t1 = Time.time;
                while (ws.Running && Time.time - t1 < 400) yield return null;
                Debug.Log("[Smoke] analysis finished, hasResult=" + AppState.HasResult + " after " + (Time.time - t1) + "s");
                yield return Shot("05_analysis_done", 2.5f);
                ws.SwitchTab(Workspace.Tab.Timeline);
                yield return Shot("06_timeline", 2.0f);
                var before = AppState.Result.analysis.segments;
                if (before != null && before.Length > 0)
                {
                    Debug.Log("[Smoke] feedback: dropping segment " + before[0].start + "-" + before[0].end + " (of " + before.Length + ")");
                    ws.SendFeedback(before[0].start, before[0].end, -1);
                    yield return new WaitForSeconds(12f);
                    var after = AppState.Result.analysis.segments;
                    Debug.Log("[Smoke] feedback result: votes=" + AppState.Result.analysis.feedback_votes + " segments=" + after.Length + " first=" + after[0].start);
                    yield return Shot("06b_after_feedback", 1.0f);
                }
                ws.SwitchTab(Workspace.Tab.Summary);
                yield return Shot("07_summary", 4.0f);
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-vistaraTestLab") >= 0)
            {
                // Test Lab: held-out video list + REAL trained-model run on a held-out EPIC kitchen clip + evaluation metrics
                ws.SwitchTab(Workspace.Tab.TestLab);
                float tl = Time.time; while (!ws.TestLab.ListReady && Time.time - tl < 60) yield return null;
                yield return Shot("11_testlab_list", 3f);
                var ev = ws.TestLab.SelectFirst("epic_kitchens_100");
                Debug.Log("[Smoke] testlab selected " + (ev != null ? ev.video_id + " " + ev.path : "NONE"));
                if (ev != null)
                {
                    yield return new WaitForSeconds(2f);
                    ws.StartPathAnalysis(ev.path);
                    yield return Shot("12_testlab_running", 4f);
                    float t3 = Time.time; yield return new WaitForSeconds(1f);
                    while (ws.Running && Time.time - t3 < 300) yield return null;
                    Debug.Log("[Smoke] testlab analysis finished, hasResult=" + AppState.HasResult + " after " + (Time.time - t3) + "s; actions=" + (AppState.HasResult ? AppState.Result.output.actions.Length : -1));
                    ws.SwitchTab(Workspace.Tab.TestLab);
                    yield return Shot("13_testlab_result", 3f);
                    ws.SwitchTab(Workspace.Tab.Timeline);
                    yield return Shot("14_testlab_timeline", 3f);
                }
                ws.SwitchTab(Workspace.Tab.TestLab);
                yield return new WaitForSeconds(1.5f);
                var sp = ws.TestLab.SelectFirst("fpvsum");
                Debug.Log("[Smoke] testlab sport video " + (sp != null ? sp.video_id : "NONE"));
                if (sp != null)
                {
                    yield return new WaitForSeconds(2f);
                    ws.StartPathAnalysis(sp.path);
                    float t4 = Time.time; yield return new WaitForSeconds(1f);
                    while (ws.Running && Time.time - t4 < 300) yield return null;
                    ws.SwitchTab(Workspace.Tab.TestLab);
                    yield return Shot("15_testlab_sport", 3f);
                    ws.SwitchTab(Workspace.Tab.Timeline);
                    yield return Shot("16_testlab_sport_timeline", 3f);
                }
            }
            if (!string.IsNullOrEmpty(url))
            {
                ws.SwitchTab(Workspace.Tab.Library);
                Debug.Log("[Smoke] starting URL analysis " + url);
                ws.StartUrlAnalysis(url);
                yield return Shot("08_url_progress", 5f);
                float t2 = Time.time;
                yield return new WaitForSeconds(2f);
                while (ws.Running && Time.time - t2 < 240) yield return null;
                Debug.Log("[Smoke] url analysis finished, hasResult=" + AppState.HasResult + " video=" + AppState.VideoPath + " after " + (Time.time - t2) + "s");
                ws.SwitchTab(Workspace.Tab.Timeline);
                yield return Shot("09_url_timeline", 3f);
                ws.SwitchTab(Workspace.Tab.Summary);
                yield return Shot("10_url_summary", 4f);
            }
            Finish(0);
        }

        // Hand-controlled flow: Start -> Category carousel -> Video -> Analyse -> Results -> MR HUD. Test hooks call the same code the pinch buttons call.
        IEnumerator JourneyRun()
        {
            string cat = "Kitchen";
            var a = Environment.GetCommandLineArgs(); int ci = Array.IndexOf(a, "-vistaraCategory"); if (ci >= 0 && ci + 1 < a.Length) cat = a[ci + 1];
            string tag = cat == "Kitchen" ? "" : "_" + cat.ToLowerInvariant();
            Debug.Log("[Smoke] Journey: hands subsystem: " + (HandSwipeDetector.Instance != null ? HandSwipeDetector.Instance.Status : "detector not created yet") + "; " + HandOnlyMode.Report);
            SceneManager.LoadScene("Journey");
            yield return new WaitForSeconds(1.0f);
            var jc = FindFirstObjectByType<JourneyController>();
            if (jc == null) { Debug.LogError("[Smoke] no JourneyController"); Finish(1); yield break; }
            var pf = FindFirstObjectByType<PlaceInFront>(); float tp = Time.time; while (pf != null && !pf.Placed && Time.time - tp < 15) yield return null;
            float t0 = Time.time; while (!jc.CategoriesReady && Time.time - t0 < 120) yield return null;
            Debug.Log("[Smoke] Journey: categories=" + jc.CategoryCount + " after " + (Time.time - t0) + "s");
            yield return new WaitForSeconds(3.5f);                          // thumbnails
            yield return Shot("20_journey_categories" + tag, 0.5f);
            HandSwipeDetector.Inject(-1);                                    // hand swipe left -> carousel advances
            yield return new WaitForSeconds(1.2f);
            Debug.Log("[Smoke] Journey: after swipe focus=" + jc.FocusIndex);
            yield return Shot("21_journey_swiped" + tag, 0.3f);
            jc.ScrollBy(-5); yield return new WaitForSeconds(1.2f);
            Debug.Log("[Smoke] Journey: after ScrollBy(-5) focus=" + jc.FocusIndex);
            jc.TestPickCategory(cat);                                        // pinch-select a category card
            t0 = Time.time; while (!jc.VideosReady && Time.time - t0 < 30) yield return null;
            yield return new WaitForSeconds(2f);
            yield return Shot("22_journey_videos" + tag, 0.5f);
            Debug.Log("[Smoke] Journey: picked video = " + jc.TestPickFirstVideo(cat == "Kitchen" ? 5 : 0));
            yield return new WaitForSeconds(3.5f);
            yield return Shot("23_journey_preview" + tag, 0.5f);
            jc.StartAnalysis();                                              // ANALYSE button
            yield return Shot("24_journey_analysing" + tag, 4f);
            t0 = Time.time; yield return new WaitForSeconds(1f);
            while (jc.Running && Time.time - t0 < 400) yield return null;
            Debug.Log("[Smoke] Journey: analysis finished hasResult=" + AppState.HasResult + " step=" + jc.Current + " actions=" + (AppState.HasResult ? AppState.Result.output.actions.Length : -1));
            yield return Shot("25_journey_results" + tag, 1.5f);
            jc.OpenHud();                                                    // ENTER MR HUD
            yield return new WaitForSeconds(3f);
            yield return Shot("26_journey_hud" + tag, 0.5f);
            HandSwipeDetector.Inject(+1);                                    // hand swipe in the HUD: +10 s
            yield return new WaitForSeconds(2.5f);
            yield return Shot("27_journey_hud_seek" + tag, 0.5f);
            jc.Hud.Hide(true); yield return new WaitForSeconds(1f);
        }

        void Dump()
        {
            foreach (var c in Camera.allCameras)
                Debug.Log($"[Smoke] cam {c.name} en={c.enabled} pos={c.transform.position} fwd={c.transform.forward} mask={c.cullingMask} near={c.nearClipPlane} far={c.farClipPlane} stereo={c.stereoTargetEye} tag={c.tag} depth={c.depth} flags={c.clearFlags}");
            var xo = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xo) Debug.Log($"[Smoke] XROrigin pos={xo.transform.position} mode={xo.RequestedTrackingOriginMode} camYoff={xo.CameraYOffset} cameraLocal={xo.Camera.transform.localPosition} offsetLocal={xo.CameraFloorOffsetObject.transform.localPosition}");
            var ui = GameObject.Find("VistaraUI");
            Debug.Log("[Smoke] VistaraUI pos=" + (ui ? ui.transform.position.ToString() : "null"));
            var env = GameObject.Find("Environment");
            Debug.Log("[Smoke] Environment pos=" + (env ? env.transform.position.ToString() : "null") + " children=" + (env ? env.transform.childCount : -1));
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas) Debug.Log("[Smoke] canvas pos=" + canvas.transform.position + " scale=" + canvas.transform.lossyScale + " cam=" + (canvas.worldCamera ? canvas.worldCamera.name : "null") + " layer=" + canvas.gameObject.layer);
            Debug.Log("[Smoke] HMD present=" + UnityEngine.XR.XRSettings.isDeviceActive + " dev=" + UnityEngine.XR.XRSettings.loadedDeviceName);
        }

        void Finish(int code)
        {
            Debug.Log("[Smoke] done code=" + code);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
    }
}
