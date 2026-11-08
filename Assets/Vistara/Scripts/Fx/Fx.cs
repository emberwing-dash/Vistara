using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>Hover/press feedback for UI buttons (scale + brighten). Works with XR ray hover (pointer enter/exit).</summary>
    public class HoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        Image img; Color baseColor; float target = 1f; Vector3 baseScale;
        bool init;
        void Init() { if (init) return; init = true; img = GetComponent<Image>(); baseColor = img ? img.color : Color.white; baseScale = transform.localScale; }
        void OnEnable() { Init(); transform.localScale = baseScale; target = 1f; }
        public void OnPointerEnter(PointerEventData e) { Init(); target = 1.05f; if (img) img.color = Color.Lerp(baseColor, Color.white, 0.18f); }
        public void OnPointerExit(PointerEventData e) { Init(); target = 1f; if (img) img.color = baseColor; }
        public void OnPointerDown(PointerEventData e) { target = 0.96f; }
        public void OnPointerUp(PointerEventData e) { target = 1.05f; }
        void Update() { transform.localScale = Vector3.Lerp(transform.localScale, baseScale * target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime)); }
        public void SetBase(Color c) { Init(); baseColor = c; if (img) img.color = c; }
    }

    public class Spin : MonoBehaviour
    {
        public float degPerSec = 20f; public Vector3 axis = Vector3.forward;
        void Update() { transform.Rotate(axis, degPerSec * Time.deltaTime, Space.Self); }
    }

    public class Pulse : MonoBehaviour
    {
        public float speed = 2f, amount = 0.08f; public bool alpha = false; public float alphaMin = 0.5f;
        Vector3 s0; Graphic g; Color c0;
        void Awake() { s0 = transform.localScale; g = GetComponent<Graphic>(); if (g) c0 = g.color; }
        void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * speed);
            if (!alpha) transform.localScale = s0 * (1f + amount * k);
            else if (g) { var c = c0; c.a = Mathf.Lerp(alphaMin, 1f, k) * c0.a; g.color = c; }
        }
    }

    public class Bob : MonoBehaviour
    {
        public float amp = 0.02f, speed = 0.8f; Vector3 p0;
        void Awake() { p0 = transform.localPosition; }
        void Update() { transform.localPosition = p0 + Vector3.up * (Mathf.Sin(Time.time * speed) * amp); }
    }

    /// <summary>Fade + slide transition for panels. Call Show/Hide; safe to call repeatedly.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PanelTransition : MonoBehaviour
    {
        CanvasGroup cg; RectTransform rt; Vector2 home; bool homeSet; Coroutine co;
        void Init() { if (homeSet) return; cg = GetComponent<CanvasGroup>(); rt = (RectTransform)transform; home = rt.anchoredPosition; homeSet = true; }
        public void Show(float dur = 0.35f, float slide = 40f) { Init(); gameObject.SetActive(true); Run(1f, dur, slide, true); }
        public void Hide(float dur = 0.2f) { Init(); if (!gameObject.activeSelf) return; Run(0f, dur, -20f, false); }
        public void SetInstant(bool on) { Init(); gameObject.SetActive(on); cg.alpha = on ? 1 : 0; cg.interactable = cg.blocksRaycasts = on; rt.anchoredPosition = home; }

        void Run(float to, float dur, float slide, bool show)
        {
            if (co != null) StopCoroutine(co);
            co = StartCoroutine(Co(to, dur, slide, show));
        }

        IEnumerator Co(float to, float dur, float slide, bool show)
        {
            float from = cg.alpha;
            cg.interactable = cg.blocksRaycasts = show;
            Vector2 start = show ? home + new Vector2(0, -slide) : rt.anchoredPosition;
            Vector2 end = show ? home : home + new Vector2(0, -slide);
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / dur);
                cg.alpha = Mathf.Lerp(from, to, k);
                rt.anchoredPosition = Vector2.Lerp(start, end, k);
                yield return null;
            }
            cg.alpha = to; rt.anchoredPosition = show ? home : home;
            if (!show) gameObject.SetActive(false);
        }
    }

    public static class Anim
    {
        public static IEnumerator CountTo(Action<float> set, float from, float to, float dur)
        {
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime) { set(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / dur))); yield return null; }
            set(to);
        }
    }
}
