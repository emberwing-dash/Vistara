using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Vistara
{
    /// <summary>
    /// Controller input for the world-space panels: point a controller at a button and pull the trigger to press it.
    /// The pointing ray comes from the controller's interactor in the XR rig when one is active (that is the aim pose the toolkit draws its ray from),
    /// else from the device pose. The trigger comes from the XR input device, so it works on Quest controllers and in the Meta XR Simulator.
    /// It feeds HandTouchInput (the same pointer-event generator the hands use): a ray that hits a panel becomes a fingertip hovering just in front of the hit point,
    /// and the trigger pushes it onto the surface, so click, drag and hover behave exactly like touch (buttons, carousels, sliders, panel grips).
    /// The toolkit's own UI pointers on the controllers are switched off so a press is never delivered twice.
    /// </summary>
    public class ControllerPointer : MonoBehaviour
    {
        public static ControllerPointer Instance { get; private set; }
        public static string Status = "controllers: not checked";

        readonly Transform[] aim = new Transform[2];          // 0 = right, 1 = left
        readonly bool[] down = new bool[2];
        XROrigin origin; float nextFind, nextLog; readonly LineRenderer[] ray = new LineRenderer[2];

        public static ControllerPointer Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("ControllerPointer").AddComponent<ControllerPointer>();
        }

        /// <summary>True for interactors that sit under a controller object of the rig (as opposed to a hand).</summary>
        public static bool IsController(Component c)
        {
            for (var t = c.transform; t != null; t = t.parent) if (t.name.IndexOf("controller", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; HandTouchInput.ControllerFeed = Feed; HandTouchInput.Ensure();
        }

        void OnDestroy() { if (Instance == this) { Instance = null; HandTouchInput.ControllerFeed = null; } }

        void FindAims()
        {
            nextFind = Time.unscaledTime + 1f; aim[0] = aim[1] = null;
            if (origin == null) origin = FindFirstObjectByType<XROrigin>();
            foreach (var n in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!IsController(n)) continue;
                bool left = false, right = false;
                for (var t = n.transform; t != null; t = t.parent) { var nm = t.name.ToLowerInvariant(); if (nm.Contains("left")) left = true; if (nm.Contains("right")) right = true; }
                if (right && aim[0] == null) aim[0] = n.transform; else if (left && aim[1] == null) aim[1] = n.transform;
            }
            // the controller UI pointers of the toolkit are off: this pointer drives the panels, so a trigger press arrives once
            foreach (var n in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (IsController(n)) n.enableUIInteraction = false;
            foreach (var p in FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (IsController(p)) p.enableUIInteraction = false;
        }

        // ---- grab a panel by its handle: hold the trigger on a side grip / move bar and the panel follows the controller; the thumbstick pushes it away / pulls it closer.
        // PanelDock notices the new position and keeps the panel there from then on (the follow-me anchor moves with it).
        readonly DragHandle[] grabH = new DragHandle[2]; readonly Vector3[] grabOff = new Vector3[2]; readonly float[] grabDist = new float[2]; readonly bool[] prevDown = new bool[2];

        static DragHandle HandleAt(Canvas c, Vector3 hit)
        {
            foreach (var h in c.GetComponentsInChildren<DragHandle>(false))
            {
                if (h.target == null) continue; var rt = h.GetComponent<RectTransform>(); if (rt == null) continue;
                var l = rt.InverseTransformPoint(hit); if (rt.rect.Contains(new Vector2(l.x, l.y))) return h;
            }
            return null;
        }

        void MoveGrab(int i, Vector3 o, Vector3 d, InputDevice dev)
        {
            var t = grabH[i].target; if (t == null) { grabH[i] = null; return; }
            if (dev.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 st) && Mathf.Abs(st.y) > 0.2f) grabDist[i] = Mathf.Clamp(grabDist[i] + st.y * 1.2f * Time.unscaledDeltaTime, 0.4f, 2.0f);
            Vector3 want = o + d * grabDist[i] + grabOff[i]; t.position = Vector3.Lerp(t.position, want, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            var cam = Camera.main; if (cam != null)
            {
                Vector3 f = t.position - cam.transform.position; f.y = 0; if (f.sqrMagnitude > 0.01f) { float tilt = t.rotation.eulerAngles.x; if (tilt > 180f) tilt -= 360f; t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(f.normalized, Vector3.up) * Quaternion.Euler(tilt, 0f, 0f), 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime)); }
            }
        }

        readonly bool[] flick = new bool[2];
        /// <summary>While a ray is on a panel, a flick of that controller's thumbstick left / right swipes the carousels (and walking is paused so the stick does not move you).</summary>
        void FlickSwipe(int i)
        {
            FreeRoam.SuspendUntil = Time.unscaledTime + 0.2f;
            var dev = InputDevices.GetDeviceAtXRNode(i == 0 ? XRNode.RightHand : XRNode.LeftHand); if (!dev.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 st)) return;
            if (!flick[i] && Mathf.Abs(st.x) > 0.7f) { flick[i] = true; HandSwipeDetector.Inject(st.x > 0 ? -1 : 1); }          // stick right = the carousel advances
            else if (flick[i] && Mathf.Abs(st.x) < 0.3f) flick[i] = false;
        }

        void DrawRay(int i, Vector3 from, Vector3 to, bool hit)
        {
            if (ray[i] == null)
            {
                var g = new GameObject("ControllerRay" + i); g.transform.SetParent(transform, false); var l = g.AddComponent<LineRenderer>(); l.useWorldSpace = true; l.positionCount = 2; l.widthMultiplier = 0.004f;
                var sh = Shader.Find("Sprites/Default"); l.sharedMaterial = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Unlit")); l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ray[i] = l;
            }
            var lr = ray[i]; lr.enabled = true; lr.SetPosition(0, from); lr.SetPosition(1, to); lr.startColor = new Color(0.1f, 0.9f, 1f, 0.15f); lr.endColor = hit ? new Color(0.24f, 1f, 0.48f, 0.95f) : new Color(0.1f, 0.9f, 1f, 0.4f);
        }

        HandTouchInput.Finger Feed(int i)
        {
            if (Time.unscaledTime >= nextFind) FindAims();
            var dev = InputDevices.GetDeviceAtXRNode(i == 0 ? XRNode.RightHand : XRNode.LeftHand);
            if (!dev.isValid || (dev.characteristics & InputDeviceCharacteristics.Controller) == 0) { down[i] = false; if (ray[i] != null) ray[i].enabled = false; if (Time.unscaledTime >= nextLog && i == 0) { nextLog = Time.unscaledTime + 4f; Debug.Log("[Vistara] ControllerPointer: right hand device valid=" + dev.isValid + " characteristics=" + dev.characteristics); } return default; }
            Vector3 o, d;
            var a = aim[i];
            if (a != null && a.gameObject.activeInHierarchy) { o = a.position; d = a.forward; }
            else
            {
                if (origin == null) origin = FindFirstObjectByType<XROrigin>();
                if (origin == null || !dev.TryGetFeatureValue(CommonUsages.devicePosition, out var pos) || !dev.TryGetFeatureValue(CommonUsages.deviceRotation, out var rot)) return default;
                var tr = origin.Origin != null ? origin.Origin.transform : origin.transform; o = tr.TransformPoint(pos); d = tr.rotation * rot * Vector3.forward;
            }
            // the closest panel the ray points at
            Canvas best = null; float bestT = 5f; Vector3 bestHit = default, bestN = default;
            foreach (var c in HandTouchInput.Canvases)
            {
                if (c == null || !c.isActiveAndEnabled || c.renderMode != RenderMode.WorldSpace) continue;
                var cg = c.GetComponent<CanvasGroup>(); if (cg != null && cg.alpha < 0.05f) continue;
                var n = -c.transform.forward; float denom = Vector3.Dot(d, n); if (denom > -1e-4f) continue;          // pointing at the user side of the panel
                float t = Vector3.Dot(c.transform.position - o, n) / denom; if (t <= 0f || t >= bestT) continue;
                var hit = o + d * t; var local = c.transform.InverseTransformPoint(hit);
                if (!((RectTransform)c.transform).rect.Contains(new Vector2(local.x, local.y))) continue;
                best = c; bestT = t; bestHit = hit; bestN = n;
            }
            dev.TryGetFeatureValue(CommonUsages.trigger, out float trig);
            down[i] = down[i] ? trig > 0.35f : trig > 0.6f;
            if (grabH[i] != null) { if (!down[i]) { grabH[i] = null; prevDown[i] = false; return default; } MoveGrab(i, o, d, dev); prevDown[i] = true; return default; }          // holding a panel by its handle
            if (down[i] && !prevDown[i] && best != null) { var hh = HandleAt(best, bestHit); if (hh != null) { grabH[i] = hh; grabDist[i] = Mathf.Max(0.4f, Vector3.Distance(o, hh.target.position)); grabOff[i] = hh.target.position - (o + d * grabDist[i]); prevDown[i] = true; return default; } }
            prevDown[i] = down[i];
            DrawRay(i, o, best != null ? bestHit : o + d * 1.5f, best != null);
            if (Time.unscaledTime >= nextLog && i == 0) { nextLog = Time.unscaledTime + 4f; Debug.Log("[Vistara] ControllerPointer: right ctrl valid, aim=" + (a != null ? a.name : "device pose") + ", trigger=" + trig.ToString("0.00") + ", panel hit=" + (best != null ? best.name : "none") + ", panels=" + HandTouchInput.Canvases.Count); }
            if (best == null) { Status = "controllers: pointing at nothing"; return default; }
            Status = down[i] ? "controllers: trigger pressed" : "controllers: pointing at a panel";
            FlickSwipe(i);
            return new HandTouchInput.Finger { tracked = true, tip = bestHit + bestN * (down[i] ? 0.003f : 0.045f), thumb = bestHit + bestN * 0.3f, canvas = best };
        }
    }
}
