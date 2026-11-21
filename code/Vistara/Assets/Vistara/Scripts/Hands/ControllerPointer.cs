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
        XROrigin origin; float nextFind;

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

        HandTouchInput.Finger Feed(int i)
        {
            if (Time.unscaledTime >= nextFind) FindAims();
            var dev = InputDevices.GetDeviceAtXRNode(i == 0 ? XRNode.RightHand : XRNode.LeftHand);
            if (!dev.isValid || (dev.characteristics & InputDeviceCharacteristics.Controller) == 0) { down[i] = false; return default; }
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
            if (best == null) { Status = "controllers: pointing at nothing"; return default; }
            Status = down[i] ? "controllers: trigger pressed" : "controllers: pointing at a panel";
            return new HandTouchInput.Finger { tracked = true, tip = bestHit + bestN * (down[i] ? 0.003f : 0.045f), thumb = bestHit + bestN * 0.3f };
        }
    }
}
