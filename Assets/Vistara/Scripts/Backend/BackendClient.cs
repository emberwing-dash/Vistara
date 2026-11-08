using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Vistara
{
    /// <summary>HTTP client for the local Python engine. All calls are coroutines with callbacks (no threading surprises in XR).</summary>
    public class BackendClient : MonoBehaviour
    {
        public const string DefaultUrl = "http://127.0.0.1:8765";
        public static BackendClient Instance { get; private set; }
        public string BaseUrl = DefaultUrl;
        public bool Online { get; private set; }
        public bool Cuda { get; private set; }
        public event Action<bool> OnHealthChanged;

        public static BackendClient Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("BackendClient");
            return go.AddComponent<BackendClient>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (GetComponent<BackendLauncher>() == null) gameObject.AddComponent<BackendLauncher>();
        }

        void Start() { StartCoroutine(HealthLoop()); }

        IEnumerator HealthLoop()
        {
            while (true)
            {
                bool ok = false;
                using (var r = UnityWebRequest.Get(BaseUrl + "/health"))
                {
                    r.timeout = 3;
                    yield return r.SendWebRequest();
                    if (r.result == UnityWebRequest.Result.Success)
                    {
                        try { var h = JsonUtility.FromJson<HealthInfo>(r.downloadHandler.text); ok = h != null && h.status == "ok"; Cuda = h != null && h.cuda; }
                        catch (Exception) { ok = false; }
                    }
                }
                if (ok != Online) { Online = ok; OnHealthChanged?.Invoke(ok); }
                yield return new WaitForSeconds(ok ? 4f : 1.5f);
            }
        }

        /// <summary>Streams the video file to the engine (UploadHandlerFile: no full-file buffering) and returns the job id.</summary>
        public IEnumerator Analyze(string path, Action<string> onJob, Action<string> onError)
        {
            if (!File.Exists(path)) { onError?.Invoke("File not found: " + path); yield break; }
            string url = $"{BaseUrl}/analyze_raw?filename={UnityWebRequest.EscapeURL(Path.GetFileName(path))}";
            using (var r = new UnityWebRequest(url, "POST"))
            {
                r.uploadHandler = new UploadHandlerFile(path) { contentType = "application/octet-stream" };
                r.downloadHandler = new DownloadHandlerBuffer();
                r.timeout = 0;
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { onError?.Invoke("Upload failed: " + r.error); yield break; }
                var h = JsonUtility.FromJson<JobHandle>(r.downloadHandler.text);
                onJob?.Invoke(h.job_id);
            }
        }

        /// <summary>Asks the engine to fetch a small (<=100 MB, <=480p) copy of a public video URL and analyse it.</summary>
        public IEnumerator AnalyzeUrl(string url, Action<string> onJob, Action<string> onError)
        {
            string body = "{\"url\":\"" + url.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
            using (var r = new UnityWebRequest(BaseUrl + "/analyze_url", "POST"))
            {
                r.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)) { contentType = "application/json" };
                r.downloadHandler = new DownloadHandlerBuffer();
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { onError?.Invoke("Request failed: " + r.error); yield break; }
                onJob?.Invoke(JsonUtility.FromJson<JobHandle>(r.downloadHandler.text).job_id);
            }
        }

        public IEnumerator Poll(string jobId, Action<JobStatus> onUpdate, Action<ResultEnvelope> onDone, Action<string> onError)
        {
            while (true)
            {
                using (var r = UnityWebRequest.Get($"{BaseUrl}/jobs/{jobId}"))
                {
                    r.timeout = 10;
                    yield return r.SendWebRequest();
                    if (r.result == UnityWebRequest.Result.Success)
                    {
                        var s = JsonUtility.FromJson<JobStatus>(r.downloadHandler.text);
                        onUpdate?.Invoke(s);
                        if (s.status == "error") { onError?.Invoke(string.IsNullOrEmpty(s.error) ? s.message : s.error); yield break; }
                        if (s.status == "done") { yield return FetchResult(jobId, onDone, onError); yield break; }
                    }
                }
                yield return new WaitForSeconds(0.4f);
            }
        }

        public IEnumerator FetchResult(string jobId, Action<ResultEnvelope> onDone, Action<string> onError)
        {
            using (var r = UnityWebRequest.Get($"{BaseUrl}/jobs/{jobId}/result"))
            {
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { onError?.Invoke("Result fetch failed: " + r.error); yield break; }
                try { onDone?.Invoke(JsonUtility.FromJson<ResultEnvelope>(r.downloadHandler.text)); }
                catch (Exception e) { onError?.Invoke("Bad result JSON: " + e.Message); }
            }
        }

        public IEnumerator History(Action<JobStatus[]> onDone)
        {
            using (var r = UnityWebRequest.Get($"{BaseUrl}/history?limit=12"))
            {
                r.timeout = 5;
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { onDone?.Invoke(new JobStatus[0]); yield break; }
                try { onDone?.Invoke(JsonUtility.FromJson<JobList>(r.downloadHandler.text).items ?? new JobStatus[0]); }
                catch (Exception) { onDone?.Invoke(new JobStatus[0]); }
            }
        }

        /// <summary>Writes the official-schema JSON next to the user's documents and returns the path.</summary>
        public IEnumerator ExportJson(string jobId, string videoId, Action<string> onPath, Action<string> onError)
        {
            using (var r = UnityWebRequest.Get($"{BaseUrl}/jobs/{jobId}/result_json"))
            {
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { onError?.Invoke(r.error); yield break; }
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Vistara", "exports");
                    Directory.CreateDirectory(dir);
                    string p = Path.Combine(dir, videoId + ".json");
                    File.WriteAllText(p, r.downloadHandler.text);
                    onPath?.Invoke(p);
                }
                catch (Exception e) { onError?.Invoke(e.Message); }
            }
        }
    }
}
