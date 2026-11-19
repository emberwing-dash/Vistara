using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Vistara
{
    /// <summary>
    /// Starts the trained-model engine (Assets/Model/Engine~, FastAPI) as a child process when none is listening and stops it on quit.
    /// The model files live in Assets/Model (Weights = trained heads, Backbone~ = pretrained CLIP). The engine is Unity-independent;
    /// see Assets/Model/README_MODEL.md. Python is taken from (in order): env VISTARA_PYTHON, Assets/Model/Engine~/.venv,
    /// the repo's .venv (&lt;repo&gt;/.venv), then "python" on PATH.
    /// </summary>
    public class BackendLauncher : MonoBehaviour
    {
        Process proc;
        float nextTry;

        void Start() { TryLaunch(); }

        void Update()
        {
            var c = BackendClient.Instance;
            if (c != null && !c.Online && proc == null && Time.unscaledTime > nextTry) { nextTry = Time.unscaledTime + 30f; TryLaunch(); }
        }

        public static string ModelDir => Path.GetFullPath(Path.Combine(Application.dataPath, "Model"));
        /// <summary>Submission layout: this project sits in &lt;repo&gt;/code/Vistara, the engine is &lt;repo&gt;/code/backend (server/ + the vistara package) and the trained model is &lt;repo&gt;/weights.</summary>
        static string RepoRoot
        {
            get
            {
                var r = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
                return File.Exists(Path.Combine(r, "run.sh")) && Directory.Exists(Path.Combine(r, "code", "backend", "server")) ? r : null;
            }
        }
        public static string EngineDir
        {
            get
            {
                string own = Path.Combine(ModelDir, "Engine~");
                return Directory.Exists(Path.Combine(own, "server")) ? own : (RepoRoot ?? own);
            }
        }
        /// <summary>Dev layout: &lt;project&gt;/Backend holds server/, code/ and weights/ next to Assets/.</summary>
        static string LocalBackend
        {
            get { var b = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backend")); return Directory.Exists(Path.Combine(b, "server")) ? b : null; }
        }
        static bool OwnEngine => Directory.Exists(Path.Combine(ModelDir, "Engine~", "server"));
        /// <summary>FastAPI server folder (a local Assets/Model/Engine~ copy wins, otherwise the repo's code/backend/server).</summary>
        public static string ServerDir => LocalBackend != null && !OwnEngine ? Path.Combine(LocalBackend, "server") : (OwnEngine || RepoRoot == null ? Path.Combine(EngineDir, "server") : Path.Combine(RepoRoot, "code", "backend", "server"));
        /// <summary>Folder holding the vistara python package (PYTHONPATH).</summary>
        public static string CodeDir => LocalBackend != null && !OwnEngine ? Path.Combine(LocalBackend, "code") : (OwnEngine || RepoRoot == null ? Path.Combine(EngineDir, "code") : Path.Combine(RepoRoot, "code", "backend"));
        public static string WeightsDir
        {
            get { string own = Path.Combine(ModelDir, "Weights"); if (File.Exists(Path.Combine(own, "head.pt"))) return own; if (LocalBackend != null && RepoRoot == null) return Path.Combine(LocalBackend, "weights"); return RepoRoot == null ? own : Path.Combine(RepoRoot, "weights"); }
        }
        public static string BackboneDir
        {
            get { string own = Path.Combine(ModelDir, "Backbone~"); if (Directory.Exists(own)) return own; if (LocalBackend != null && RepoRoot == null) return Path.Combine(LocalBackend, "weights"); return RepoRoot == null ? own : Path.Combine(RepoRoot, "weights"); }
        }

        static string FindPython()
        {
            string env = Environment.GetEnvironmentVariable("VISTARA_PYTHON");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            string proj = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] candidates =
            {
                LocalBackend != null ? Path.Combine(LocalBackend, ".venv", "Scripts", "python.exe") : "",
                LocalBackend != null ? Path.Combine(LocalBackend, ".venv", "bin", "python") : "",
                Path.Combine(EngineDir, ".venv", "Scripts", "python.exe"),
                Path.Combine(EngineDir, ".venv", "bin", "python"),
                RepoRoot != null ? Path.Combine(RepoRoot, ".venv", "Scripts", "python.exe") : "",
                RepoRoot != null ? Path.Combine(RepoRoot, ".venv", "bin", "python") : "",
            };
            foreach (var c in candidates) if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
            return "python";
        }

        void TryLaunch()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return;      // on the headset the engine runs on a PC (see BackendClient engine_url.txt); nothing to launch here
#endif
            try
            {
                string server = ServerDir;
                if (!Directory.Exists(server)) { Debug.LogWarning("[Vistara] Model engine not found at " + EngineDir); return; }
                string py = FindPython();
                var psi = new ProcessStartInfo(py, "-m uvicorn app:app --host 127.0.0.1 --port 8765 --timeout-keep-alive 300")
                {
                    WorkingDirectory = server,
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = false, RedirectStandardError = false
                };
                psi.EnvironmentVariables["PYTHONPATH"] = CodeDir;
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["VISTARA_WEIGHTS"] = WeightsDir;          // trained heads
                psi.EnvironmentVariables["VISTARA_CLIP_DIR"] = BackboneDir;       // pretrained CLIP backbone
                psi.EnvironmentVariables["VISTARA_EVAL_CONFIG"] = Path.Combine(ModelDir, "Config", "eval_config.json");
                proc = Process.Start(psi);
                Debug.Log("[Vistara] Model engine started (pid " + proc.Id + ", python: " + py + ")");
            }
            catch (Exception e) { Debug.LogWarning("[Vistara] Could not start the model engine: " + e.Message); }
        }

        void OnApplicationQuit() { Kill(); }
        void OnDestroy() { if (BackendClient.Instance == null || BackendClient.Instance.gameObject == gameObject) Kill(); }

        void Kill()
        {
            try { if (proc != null && !proc.HasExited) proc.Kill(); } catch (Exception) { }
            proc = null;
        }
    }
}
