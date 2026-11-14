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

        /// <summary>Analyse a video that already sits on the engine's disk (no upload) - used by the Test Lab for the held-out set.</summary>
        public IEnumerator AnalyzePath(string path, Action<string> onJob, Action<string> onError)
        {
            string body = "{\"path\":\"" + path.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
            using (var r = new UnityWebRequest(BaseUrl + "/analyze_path", "POST"))
            {
                r.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)) { contentType = "application/json" };
                r.downloadHandler = new DownloadHandlerBuffer();
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { onError?.Invoke(r.downloadHandler.text.Length > 0 ? r.downloadHandler.text : r.error); yield break; }
                onJob?.Invoke(JsonUtility.FromJson<JobHandle>(r.downloadHandler.text).job_id);
            }
        }

        IEnumerator GetJson<T>(string path, Action<T> onDone, Action<string> onError, int timeout = 10)
        {
            using (var r = UnityWebRequest.Get(BaseUrl + path))
            {
                r.timeout = timeout;
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { onError?.Invoke(r.downloadHandler != null && r.downloadHandler.text.Length > 0 ? r.downloadHandler.text : r.error); yield break; }
                T v = default;
                try { v = JsonUtility.FromJson<T>(r.downloadHandler.text); } catch (Exception e) { onError?.Invoke("Bad JSON: " + e.Message); yield break; }
                onDone?.Invoke(v);
            }
        }

        public IEnumerator Categories(Action<CategoryList> ok, Action<string> err) { return GetJson("/categories", ok, err, 30); }
        public IEnumerator CategoryVideos(string id, Action<CatVideoList> ok, Action<string> err) { return GetJson("/categories/" + UnityWebRequest.EscapeURL(id) + "/videos", ok, err, 30); }

        /// <summary>Downloads an engine-served JPEG (thumbnail) as a texture.</summary>
        public IEnumerator LoadTexture(string path, Action<Texture2D> ok)
        {
            using (var r = UnityWebRequestTexture.GetTexture(BaseUrl + path))
            {
                r.timeout = 30;
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { ok?.Invoke(null); yield break; }
                ok?.Invoke(DownloadHandlerTexture.GetContent(r));
            }
        }

        /// <summary>Asks the engine for a Unity-friendly transcoded copy of a video (fallback when the VideoPlayer cannot prepare the original).</summary>
        public IEnumerator Playable(string path, Action<string> ok)
        {
            using (var r = UnityWebRequest.Get(BaseUrl + "/playable?path=" + UnityWebRequest.EscapeURL(path)))
            {
                r.timeout = 180;
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { ok?.Invoke(null); yield break; }
                var j = JsonUtility.FromJson<PlayableInfo>(r.downloadHandler.text);
                ok?.Invoke(j != null ? j.path : null);
            }
        }

        public IEnumerator ModelInfo(Action<ModelInfo> ok, Action<string> err) { return GetJson("/model_info", ok, err); }
        public IEnumerator EvalVideos(Action<EvalVideoList> ok, Action<string> err) { return GetJson("/eval/videos", ok, err, 30); }
        public IEnumerator EvalStatus(Action<EvalStatus> ok, Action<string> err) { return GetJson("/eval/status", ok, err); }
        public IEnumerator EvalResults(Action<EvalResults> ok, Action<string> err) { return GetJson("/eval/results", ok, err, 30); }

        public IEnumerator EvalStart(int limit, Action ok, Action<string> err)
        {
            using (var r = new UnityWebRequest(BaseUrl + "/eval/start", "POST"))
            {
                r.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("{\"limit\":" + limit + "}")) { contentType = "application/json" };
                r.downloadHandler = new DownloadHandlerBuffer();
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { err?.Invoke(r.downloadHandler.text.Length > 0 ? r.downloadHandler.text : r.error); yield break; }
                ok?.Invoke();
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

        /// <summary>Sends a vote (+1 more like this / -1 drop this) for a highlight; the engine re-selects and re-renders.</summary>
        public IEnumerator Feedback(string jobId, float start, float end, int vote, Action<ResultEnvelope> onDone, Action<string> onError)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string body = "{\"start\":" + start.ToString("R", ic) + ",\"end\":" + end.ToString("R", ic) + ",\"vote\":" + vote + "}";
            using (var r = new UnityWebRequest($"{BaseUrl}/jobs/{jobId}/feedback", "POST"))
            {
                r.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)) { contentType = "application/json" };
                r.downloadHandler = new DownloadHandlerBuffer();
                r.timeout = 180;
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { onError?.Invoke(r.downloadHandler.text.Length > 0 ? r.downloadHandler.text : r.error); yield break; }
                try { onDone?.Invoke(JsonUtility.FromJson<ResultEnvelope>(r.downloadHandler.text)); }
                catch (Exception e) { onError?.Invoke("Bad result JSON: " + e.Message); }
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
