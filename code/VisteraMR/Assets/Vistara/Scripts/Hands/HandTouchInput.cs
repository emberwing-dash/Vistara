using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Vistara
{
    /// <summary>
    /// Direct hand input for the world-space panels, independent of the XR Interaction Toolkit UI pipeline:
    ///  * TOUCH  - push an index fingertip through a button / card (about 1 cm) = tap; keep it down and move = drag
    ///  * PINCH  - pinch (thumb + index) on a control close to the panel = press; move the pinched hand = drag (carousels, sliders, the side grips)
    /// The fingertips come from the XR Hands subsystem (Quest 3 hand tracking). The panel is meant to be held close (about 0.4 m), within arm's reach.
    /// It generates the same pointer events a mouse would (down, up, click, drag, enter, exit), so every Button / ScrollRect / Slider / DragHandle works unchanged.
    /// TestFeed lets a test inject fingertip positions without a headset.
    /// </summary>
    public class HandTouchInput : MonoBehaviour
    {
        public struct Finger { public bool tracked; public Vector3 tip, thumb; }
        public static readonly List<Canvas> Canvases = new List<Canvas>();
        public static Func<int, Finger> TestFeed;
        public static HandTouchInput Instance { get; private set; }
        public bool HandsTracked { get; private set; }
        public string Status { get; private set; } = "no hand subsystem";
        public int ClicksSent { get; private set; }

        class Ptr
        {
            public PointerEventData ped, hoverPed; public GameObject hover, raw; public Canvas canvas;
            public bool pressed, dragging, pinching, viaPinch; public Vector2 pressScreen; public float frontTime = -10f;
        }
        readonly Ptr[] ptr = { new Ptr(), new Ptr() };
        static readonly List<Graphic> gbuf = new List<Graphic>();
        static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
        XRHandSubsystem sub; XROrigin origin; float nextFind; bool xriUiOff;

        const float TouchOn = 0.008f, TouchOff = 0.030f, FrontDist = 0.022f, PinchWindow = 0.07f, Near = 0.18f, Far = -0.09f;

        public static HandTouchInput Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("HandTouchInput").AddComponent<HandTouchInput>();
        }

        void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }

        // ------------------------------------------------------------------ fingers
        Finger GetFinger(int i)
        {
            if (TestFeed != null) return TestFeed(i);
            if (sub == null || !sub.running)
            {
                if (Time.unscaledTime < nextFind) return default;
                nextFind = Time.unscaledTime + 2f;
                subs.Clear(); SubsystemManager.GetSubsystems(subs); sub = null;
                foreach (var s in subs) if (s.running) { sub = s; break; }
                Status = sub == null ? "no running XRHandSubsystem" : "XRHandSubsystem running";
                if (sub == null) return default;
            }
            if (origin == null) origin = FindFirstObjectByType<XROrigin>();
            if (origin == null) return default;
            var hand = i == 0 ? sub.rightHand : sub.leftHand;
            if (!hand.isTracked) return default;
            if (!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var ip) || !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var tp)) return default;
            var tr = origin.Origin != null ? origin.Origin.transform : origin.transform;
            return new Finger { tracked = true, tip = tr.TransformPoint(ip.position), thumb = tr.TransformPoint(tp.position) };
        }

        void Update()
        {
            bool any = false;
            for (int i = 0; i < 2; i++) { var f = GetFinger(i); any |= f.tracked; Process(i, f); }
            HandsTracked = any;
            if (any) Status = "hands tracked";
            if (any && !xriUiOff) DisableXriUi();
        }

        /// <summary>With direct hand input running, the toolkit's own UI pointers are switched off so a tap is not delivered twice.</summary>
        void DisableXriUi()
        {
            xriUiOff = true;
            foreach (var p in FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) p.enableUIInteraction = false;
            foreach (var n in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) n.enableUIInteraction = false;
            Debug.Log("[Vistara] direct hand touch / pinch input active (toolkit UI pointers off)");
        }

        // ------------------------------------------------------------------ geometry
        static float SignedDistance(Canvas c, Vector3 p)        // > 0 in front of the panel (user side), canvas forward points away from the user
        {
            var t = c.transform; return -t.InverseTransformPoint(p).z * Mathf.Abs(t.lossyScale.z);
        }

        static bool FindCanvas(Vector3 p, out Canvas best, out float bestD)
        {
            best = null; bestD = 0f;
            foreach (var c in Canvases)
            {
                if (c == null || !c.isActiveAndEnabled || c.renderMode != RenderMode.WorldSpace) continue;
                var cg = c.GetComponent<CanvasGroup>(); if (cg != null && cg.alpha < 0.05f) continue;
                float d = SignedDistance(c, p);
                if (d > Near || d < Far) continue;
                if (best == null || Mathf.Abs(d) < Mathf.Abs(bestD)) { best = c; bestD = d; }
            }
            return best != null;
        }

        static bool Blocked(Transform t, Transform stop)
        {
            for (; t != null && t != stop; t = t.parent)
                if (t.TryGetComponent(out CanvasGroup cg)) { if (!cg.blocksRaycasts) return true; if (cg.ignoreParentGroups) break; }
            return false;
        }

        static Graphic HitTest(Canvas c, Vector3 p)
        {
            c.GetComponentsInChildren(false, gbuf);
            Graphic best = null; int bestDepth = int.MinValue; float s = Mathf.Abs(c.transform.lossyScale.x);
            foreach (var g in gbuf)
            {
                if (!g.raycastTarget || !g.isActiveAndEnabled || g.canvasRenderer.cull) continue;
                var rt = g.rectTransform; var lp = rt.InverseTransformPoint(p); var r = rt.rect;
                float slop = g.GetComponent<DragHandle>() != null ? 0.018f / Mathf.Max(1e-5f, s) : 0f;      // generous target for the pinch grips
                if (lp.x < r.xMin - slop || lp.x > r.xMax + slop || lp.y < r.yMin - slop || lp.y > r.yMax + slop) continue;
                if (Blocked(g.transform, c.transform)) continue;
                if (g.canvasRenderer.absoluteDepth > bestDepth) { best = g; bestDepth = g.canvasRenderer.absoluteDepth; }
            }
            return best;
        }

        static Vector3 OnPlane(Canvas c, Vector3 p) { return p + c.transform.forward * SignedDistance(c, p); }

        // ------------------------------------------------------------------ pointer state machine
        void Process(int i, Finger f)
        {
            var p = ptr[i];
            if (!f.tracked) { Release(p, true); SetHover(p, null); return; }
            var cam = Camera.main; if (cam == null || EventSystem.current == null) return;

            float pd = Vector3.Distance(f.tip, f.thumb);
            p.pinching = p.pinching ? pd < 0.040f : pd < 0.026f;
            Vector3 P = p.pinching ? (f.tip + f.thumb) * 0.5f : f.tip;

            if (p.pressed)
            {
                float dd = SignedDistance(p.canvas, P);
                bool keep = p.viaPinch ? p.pinching : (dd < TouchOff && dd > -0.07f);
                if (!keep) { Release(p, false); return; }
                Drag(p, cam, P);
                return;
            }

            if (!FindCanvas(P, out var c, out float d)) { SetHover(p, null); return; }
            if (d > FrontDist) p.frontTime = Time.unscaledTime;
            var g = HitTest(c, P);
            SetHover(p, g != null ? g.gameObject : null);
            if (g == null) return;

            bool pinchStart = p.pinching && Mathf.Abs(d) < PinchWindow;
            bool touchStart = !p.pinching && d < TouchOn && d > -0.04f && Time.unscaledTime - p.frontTime < 0.6f;
            if (pinchStart || touchStart) Press(p, cam, c, g, P, pinchStart);
        }

        PointerEventData MakePed(Camera cam, Canvas c, Graphic g, Vector3 P)
        {
            var world = OnPlane(c, P); Vector2 sp = cam.WorldToScreenPoint(world);
            var ped = new HandPed(EventSystem.current) { hand = P, button = PointerEventData.InputButton.Left, position = sp, clickCount = 1, clickTime = Time.unscaledTime };
            ped.pointerCurrentRaycast = new RaycastResult
            {
                gameObject = g != null ? g.gameObject : null, module = c.GetComponent<GraphicRaycaster>(), screenPosition = sp, worldPosition = world,
                worldNormal = -c.transform.forward, distance = Vector3.Distance(cam.transform.position, world), depth = g != null ? g.canvasRenderer.absoluteDepth : 0
            };
            return ped;
        }

        void Press(Ptr p, Camera cam, Canvas c, Graphic g, Vector3 P, bool viaPinch)
        {
            var go = g.gameObject; var ped = MakePed(cam, c, g, P);
            ped.pressPosition = ped.position; ped.pointerPressRaycast = ped.pointerCurrentRaycast; ped.rawPointerPress = go;
            ped.pointerPress = ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerDownHandler);
            if (ped.pointerPress == null) ped.pointerPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
            ped.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(go);
            if (ped.pointerDrag != null) ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.initializePotentialDrag);
            ped.eligibleForClick = true; ped.useDragThreshold = true;
            p.ped = ped; p.canvas = c; p.raw = go; p.pressed = true; p.dragging = false; p.viaPinch = viaPinch; p.pressScreen = ped.position;
        }

        void Drag(Ptr p, Camera cam, Vector3 P)
        {
            var ped = p.ped; var c = p.canvas;
            var world = OnPlane(c, P); Vector2 sp = cam.WorldToScreenPoint(world);
            ped.delta = sp - ped.position; ped.position = sp; if (ped is HandPed hpd) hpd.hand = P;
            var rr = ped.pointerCurrentRaycast; rr.screenPosition = sp; rr.worldPosition = world; ped.pointerCurrentRaycast = rr;
            var g = HitTest(c, P); SetHover(p, g != null ? g.gameObject : null);
            float thr = Mathf.Max(EventSystem.current.pixelDragThreshold, 0.012f * Screen.width);
            if (!p.dragging && ped.pointerDrag != null && (sp - p.pressScreen).sqrMagnitude >= thr * thr)
            {
                p.dragging = true; ped.dragging = true; ped.eligibleForClick = false;
                ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.beginDragHandler);
            }
            if (p.dragging && ped.pointerDrag != null) ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.dragHandler);
        }

        void Release(Ptr p, bool cancel)
        {
            if (!p.pressed) return;
            var ped = p.ped;
            if (ped.pointerPress != null) ExecuteEvents.Execute(ped.pointerPress, ped, ExecuteEvents.pointerUpHandler);
            if (p.dragging) ExecuteEvents.Execute(ped.pointerDrag, ped, ExecuteEvents.endDragHandler);
            else if (!cancel && ped.eligibleForClick)
            {
                var pressTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(p.raw);
                var overTarget = p.hover != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(p.hover) : null;
                if (pressTarget != null && pressTarget == overTarget) { ExecuteEvents.Execute(pressTarget, ped, ExecuteEvents.pointerClickHandler); ClicksSent++; }
            }
            p.pressed = false; p.dragging = false; ped.pointerPress = null; ped.pointerDrag = null;
        }

        void SetHover(Ptr p, GameObject go)
        {
            if (go == p.hover) return;
            if (p.hoverPed == null && EventSystem.current != null) p.hoverPed = new PointerEventData(EventSystem.current);
            if (p.hoverPed == null) return;
            if (p.hover != null) ExecuteEvents.ExecuteHierarchy(p.hover, p.hoverPed, ExecuteEvents.pointerExitHandler);
            p.hover = go;
            if (go != null) ExecuteEvents.ExecuteHierarchy(go, p.hoverPed, ExecuteEvents.pointerEnterHandler);
        }
    }

    /// <summary>Registers a world-space canvas with the direct hand input while it is enabled.</summary>
    public class HandTouchCanvas : MonoBehaviour
    {
        Canvas c;
        void OnEnable() { c = GetComponent<Canvas>(); if (c != null && !HandTouchInput.Canvases.Contains(c)) HandTouchInput.Canvases.Add(c); }
        void OnDisable() { if (c != null) HandTouchInput.Canvases.Remove(c); }
    }
}
