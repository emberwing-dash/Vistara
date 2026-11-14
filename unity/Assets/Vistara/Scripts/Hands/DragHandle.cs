using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vistara
{
    /// <summary>Pinch-and-drag a floating panel: put on a grip bar and set target to the panel root. Works with the hand pinch ray (XR UI input) and the mouse.</summary>
    public class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Transform target;
        public float minDistance = 0.6f, maxDistance = 3.0f;
        public event Action<Vector3> Dragged;
        Plane plane; Vector3 offset; bool active;

        public void OnBeginDrag(PointerEventData e)
        {
            if (target == null) return;
            plane = new Plane(target.forward, target.position);
            if (PointerRay.HitPlane(e, plane, out var hit)) { offset = target.position - hit; active = true; }
        }

        public void OnDrag(PointerEventData e)
        {
            if (!active || target == null) return;
            if (!PointerRay.HitPlane(e, plane, out var hit)) return;
            var p = hit + offset;
            var cam = Camera.main;
            if (cam != null)       // keep the panel within a comfortable reach of the user
            {
                var d = p - cam.transform.position; float m = d.magnitude;
                if (m > 1e-3f) p = cam.transform.position + d / m * Mathf.Clamp(m, minDistance, maxDistance);
            }
            target.position = p;
            Dragged?.Invoke(p);
        }

        public void OnEndDrag(PointerEventData e)
        {
            active = false;
            var cam = Camera.main;
            if (cam != null && target != null)          // face the user again (yaw only) after the move
            {
                var f = target.position - cam.transform.position; f.y = 0;
                if (f.sqrMagnitude > 0.01f) target.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            }
        }
    }

    /// <summary>Horizontal scrub bar: pinch (or click) and drag along a rect to get a 0..1 position.</summary>
    public class PointerScrub : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public event Action<float> Scrub;
        RectTransform rt;
        void Awake() { rt = (RectTransform)transform; }

        public void OnPointerDown(PointerEventData e) { Apply(e); }
        public void OnDrag(PointerEventData e) { Apply(e); }

        void Apply(PointerEventData e)
        {
            var plane = new Plane(rt.forward, rt.position);
            if (!PointerRay.HitPlane(e, plane, out var hit)) return;
            var local = rt.InverseTransformPoint(hit);
            float x = (local.x - rt.rect.xMin) / Mathf.Max(1e-3f, rt.rect.width);
            Scrub?.Invoke(Mathf.Clamp01(x));
        }
    }
}
