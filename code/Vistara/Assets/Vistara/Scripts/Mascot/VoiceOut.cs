using System;
using System.Diagnostics;
using UnityEngine;

namespace Vistara
{
    /// <summary>
    /// Text-to-speech for the guide. Order of preference:
    ///  1. Meta Voice SDK (Meta.WitAi.TTS TTSSpeaker) when that package is in the project (used through reflection so the project still builds without it).
    ///  2. The headset's system text-to-speech (Android TextToSpeech; Quest ships its own voice).
    ///  3. In the Windows editor: the Windows speech synthesiser, so it can be tried with the XR Simulator.
    /// </summary>
    public static class VoiceOut
    {
        public static bool Enabled = Array.IndexOf(Environment.GetCommandLineArgs(), "-vistaraPreview") < 0 && Array.IndexOf(Environment.GetCommandLineArgs(), "-mute") < 0;       // silent in automated preview runs
        static bool inited; static Component metaSpeaker; static System.Reflection.MethodInfo metaSpeak;
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject tts; static bool ttsReady;
        sealed class InitListener : AndroidJavaProxy
        {
            public InitListener() : base("android.speech.tts.TextToSpeech$OnInitListener") { }
            public void onInit(int status) { ttsReady = status == 0; if (ttsReady && tts != null) { try { tts.Call<int>("setPitch", 1.55f); tts.Call<int>("setSpeechRate", 0.9f); } catch (Exception) { } } }
        }
#elif UNITY_EDITOR || UNITY_STANDALONE_WIN
        static Process proc;
#endif

        static void Init(GameObject host)
        {
            if (inited) return; inited = true;
            try
            {
                Type t = null; foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType("Meta.WitAi.TTS.Utilities.TTSSpeaker"); if (t != null) break; }
                if (t != null && host != null) { metaSpeaker = host.AddComponent(t); var mi = t.GetMethod("Speak", new[] { typeof(string) }); if (mi != null) metaSpeak = mi; }
            }
            catch (Exception) { metaSpeaker = null; metaSpeak = null; }
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                    tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", act, new InitListener());
            }
            catch (Exception) { tts = null; }
#endif
        }

        public static void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { if (tts != null && ttsReady) tts.Call<int>("stop"); } catch (Exception) { }
#elif UNITY_EDITOR || UNITY_STANDALONE_WIN
            try { if (proc != null && !proc.HasExited) proc.Kill(); } catch (Exception) { }
#endif
        }

        public static void Say(string text, GameObject host)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(text)) return;
            Init(host); Stop();
            string clean = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "").Replace("\"", "").Replace("'", "").Replace("\r", " ").Replace("\n", " ");
            if (metaSpeaker != null && metaSpeak != null) { try { metaSpeak.Invoke(metaSpeaker, new object[] { clean }); return; } catch (Exception) { } }
#if UNITY_ANDROID && !UNITY_EDITOR
            try { if (tts != null && ttsReady) tts.Call<int>("speak", clean, 0, null, "vee"); } catch (Exception) { }
#elif UNITY_EDITOR || UNITY_STANDALONE_WIN
            try
            {
                string ssml = "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><prosody pitch=\"+55%\" rate=\"-12%\" volume=\"soft\">" + System.Security.SecurityElement.Escape(clean) + "</prosody></speak>";
                string script = "Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; try { $s.SelectVoiceByHints([System.Speech.Synthesis.VoiceGender]::Female) } catch {}; $s.Volume = 70; $s.SpeakSsml(@'\n" + ssml + "\n'@)";
                string enc = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo("powershell", "-NoProfile -WindowStyle Hidden -EncodedCommand " + enc) { CreateNoWindow = true, UseShellExecute = false };
                proc = Process.Start(psi);
            }
            catch (Exception) { }
#endif
        }
    }
}
