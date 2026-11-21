using System;
using UnityEngine;
using UnityEngine.Video;

namespace Vistara
{
    /// <summary>Thin wrapper over VideoPlayer -> RenderTexture with a safe seek / transport API.</summary>
    public class VideoController : MonoBehaviour
    {
        public VideoPlayer Player { get; private set; }
        public RenderTexture Texture { get; private set; }
        public bool Prepared { get; private set; }
        /// <summary>True from a Seek() until the player has really arrived at the new position (reading TimeSec before that returns the OLD time).</summary>
        public bool Seeking { get; private set; }
        float seekAt;
        public string Error { get; private set; }
        public float Length => Prepared ? (float)Player.length : 0f;
        public float Time01 => Length > 0 ? Mathf.Clamp01((float)Player.time / Length) : 0f;
        public float TimeSec => Prepared ? (float)Player.time : 0f;
        /// <summary>Playing and not waiting for a seek (a seek that never reports back is given up after 2.5 s).</summary>
        public bool Settled => Prepared && (!Seeking || UnityEngine.Time.unscaledTime - seekAt > 2.5f);
        public bool IsPlaying => Player != null && Player.isPlaying;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public event Action OnPrepared;
        public event Action<string> OnError;

        float volume = 1f;

        void Awake()
        {
            Player = gameObject.AddComponent<VideoPlayer>();
            Player.playOnAwake = false;
            Player.renderMode = VideoRenderMode.RenderTexture;
            Player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 0f;
            Player.SetTargetAudioSource(0, src);
            Player.skipOnDrop = true;
            Player.isLooping = false;
            Player.prepareCompleted += p =>
            {
                Prepared = true;
                Width = (int)p.width; Height = (int)p.height;
                OnPrepared?.Invoke();
            };
            Player.seekCompleted += _ => Seeking = false;
            Player.errorReceived += (p, msg) => { Error = msg; OnError?.Invoke(msg); };
            Texture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32) { name = "VistaraVideoRT" };
            Texture.Create();
            Player.targetTexture = Texture;
        }

        int loadGen;

        /// <summary>Stop and show plain black: used when the category changes or a new video is chosen, so the previously played video never stays on the preview.</summary>
        public void ClearToBlack()
        {
            loadGen++; Prepared = false; Error = null; Seeking = false;
            if (Player != null) Player.Stop();
            if (Texture != null) { var a = RenderTexture.active; RenderTexture.active = Texture; GL.Clear(true, true, Color.black); RenderTexture.active = a; }
        }

        public void Load(string path, bool autoplay = false)
        {
            ClearToBlack();          // the old picture is gone while the new video loads
            Player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            Player.source = VideoSource.Url;
            Player.url = path.StartsWith("http") ? path : new Uri(path).AbsoluteUri;   // http(s): streamed from the engine (headset)
            Player.Prepare();
            StartCoroutine(PrepareWatchdog(++loadGen));
            if (autoplay) { void h() { OnPrepared -= h; Play(); } OnPrepared += h; }
        }

        /// <summary>Some clips (e.g. video-only downloads without an audio track) never finish preparing with audio output on: retry silently.</summary>
        System.Collections.IEnumerator PrepareWatchdog(int gen)
        {
            float t = 0f;
            while (!Prepared && gen == loadGen && t < 1.5f) { t += UnityEngine.Time.unscaledDeltaTime; yield return null; }
            if (Prepared || gen != loadGen) yield break;
            Debug.LogWarning("[Vistara] video did not prepare with audio output; retrying without audio");
            Player.Stop();
            Player.audioOutputMode = VideoAudioOutputMode.None;
            Error = null;
            Player.Prepare();
        }

        public void Play() { if (Prepared) { Player.Play(); SetVolume(volume); } }
        public void Pause() { if (Prepared) Player.Pause(); }
        public void TogglePlay() { if (IsPlaying) Pause(); else Play(); }

        public void Seek(float seconds)
        {
            if (!Prepared) return;
            Seeking = true; seekAt = UnityEngine.Time.unscaledTime;
            Player.time = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, Length - 0.05f));
            if (!Player.isPlaying) { Player.Play(); Player.Pause(); }   // refresh the displayed frame while paused
        }

        public void SeekRelative(float delta) { Seek(TimeSec + delta); }

        public void SetVolume(float v)
        {
            volume = Mathf.Clamp01(v);
            var a = GetComponent<AudioSource>(); if (a) a.volume = volume;
        }

        void OnDestroy() { if (Texture) { Texture.Release(); Destroy(Texture); } }
    }
}
