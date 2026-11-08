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
        public string Error { get; private set; }
        public float Length => Prepared ? (float)Player.length : 0f;
        public float Time01 => Length > 0 ? Mathf.Clamp01((float)Player.time / Length) : 0f;
        public float TimeSec => Prepared ? (float)Player.time : 0f;
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
            Player.errorReceived += (p, msg) => { Error = msg; OnError?.Invoke(msg); };
            Texture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32) { name = "VistaraVideoRT" };
            Texture.Create();
            Player.targetTexture = Texture;
        }

        public void Load(string path, bool autoplay = false)
        {
            Prepared = false; Error = null;
            Player.Stop();
            Player.source = VideoSource.Url;
            Player.url = new Uri(path).AbsoluteUri;
            Player.Prepare();
            if (autoplay) { void h() { OnPrepared -= h; Play(); } OnPrepared += h; }
        }

        public void Play() { if (Prepared) { Player.Play(); SetVolume(volume); } }
        public void Pause() { if (Prepared) Player.Pause(); }
        public void TogglePlay() { if (IsPlaying) Pause(); else Play(); }

        public void Seek(float seconds)
        {
            if (!Prepared) return;
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
