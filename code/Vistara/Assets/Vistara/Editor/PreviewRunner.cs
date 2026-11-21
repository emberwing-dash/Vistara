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
                var o = JsonUtility.FromJson<OutputJson>(File.ReadAllText(file)); if (o == null || o.actions == null) return null;
                int n = Mathf.Max(2, Mathf.CeilToInt(o.duration_sec)); var t = new float[n]; var imp = new float[n];
                for (int i = 0; i < n; i++) { t[i] = i; float v = 0.15f + 0.1f * Mathf.PerlinNoise(i * 0.21f, 3f); foreach (var h in o.highlights) { float c = (h.segment[0] + h.segment[1]) * 0.5f, w = Mathf.Max(2f, (h.segment[1] - h.segment[0]) * 0.6f); v += 0.8f * Mathf.Exp(-((i - c) * (i - c)) / (2 * w * w)); } imp[i] = v; }
                var nouns = new System.Collections.Generic.Dictionary<string, ObjStat>();
                foreach (var a in o.actions) { if (!nouns.TryGetValue(a.noun, out var s)) nouns[a.noun] = s = new ObjStat { noun = a.noun }; s.count++; s.seconds += a.End - a.Start; }
                var an = new AnalysisJson { curve = new Curve { t = t, importance = imp, activity = imp }, objects = new System.Collections.Generic.List<ObjStat>(nouns.Values).ToArray() };
                return new ResultEnvelope { output = o, analysis = an, source_path = videoPath };
            }
            catch (Exception e) { Log("mock failed: " + e.Message); return null; }
        }

        static void HeroCam(float dist)
        {
            var m = Camera.main; var vee = Vistara.GuideGirl.Instance; var st = Vistara.MascotAssistant.Instance; if (m == null || vee == null) return;
            var mm = Vistara.MainMenuController.Instance; Vector3 center = vee.transform.position; int n = 1; if (st != null) { center += st.transform.position; n++; } if (mm != null) { center += mm.transform.position; n++; } center /= n;
            Vector3 toCam = m.transform.position - center; toCam.y = 0; if (toCam.sqrMagnitude < 0.01f) toCam = -Vector3.forward; toCam.Normalize();
            var go = new GameObject("HeroCam"); var c = go.AddComponent<Camera>(); c.depth = 50; c.fieldOfView = 38; c.clearFlags = CameraClearFlags.Skybox;
            Vector3 look = center + Vector3.up * 0.42f; go.transform.position = look + toCam * dist + Vector3.up * 0.12f; go.transform.rotation = Quaternion.LookRotation(look - go.transform.position, Vector3.up);
        }

        /// <summary>Capture framing for -previewHero: every world-space UI canvas is switched off (what Y does), and a camera in front of Vee shows only her and what she is doing.</summary>
        static void HeroOnVee()
        {
            var vee = Vistara.GuideGirl.Instance; if (vee == null) return;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c.renderMode == RenderMode.WorldSpace) c.enabled = false;
            if (Arg("-previewMainCam") != null) return;          // keep the player's own camera: what you see when the UI is hidden with Y
            var old = GameObject.Find("HeroCam"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var go = new GameObject("HeroCam"); var cam = go.AddComponent<Camera>(); cam.depth = 50; cam.fieldOfView = 42; cam.clearFlags = CameraClearFlags.Skybox;
            Vector3 f = vee.transform.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            var bnd = new Bounds(vee.transform.position + Vector3.up * 0.7f, Vector3.one * 0.1f); foreach (var r in vee.GetComponentsInChildren<Renderer>()) if (r.enabled) bnd.Encapsulate(r.bounds);
            float dist = Mathf.Clamp(bnd.size.magnitude * 1.35f, 1.7f, 4.2f); float yaw = Arg("-previewYaw") != null ? float.Parse(Arg("-previewYaw")) : (sports != null && si < sports.Length && sports[si] == "Kitchen" ? -55f : 28f);
            go.transform.position = bnd.center + Quaternion.Euler(0, yaw, 0) * f * dist + Vector3.up * 0.2f; go.transform.rotation = Quaternion.LookRotation(bnd.center - go.transform.position, Vector3.up);
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
                    if (Arg("-previewMenu") != null) { EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); stage = 60; startedAt = EditorApplication.timeSinceStartup; EditorApplication.isPlaying = true; return; }
                    EditorSceneManager.OpenScene("Assets/Scenes/Journey.unity"); stage = 1; startedAt = EditorApplication.timeSinceStartup; EditorApplication.isPlaying = true; return;
                }
                if (!EditorApplication.isPlaying) return;
                if (EditorApplication.timeSinceStartup < until) return;
                if (stage == 60) { if (GameObject.Find("HeroCam") == null && EditorApplication.timeSinceStartup - startedAt > 20) HeroCam(float.Parse(Arg("-previewMenu"))); if (EditorApplication.timeSinceStartup - startedAt > 31) { Shot("00_menu_a"); until = EditorApplication.timeSinceStartup + 4; stage = 61; } return; }
                if (stage == 61) { Shot("00_menu_b"); until = EditorApplication.timeSinceStartup + 3; stage = 62; return; }
                if (stage == 62) { Finish(0); return; }
                var j = UnityEngine.Object.FindFirstObjectByType<JourneyController>();
                if (j == null) { if (EditorApplication.timeSinceStartup - startedAt > 120) { Log("no JourneyController"); Finish(2); } return; }
                switch (stage)
                {
                    case 1: if (j.CategoriesReady) { Log("categories ready (" + j.CategoryCount + ")"); until = EditorApplication.timeSinceStartup + 3; stage = 2; } else if (EditorApplication.timeSinceStartup - startedAt > 420) { Log("categories never loaded"); Shot("00_timeout"); Finish(3); } break;
                    case 2: Shot("01_categories"); if (Arg("-previewReal") != null) { j.TestPickCategory(Arg("-previewReal")); until = EditorApplication.timeSinceStartup + 7; stage = 90; startedAt = EditorApplication.timeSinceStartup; } else if (Arg("-previewReenact") != null) { j.TestPickCategory(Arg("-previewReenact")); until = EditorApplication.timeSinceStartup + 7; stage = 80; } else if (Arg("-previewLink") != null) { j.TestPickCategory("Kitchen"); until = EditorApplication.timeSinceStartup + 7; stage = 33; } else stage = 3; break;
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
                    case 21: { var gg = Vistara.GuideGirl.Instance; string[] vn = SportAct(sports[si]); if (gg != null) gg.TestAct(vn[0], vn[1]); if (Arg("-previewHero") != null) { var st = Vistara.MascotAssistant.Instance; if (st != null) st.gameObject.SetActive(false); } until = EditorApplication.timeSinceStartup + 5; stage = 22; break; }
                    case 22: if (Arg("-previewHero") != null) HeroOnVee(); until = EditorApplication.timeSinceStartup + 0.4; stage = 28; break;
                    case 28: Shot((Arg("-previewHero") != null ? "10_hero_" : "08_act_") + sports[si] + "_a"); until = EditorApplication.timeSinceStartup + 3.5; stage = 29; break;
                    case 29: if (Arg("-previewHero") != null) HeroOnVee(); until = EditorApplication.timeSinceStartup + 0.4; stage = 23; break;
                    case 23: Shot((Arg("-previewHero") != null ? "10_hero_" : "08_act_") + sports[si] + "_b"); if (Vistara.GuideGirl.Instance != null) Vistara.GuideGirl.Instance.TestStop(); si++; stage = 20; until = EditorApplication.timeSinceStartup + 1; break;
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
                    case 90: if (j.VideosReady) { j.TestPickFirstVideo(0); until = EditorApplication.timeSinceStartup + 3; stage = 91; } else if (EditorApplication.timeSinceStartup - startedAt > 120) { Log("videos never ready"); stage = 99; } break;
                    case 91: j.StartAnalysis(); startedAt = EditorApplication.timeSinceStartup; until = EditorApplication.timeSinceStartup + 4; stage = 92; break;
                    case 92: if (j.Hud != null && j.Hud.Visible) { Log("results open"); until = EditorApplication.timeSinceStartup + 2; stage = 93; } else if (EditorApplication.timeSinceStartup - startedAt > 180) { Log("analysis timed out"); stage = 99; } break;
                    case 93: case 94: case 95: case 96: { var gg = Vistara.GuideGirl.Instance; var rn = gg != null ? gg.GetComponent<Vistara.Reenactor>() : null; Log("real flow check " + (stage - 93) + ": action=" + (gg != null ? gg.CurrentAction : "-") + " mode=" + (rn != null ? rn.Mode : "-") + " active=" + (rn != null && rn.Active)); Shot("13_real_" + Arg("-previewReal") + "_" + (stage - 93)); until = EditorApplication.timeSinceStartup + 3.5; stage++; if (stage > 96) stage = 99; break; }
                    case 80: { var m = MockFor(Arg("-previewVideoPath")); if (m == null) { Log("no saved output for the video"); stage = 99; break; } j.TestShowResult(m); until = EditorApplication.timeSinceStartup + 2.5; stage = 81; break; }
                    case 81: until = EditorApplication.timeSinceStartup + 0.2; stage = 82; break;
                    case 82: { var gg = Vistara.GuideGirl.Instance; Log("reenact check: action=" + (gg != null ? gg.CurrentAction : "no guide") + " category=" + Vistara.AppState.Category); Shot("12_reenact_" + Arg("-previewReenact") + "_a"); until = EditorApplication.timeSinceStartup + 1.5; stage = 83; break; }
                    case 83: { var gg = Vistara.GuideGirl.Instance; Log("reenact check later: action=" + (gg != null ? gg.CurrentAction : "no guide")); Shot("12_reenact_" + Arg("-previewReenact") + "_b"); stage = 99; break; }
                    case 99: Log("done"); Finish(0); if (!daemon) stage = 100; break;
                }
            }
            catch (Exception e) { Log("EXC " + e); Finish(1); }
        }
    }
}
#endif
