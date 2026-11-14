using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Vistara
{
    /// <summary>Resolves the pointing ray behind a UI pointer event: XR (hand pinch ray via XRUIInputModule) or the mouse in the editor.</summary>
    public static class PointerRay
    {
        public static bool TryGet(PointerEventData e, out Ray ray)
        {
            if (e is TrackedDeviceEventData td && td.rayPoints != null && td.rayPoints.Count >= 2)
            {
                var a = td.rayPoints[0]; var b = td.rayPoints[td.rayPoints.Count - 1];
                if ((b - a).sqrMagnitude > 1e-8f) { ray = new Ray(a, (b - a).normalized); return true; }
            }
            var cam = e.pressEventCamera != null ? e.pressEventCamera : Camera.main;
            if (cam != null) { ray = cam.ScreenPointToRay(e.position); return true; }
            ray = default; return false;
        }

        public static bool HitPlane(PointerEventData e, Plane plane, out Vector3 hit)
        {
            hit = default;
            if (!TryGet(e, out var ray) || !plane.Raycast(ray, out float d)) return false;
            hit = ray.GetPoint(d);
            return true;
        }
    }
}
