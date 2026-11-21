using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif

namespace Vistara
{
    /// <summary>
    /// Voice in: hold the X button of the left controller, press the microphone button of the dock, or hold T in the editor, and talk to Vee. When you let go, what you said goes to her
    /// (language model, as Vee) and she answers out loud (text-to-speech, lip-sync) - voice to voice.
    /// Speech recognition: Windows (editor / PC build) uses the system dictation; on an Android headset the platform SpeechRecognizer is used where the device provides one.
    /// </summary>
    public class VoiceIn : MonoBehaviour
    {
        public static VoiceIn Instance { get; private set; }
        public static bool Listening { get; private set; }
        public static string Status = "voice: idle";

        readonly Queue<string> heard = new Queue<string>(); string lastHypothesis = ""; bool prevGrip; float listenStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // voice chat is switched off for now (the code stays): nothing is spawned, so there is no microphone input
            return;
        }
        static void Spawn() { if (Instance == null) new GameObject("VoiceIn").AddComponent<VoiceIn>(); }

        void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; _ = Microphone.devices; }          // touching Microphone makes the Android build request the microphone permission

        public static void Toggle() { if (Instance == null) return; if (Listening) Instance.StopListening(); else Instance.StartListening(); }

        void Update()
        {
            while (heard.Count > 0) { string t = heard.Dequeue(); Debug.Log("[Vistara] voice: heard \"" + t + "\""); if (GuideGirl.Instance != null) GuideGirl.Instance.Hear(t); }
            bool grip = HoldX();
            if (grip && !prevGrip) StartListening(); else if (!grip && prevGrip && Listening) StopListening();
            prevGrip = grip;
            if (Keyboard.current != null) { if (Keyboard.current.tKey.wasPressedThisFrame) StartListening(); if (Keyboard.current.tKey.wasReleasedThisFrame && Listening) StopListening(); }
            if (Listening && Time.unscaledTime - listenStart > 20f) StopListening();
        }

        /// <summary>The X button of the left controller (hold to talk).</summary>
        static bool HoldX() { var d = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand); return d.isValid && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool v) && v; }
        static bool Grip(XRNode node) { var d = InputDevices.GetDeviceAtXRNode(node); return d.isValid && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool v) && v; }

        // ---------------------------------------------------------------- start / stop
        void StartListening()
        {
            if (Listening) return; Listening = true; listenStart = Time.unscaledTime; lastHypothesis = ""; Status = "voice: listening";
            if (GuideGirl.Instance != null) GuideGirl.Instance.Speak("I'm listening...", 0f);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            StartWindows();
#elif UNITY_ANDROID
            StartAndroid();
#else
            Status = "voice: speech recognition is not available on this platform"; Debug.LogWarning("[Vistara] " + Status);
#endif
        }

        void StopListening()
        {
            if (!Listening) return; Listening = false; Status = "voice: processing";
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            StopWindows();
#elif UNITY_ANDROID
            StopAndroid();
#endif
        }

        void Deliver(string text) { if (!string.IsNullOrWhiteSpace(text)) lock (heard) heard.Enqueue(text.Trim()); Status = "voice: idle"; }

        // ---------------------------------------------------------------- Windows dictation
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        DictationRecognizer dr; string windowsFinal = "";
        void StartWindows()
        {
            try
            {
                windowsFinal = ""; dr = new DictationRecognizer(DictationTopicConstraint.Dictation);
                dr.DictationHypothesis += t => lastHypothesis = t;
                dr.DictationResult += (t, c) => { windowsFinal = (windowsFinal + " " + t).Trim(); };
                dr.DictationComplete += cause => { if (cause != DictationCompletionCause.Complete && cause != DictationCompletionCause.Canceled) Debug.LogWarning("[Vistara] dictation ended: " + cause); };
                dr.DictationError += (e, h) => { Debug.LogWarning("[Vistara] dictation error: " + e); Status = "voice: " + e; };
                dr.Start();
            }
            catch (System.Exception e) { Status = "voice: " + e.Message; Debug.LogWarning("[Vistara] " + Status); Listening = false; }
        }
        void StopWindows()
        {
            if (dr == null) return;
            var d = dr; dr = null; StartCoroutine(FinishWindows(d));
        }
        System.Collections.IEnumerator FinishWindows(DictationRecognizer d)
        {
            if (d.Status == SpeechSystemStatus.Running) d.Stop();
            yield return new WaitForSecondsRealtime(0.8f);          // the final result is delivered right after Stop
            string text = !string.IsNullOrWhiteSpace(windowsFinal) ? windowsFinal : lastHypothesis; d.Dispose(); Deliver(text);
        }
#endif

        // ---------------------------------------------------------------- Android SpeechRecognizer
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject recognizer, activity;
        class Listener : AndroidJavaProxy
        {
            readonly VoiceIn v; public Listener(VoiceIn v) : base("android.speech.RecognitionListener") { this.v = v; }
            public void onReadyForSpeech(AndroidJavaObject p) { }
            public void onBeginningOfSpeech() { }
            public void onRmsChanged(float f) { }
            public void onBufferReceived(byte[] b) { }
            public void onEndOfSpeech() { }
            public void onEvent(int t, AndroidJavaObject p) { }
            public void onPartialResults(AndroidJavaObject b) { try { var l = b.Call<AndroidJavaObject>("getStringArrayList", "results_recognition"); if (l != null && l.Call<int>("size") > 0) v.lastHypothesis = l.Call<string>("get", 0); } catch (System.Exception) { } }
            public void onError(int e) { v.Deliver(v.lastHypothesis); Debug.LogWarning("[Vistara] SpeechRecognizer error " + e); }
            public void onResults(AndroidJavaObject b) { string s = v.lastHypothesis; try { var l = b.Call<AndroidJavaObject>("getStringArrayList", "results_recognition"); if (l != null && l.Call<int>("size") > 0) s = l.Call<string>("get", 0); } catch (System.Exception) { } v.Deliver(s); }
        }
        void StartAndroid()
        {
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)) { UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone); Listening = false; Status = "voice: allow the microphone, then try again"; return; }
            try
            {
                activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        var cls = new AndroidJavaClass("android.speech.SpeechRecognizer");
                        if (!cls.CallStatic<bool>("isRecognitionAvailable", activity)) { Status = "voice: this device has no speech recognition service"; Listening = false; return; }
                        recognizer = cls.CallStatic<AndroidJavaObject>("createSpeechRecognizer", activity); recognizer.Call("setRecognitionListener", new Listener(this));
                        var intent = new AndroidJavaObject("android.content.Intent", "android.speech.action.RECOGNIZE_SPEECH");
                        intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE_MODEL", "free_form"); intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.PARTIAL_RESULTS", true);
                        recognizer.Call("startListening", intent);
                    }
                    catch (System.Exception e) { Status = "voice: " + e.Message; Listening = false; }
                }));
            }
            catch (System.Exception e) { Status = "voice: " + e.Message; Listening = false; }
        }
        void StopAndroid() { if (activity != null && recognizer != null) activity.Call("runOnUiThread", new AndroidJavaRunnable(() => { try { recognizer.Call("stopListening"); } catch (System.Exception) { } })); }
#endif
    }
}
