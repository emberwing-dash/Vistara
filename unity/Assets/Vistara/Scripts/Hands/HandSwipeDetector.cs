using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Vistara
{
    /// <summary>
    /// Horizontal hand-swipe gesture from XR Hands joint data (Quest 3 hand tracking, or the XR Interaction Simulator in hand mode):
    /// a fast, mostly lateral palm movement of at least ~22 cm while the hand is NOT pinching (pinch is the select / drag gesture).
    /// Raises OnSwipe(+1) for a swipe to the user's right and OnSwipe(-1) to the left. Needs a running XRHandSubsystem.
    /// Inject() raises the same event (used by the automated test and by the [ and ] keys in the editor).
    /// </summary>
    public class HandSwipeDetector : MonoBehaviour
    {
        public static event Action<int> OnSwipe;
        public static HandSwipeDetector Instance { get; private set; }
        public bool HandsTracked { get; private set; }
        public string Status { get; private set; } = "no hand subsystem";

        public float minSpeed = 0.9f;        // m/s lateral
        public float minDistance = 0.22f;    // m accumulated
        public float cooldown = 0.7f;

        class HandState { public bool has; public Vector3 last; public float dist; }
        readonly HandState[] st = { new HandState(), new HandState() };
        XRHandSubsystem sub; XROrigin origin; float cooldownUntil, nextFind;
        static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();

        public static HandSwipeDetector Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("HandSwipeDetector");
            return go.AddComponent<HandSwipeDetector>();
        }

        void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }

        public static void Inject(int dir) { OnSwipe?.Invoke(dir); }

        void Update()
        {
#if UNITY_EDITOR
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.leftBracketKey.wasPressedThisFrame) Inject(-1);
                if (kb.rightBracketKey.wasPressedThisFrame) Inject(+1);
            }
#endif
            if (sub == null || !sub.running)
            {
                if (Time.unscaledTime < nextFind) return;
                nextFind = Time.unscaledTime + 2f;
                subs.Clear(); SubsystemManager.GetSubsystems(subs);
                sub = null;
                foreach (var s in subs) if (s.running) { sub = s; break; }
                HandsTracked = false;
                Status = sub == null ? "no running XRHandSubsystem" : "XRHandSubsystem running";
                if (sub == null) return;
            }
            if (origin == null) origin = FindFirstObjectByType<XROrigin>();
            var cam = Camera.main;
            if (cam == null || origin == null) return;
            bool any = Process(sub.rightHand, st[0], cam);
            any |= Process(sub.leftHand, st[1], cam);
            HandsTracked = any;
            Status = any ? "hands tracked" : "XRHandSubsystem running, no hand tracked";
        }

        bool Process(XRHand h, HandState s, Camera cam)
        {
            if (!h.isTracked) { s.has = false; s.dist = 0; return false; }
            if (!h.GetJoint(XRHandJointID.Palm).TryGetPose(out var pose)) return true;
            var tr = origin.Origin != null ? origin.Origin.transform : origin.transform;
            Vector3 p = tr.TransformPoint(pose.position);
            bool pinching = false;       // thumb tip close to index tip = select / drag gesture, not a swipe
            if (h.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var tp) && h.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var ip))
                pinching = Vector3.Distance(tp.position, ip.position) < 0.035f;
            if (!s.has || pinching) { s.last = p; s.has = true; s.dist = 0; return true; }
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-3f);
            Vector3 v = (p - s.last) / dt; s.last = p;
            var right = cam.transform.right; right.y = 0; right.Normalize();
            float vx = Vector3.Dot(v, right);
            float other = Mathf.Sqrt(Mathf.Max(0f, v.sqrMagnitude - vx * vx));
            bool lateral = Mathf.Abs(vx) > minSpeed && other < 0.6f * Mathf.Abs(vx);
            if (lateral && (s.dist == 0 || Mathf.Sign(vx) == Mathf.Sign(s.dist))) s.dist += vx * dt;
            else s.dist = Mathf.MoveTowards(s.dist, 0f, dt * 0.8f);
            if (Mathf.Abs(s.dist) >= minDistance && Time.unscaledTime >= cooldownUntil)
            {
                int dir = s.dist > 0 ? 1 : -1;
                s.dist = 0; cooldownUntil = Time.unscaledTime + cooldown;
                OnSwipe?.Invoke(dir);
            }
            return true;
        }
    }
}
