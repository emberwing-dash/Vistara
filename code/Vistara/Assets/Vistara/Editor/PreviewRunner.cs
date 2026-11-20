#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vistara.EditorTools
{
    /// <summary>
    /// Development helper: launched with -vistaraPreview, it builds the scenes, opens Journey, plays it (XR Simulator rig) and writes screenshots of each stage
    /// to &lt;project&gt;/preview. Optional args: -previewCats Kitchen,Scuba  -previewVideo &lt;path&gt;
    /// </summary>
    [InitializeOnLoad]
    static class PreviewRunner
    {
        static int stage { get { return SessionState.GetInt("vp_stage", 0); } set { SessionState.SetInt("vp_stage", value); } }
        static int ci { get { return SessionState.GetInt("vp_ci", 0); } set { SessionState.SetInt("vp_ci", value); } }
        static double startedAt { get { return double.Parse(SessionState.GetString("vp_t0", "0")); } set { SessionState.SetString("vp_t0", value.ToString()); } }
        static double until; static string dir; static string[] cats;
        static bool daemon; static string[] argv = Environment.GetCommandLineArgs();
        const string JobFile = "C:/vis_eval/cmd/job.txt", DoneFile = "C:/vis_eval/cmd/done.txt", RefreshFile = "C:/vis_eval/cmd/refresh.txt";
        static string Arg(string n) { var a = argv; int i = Array.IndexOf(a, n); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
        static void Finish(int code) { if (daemon) { try { EditorApplication.isPlaying = false; } catch (Exception) { } stage = -1; try { File.WriteAllText(DoneFile, "code " + code + " " + DateTime.Now.ToString("HH:mm:ss")); } catch (Exception) { } } else EditorApplication.Exit(code); }
        static void Log(string m) { Debug.Log("[Preview] " + m); try { File.AppendAllText(Path.Combine(dir ?? ".", "preview.log"), DateTime.Now.ToString("HH:mm:ss ") + m + "\n"); } catch (Exception) { } }

        static PreviewRunner()
        {
            var cl = Environment.GetCommandLineArgs(); daemon = Array.IndexOf(cl, "-vistaraDaemon") >= 0;
            if (Array.IndexOf(cl, "-vistaraPreview") < 0 && !daemon) return;
            EditorUtility.audioMasterMute = true;          // automated runs must be silent
            PlayerSettings.runInBackground = true;          // play mode must keep running when the editor window is not focused
            dir = Path.GetFullPath("../preview"); Directory.CreateDirectory(dir);
            if (daemon)      // one editor stays open (minimised) and takes jobs from C:is_eval\cmd\job.txt; the job line survives the domain reloads of play mode
            {
                if (!SessionState.GetBool("vp_init", false)) { SessionState.SetBool("vp_init", true); stage = -1; }
                var line = SessionState.GetString("vp_job", ""); if (line.Length > 0) argv = ("unity " + line).Split(' ');
                EditorApplication.update += DaemonTick;
            }
            { var sp = Arg("-previewSport"); if (sp != null) sports = sp.Split(','); }
            cats = (Arg("-previewCats") ?? "Parkour,Scuba,Surfing,Boxing").Split(',');
            EditorApplication.update += Tick;
        }

        static void DaemonTick()
        {
            if (File.Exists(RefreshFile)) { try { File.Delete(RefreshFile); } catch (Exception) { } AssetDatabase.Refresh(); return; }          // pick up edited scripts while the editor is minimised (it only refreshes on focus otherwise)
            if (stage >= 0 || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(JobFile)) return;
            string line; try { line = File.ReadAllText(JobFile).Trim(); File.Delete(JobFile); } catch (Exception) { return; }
            SessionState.SetString("vp_job", line); argv = ("unity " + line).Split(' ');
            try { if (File.Exists(DoneFile)) File.Delete(DoneFile); foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f); File.Delete(Path.Combine(dir, "preview.log")); } catch (Exception) { }
            sports = null; { var sp = Arg("-previewSport"); if (sp != null) sports = sp.Split(','); }
            cats = (Arg("-previewCats") ?? "Parkour,Scuba,Surfing,Boxing").Split(','); ci = 0; si = 0; startedAt = EditorApplication.timeSinceStartup; until = 0;
            Log("job: " + line); stage = 0;
        }

        static void Shot(string name)
        {
            // URP: a copied camera rendering to an RT came out blank; the Game view capture is what the headset/simulator really shows.
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"), 1);
            Log("shot " + name);
        }

        /// <summary>A saved real output (repo/outputs) for the chosen video, plus an interest curve derived from its highlights, so the results theater can be checked without the GPU.</summary>
        static ResultEnvelope MockFor(string videoPath)
        {
            try
            {
                string dirOut = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "repo", "outputs"));
                string evalDir = "C:/vis_eval/eval4/predictions"; if (Directory.Exists(evalDir) && File.Exists(Path.Combine(evalDir, Path.GetFileNameWithoutExtension(videoPath ?? "") + ".json"))) dirOut = evalDir;       // real model output of the latest evaluation, if present
                if (!Directory.Exists(dirOut)) return null;
                string id = Path.GetFileNameWithoutExtension(videoPath ?? ""); string file = null;
                foreach (var f in Directory.GetFiles(dirOut, "*.json")) { if (Path.GetFileName(f).Contains(id)) { file = f; break; } }
                if (file == null) return null;
                var o = JsonUtility.FromJson<OutputJson>(File.ReadAllText(file)); if (o == null || o.actions == null || o.actions.Length == 0) return null;
                int n = Mathf.Max(2, Mathf.CeilToInt(o.duration_sec)); var t = new float[n]; var imp = new float[n];
                for (int i = 0; i < n; i++) { t[i] = i; float v = 0.15f + 0.1f * Mathf.PerlinNoise(i * 0.21f, 3f); foreach (var h in o.highlights) { float c = (h.segment[0] + h.segment[1]) * 0.5f, w = Mathf.Max(2f, (h.segment[1] - h.segment[0]) * 0.6f); v += 0.8f * Mathf.Exp(-((i - c) * (i - c)) / (2 * w * w)); } imp[i] = v; }
                var nouns = new System.Collections.Generic.Dictionary<string, ObjStat>();
                foreach (var a in o.actions) { if (!nouns.TryGetValue(a.noun, out var s)) nouns[a.noun] = s = new ObjStat { noun = a.noun }; s.count++; s.seconds += a.End - a.Start; }
                var an = new AnalysisJson { curve = new Curve { t = t, importance = imp, activity = imp }, objects = new System.Collections.Generic.List<ObjStat>(nouns.Values).ToArray() };
                return new ResultEnvelope { output = o, analysis = an, source_path = videoPath };
            }
            catch (Exception e) { Log("mock failed: " + e.Message); return null; }
        }

        static string[] sports; static int si; static int wi;
        static string[] SportAct(string c)
        {
            switch (c) { case "Kitchen": return new[] { "take", "milk" }; case "Boxing": return new[] { "punch", "punching bag" }; case "Horse": return new[] { "ride", "horse" }; case "Jumping": return new[] { "jump", "box" }; case "Longboarding": return new[] { "ride", "board" }; case "Parkour": return new[] { "jump", "box" };
                case "Plane": return new[] { "fly", "plane" }; case "RockClimbing": return new[] { "climb", "wall" }; case "Scuba": return new[] { "swim", "fish" }; case "Skate": return new[] { "ride", "skateboard" }; case "Ski": return new[] { "ski", "skis" }; case "Surfing": return new[] { "surf", "surfboard" }; default: return new[] { "ride", "bike" }; }
        }
        static void Tick()
        {
            if (stage < 0) return;
            try
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (stage == 0)
                {
                    if (!Directory.Exists("Assets/Scenes") || !File.Exists("Assets/Scenes/Journey.unity")) { Log("building scenes"); VistaraSceneBuilder.BuildAll(); }
                    EditorSceneManager.OpenScene("Assets/Scenes/Journey.unity"); stage = 1; startedAt = EditorApplication.timeSinceStartup; EditorApplication.isPlaying = true; return;
                }
                if (!EditorApplication.isPlaying) return;
                if (EditorApplication.timeSinceStartup < until) return;
                var j = UnityEngine.Object.FindFirstObjectByType<JourneyController>();
                if (j == null) { if (EditorApplication.timeSinceStartup - startedAt > 120) { Log("no JourneyController"); Finish(2); } return; }
                switch (stage)
                {
                    case 1: if (j.CategoriesReady) { Log("categories ready (" + j.CategoryCount + ")"); until = EditorApplication.timeSinceStartup + 3; stage = 2; } else if (EditorApplication.timeSinceStartup - startedAt > 420) { Log("categories never loaded"); Shot("00_timeout"); Finish(3); } break;
                    case 2: Shot("01_categories"); if (Arg("-previewLink") != null) { j.TestPickCategory("Kitchen"); until = EditorApplication.timeSinceStartup + 7; stage = 33; } else stage = 3; break;
                    case 33: Vistara.SpaceShift.Instance.StartCoroutine(Vistara.SpaceShift.Instance.Run(() => { })); until = EditorApplication.timeSinceStartup + 0.45; stage = 30; break;
                    case 30: Shot("09_link_a"); until = EditorApplication.timeSinceStartup + 0.35; stage = 31; break;
                    case 31: Shot("09_link_b"); until = EditorApplication.timeSinceStartup + 0.5; stage = 32; break;
                    case 32: Shot("09_link_c"); until = EditorApplication.timeSinceStartup + 2.5; stage = 3; break;
                    case 3:
                        if (ci < cats.Length && ci == 0 && Arg("-previewShift") != null) { j.TestPickCategory(cats[ci]); until = EditorApplication.timeSinceStartup + 0.05; stage = 50; }       // shots in the middle of the menu -> category shift
                        else if (ci < cats.Length) { j.TestPickCategory(cats[ci]); until = EditorApplication.timeSinceStartup + 5; stage = 4; }       // 5 s: the spatial shift (~1.7 s) + world build
                        else if (sports != null) { stage = 20; }
                        else if (Arg("-previewCatsOnly") != null) { stage = 99; }
                        else { j.TestPickCategory("Kitchen"); until = EditorApplication.timeSinceStartup + 6; stage = 5; startedAt = EditorApplication.timeSinceStartup; }
                        break;
                    case 4: Shot("02_cat_" + cats[ci]); ci++; stage = 3; until = EditorApplication.timeSinceStartup + 1; break;
                    case 5: Shot("02_cat_Kitchen_again"); stage = 6; break;          // videos exist locally for Kitchen only
                    case 6:
                        if (j.VideosReady) { Log("videos ready"); j.TestPickFirstVideo(0); until = EditorApplication.timeSinceStartup + 3; stage = 7; }
                        else if (EditorApplication.timeSinceStartup - startedAt > 400) { Log("videos never ready"); stage = 99; }
                        break;
                    case 7:
                        Shot("03_video_chosen");
                        if (Arg("-previewMock") != null) { var m = MockFor(j.SelectedPath); if (m != null) { j.TestShowResult(m); until = EditorApplication.timeSinceStartup + 6; stage = 9; startedAt = EditorApplication.timeSinceStartup; break; } Log("no saved output for the chosen video"); }
                        j.StartAnalysis(); until = EditorApplication.timeSinceStartup + 6; stage = 8; break;
                    case 8: Shot("04_analysing"); stage = 9; startedAt = EditorApplication.timeSinceStartup; break;
                    case 9:
                        if (j.Hud != null && j.Hud.Visible) { Log("hud visible"); until = EditorApplication.timeSinceStartup + 6; stage = 10; }
                        else if (EditorApplication.timeSinceStartup - startedAt > 900) { Log("analysis timed out"); Shot("04b_timeout"); stage = 99; }
                        else { until = EditorApplication.timeSinceStartup + 20; }
                        break;
                    case 10: Shot("05_results_a"); until = EditorApplication.timeSinceStartup + 8; stage = 11; break;
                    case 11: Shot("06_results_b"); until = EditorApplication.timeSinceStartup + 8; stage = 12; break;
                    case 12: Shot("07_results_c"); stage = Arg("-previewWalk") != null ? 40 : 99; break;
                    case 20: if (si < sports.Length) { j.TestPickCategory(sports[si]); until = EditorApplication.timeSinceStartup + 7; stage = 21; } else stage = 99; break;
                    case 21: { var gg = Vistara.GuideGirl.Instance; string[] vn = SportAct(sports[si]); if (gg != null) gg.TestAct(vn[0], vn[1]); until = EditorApplication.timeSinceStartup + 5; stage = 22; break; }
                    case 22: Shot("08_act_" + sports[si] + "_a"); until = EditorApplication.timeSinceStartup + 3.5; stage = 23; break;
                    case 23: Shot("08_act_" + sports[si] + "_b"); if (Vistara.GuideGirl.Instance != null) Vistara.GuideGirl.Instance.TestStop(); si++; stage = 20; until = EditorApplication.timeSinceStartup + 1; break;
                    case 50: case 51: case 52: case 53: case 54: case 55: Shot("11_shift_" + (stage - 50)); until = EditorApplication.timeSinceStartup + 0.12; stage++; if (stage > 55) { until = EditorApplication.timeSinceStartup + 4.5; stage = 4; } break;
                    case 40:      // walk around Vee in a circle (the XR rig is moved, the headset looks at her) and shoot every side
                    {
                        var gg = Vistara.GuideGirl.Instance; var xo = UnityEngine.Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>(); var cm = Camera.main;
                        if (wi == 0) { var dk = UnityEngine.Object.FindFirstObjectByType<Vistara.PanelDock>(); if (dk != null && !Vistara.PanelDock.Hidden) dk.ToggleHidden(); }          // the dock's Hide UI, so Vee is not covered by panels
                        if (gg == null || xo == null || cm == null || wi >= 4) { stage = 99; break; }
                        float ang = wi * 90f * Mathf.Deg2Rad; Vector3 tgt = gg.transform.position + Vector3.up * 0.65f; Vector3 pos = gg.transform.position + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 0.95f;
                        xo.transform.position += new Vector3(pos.x - cm.transform.position.x, 0.95f - cm.transform.position.y, pos.z - cm.transform.position.z);          // eye height of a small crouching viewer, so Vee is centred
                        Vector3 dir = tgt - cm.transform.position; dir.y = 0f; Vector3 fwd = cm.transform.forward; fwd.y = 0f;
                        xo.transform.RotateAround(cm.transform.position, Vector3.up, Vector3.SignedAngle(fwd, dir, Vector3.up));
                        Log("walk position " + wi); until = EditorApplication.timeSinceStartup + 1.5; wi++; stage = 41; break;
                    }
                    case 41: Shot("10_walk_" + (wi - 1)); stage = 40; break;
                    case 99: Log("done"); Finish(0); if (!daemon) stage = 100; break;
                }
            }
            catch (Exception e) { Log("EXC " + e); Finish(1); }
        }
    }
}
#endif
