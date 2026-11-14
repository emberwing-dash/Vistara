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
    /// the sibling Vistara project's Backend/.venv, then "python" on PATH.
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
        public static string EngineDir => Path.Combine(ModelDir, "Engine~");

        static string FindPython()
        {
            string env = Environment.GetEnvironmentVariable("VISTARA_PYTHON");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            string proj = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] candidates =
            {
                Path.Combine(EngineDir, ".venv", "Scripts", "python.exe"),
                Path.Combine(EngineDir, ".venv", "bin", "python"),
                Path.GetFullPath(Path.Combine(proj, "..", "Vistara", "Backend", ".venv", "Scripts", "python.exe")),
                Path.GetFullPath(Path.Combine(proj, "..", "Vistara", "Backend", ".venv", "bin", "python")),
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
            return "python";
        }

        void TryLaunch()
        {
            try
            {
                string server = Path.Combine(EngineDir, "server");
                if (!Directory.Exists(server)) { Debug.LogWarning("[Vistara] Model engine not found at " + EngineDir); return; }
                string py = FindPython();
                var psi = new ProcessStartInfo(py, "-m uvicorn app:app --host 127.0.0.1 --port 8765")
                {
                    WorkingDirectory = server,
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = false, RedirectStandardError = false
                };
                psi.EnvironmentVariables["PYTHONPATH"] = Path.Combine(EngineDir, "code");
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["VISTARA_WEIGHTS"] = Path.Combine(ModelDir, "Weights");          // trained heads
                psi.EnvironmentVariables["VISTARA_CLIP_DIR"] = Path.Combine(ModelDir, "Backbone~");       // pretrained CLIP backbone
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
