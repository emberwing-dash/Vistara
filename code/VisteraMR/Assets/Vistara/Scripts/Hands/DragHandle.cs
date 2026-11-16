using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vistara
{
    /// <summary>Pointer event produced by the direct hand input: carries the real 3D point of the pinch / fingertip.</summary>
    public class HandPed : PointerEventData
    {
        public Vector3 hand;
        public HandPed(EventSystem es) : base(es) { }
    }

    /// <summary>
    /// Pinch-and-drag a floating panel: put on a grip and set target to the panel root.
    /// With hand tracking the panel is GRABBED: it keeps the offset it had to your pinch and follows your hand in 3D (smoothed), turning to face you as it moves.
    /// With a ray / mouse it slides on its own plane.
    /// </summary>
    public class DragHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Transform target;
        public float minDistance = 0.3f, maxDistance = 1.6f;
        public event Action<Vector3> Dragged;
        Plane plane; Vector3 offset, grab; bool active, hand3d; float tilt;

        public void OnPointerDown(PointerEventData e)
        {
            if (target == null) return;
            if (e is HandPed hp)
            {
                hand3d = true; active = true; grab = target.position - hp.hand;
                float t = target.rotation.eulerAngles.x; tilt = t > 180f ? t - 360f : t;
            }
            else hand3d = false;
        }

        public void OnPointerUp(PointerEventData e) { if (hand3d) active = false; }

        public void OnBeginDrag(PointerEventData e)
        {
            if (target == null || hand3d) return;
            plane = new Plane(target.forward, target.position);
            if (PointerRay.HitPlane(e, plane, out var hit)) { offset = target.position - hit; active = true; }
        }

        Vector3 Clamp(Vector3 p)
        {
            var cam = Camera.main;
            if (cam == null) return p;
            var d = p - cam.transform.position; float m = d.magnitude;     // keep the panel within a comfortable reach of the user
            return m > 1e-3f ? cam.transform.position + d / m * Mathf.Clamp(m, minDistance, maxDistance) : p;
        }

        void FaceUser(float k)
        {
            var cam = Camera.main; if (cam == null || target == null) return;
            var f = target.position - cam.transform.position; f.y = 0;
            if (f.sqrMagnitude < 0.01f) return;
            var want = Quaternion.LookRotation(f.normalized, Vector3.up) * Quaternion.Euler(tilt, 0f, 0f);
            target.rotation = Quaternion.Slerp(target.rotation, want, k);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!active || target == null) return;
            if (hand3d && e is HandPed hp)
            {
                var want = Clamp(hp.hand + grab);
                target.position = Vector3.Lerp(target.position, want, 1f - Mathf.Exp(-28f * Time.unscaledDeltaTime));
                FaceUser(1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
                Dragged?.Invoke(target.position);
                return;
            }
            if (!PointerRay.HitPlane(e, plane, out var hit)) return;
            target.position = Clamp(hit + offset);
            Dragged?.Invoke(target.position);
        }

        public void OnEndDrag(PointerEventData e)
        {
            active = false;
            if (!hand3d && target != null) { var cam = Camera.main; if (cam != null) { float t = target.rotation.eulerAngles.x; tilt = t > 180f ? t - 360f : t; FaceUser(1f); } }
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
