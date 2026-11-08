using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Vistara
{
    /// <summary>
    /// Starts the Python engine (Backend/.venv + uvicorn) as a child process when none is listening, and stops it on quit.
    /// The engine itself is Unity-independent and can be launched manually: Backend/start_server.bat
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

        public static string BackendDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backend"));

        void TryLaunch()
        {
            try
            {
                string dir = BackendDir;
                string py = Path.Combine(dir, ".venv", "Scripts", "python.exe");
                if (!File.Exists(py)) py = Path.Combine(dir, ".venv", "bin", "python");
                if (!File.Exists(py)) { Debug.LogWarning("[Vistara] Backend venv not found; start Backend/start_server.bat manually."); return; }
                var psi = new ProcessStartInfo(py, "-m uvicorn app:app --host 127.0.0.1 --port 8765")
                {
                    WorkingDirectory = Path.Combine(dir, "server"),
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = false, RedirectStandardError = false
                };
                psi.EnvironmentVariables["PYTHONPATH"] = Path.Combine(dir, "code");
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                proc = Process.Start(psi);
                Debug.Log("[Vistara] Engine process started (pid " + proc.Id + ")");
            }
            catch (Exception e) { Debug.LogWarning("[Vistara] Could not start engine: " + e.Message); }
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
