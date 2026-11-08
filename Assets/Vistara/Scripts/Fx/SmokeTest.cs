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
        string outDir, video;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-vistaraSmoke");
            if (i < 0 || i + 1 >= a.Length) return;
            var go = new GameObject("SmokeTest"); DontDestroyOnLoad(go);
            var s = go.AddComponent<SmokeTest>(); s.outDir = a[i + 1];
            int v = Array.IndexOf(a, "-vistaraVideo"); if (v >= 0 && v + 1 < a.Length) s.video = a[v + 1];
            Directory.CreateDirectory(s.outDir);
        }

        void Start() { StartCoroutine(Run()); }

        IEnumerator Shot(string name, float wait)
        {
            yield return new WaitForSeconds(wait);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
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
                ws.SwitchTab(Workspace.Tab.Summary);
                yield return Shot("07_summary", 4.0f);
            }
            Finish(0);
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
