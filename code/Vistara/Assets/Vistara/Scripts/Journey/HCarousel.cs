using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vistara
{
    /// <summary>
    /// Horizontal snap carousel for world-space UI: pinch-drag / finger-drag to scroll, hand swipes and arrow buttons call ScrollBy(),
    /// the focused card is enlarged. Cards are pinch / poke selectable buttons.
    /// </summary>
    public class HCarousel
    {
        public readonly RectTransform Viewport, Content;
        public int Focus { get; private set; }
        public int Count => cards.Count;

        readonly MonoBehaviour host; readonly float cardW, cardH, gap, viewW, pad;
        readonly List<RectTransform> cards = new List<RectTransform>(); readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        Coroutine snapCo;

        public HCarousel(MonoBehaviour host, RectTransform parent, float x, float y, float viewW, float viewH, float cardW, float cardH, float gap)
        {
            this.host = host; this.viewW = viewW; this.cardW = cardW; this.cardH = cardH; this.gap = gap; pad = (viewW - cardW) / 2f;
            Viewport = UIKit.Rect(parent, "Carousel"); UIKit.TL(Viewport, x, y, viewW, viewH);
            Viewport.gameObject.AddComponent<RectMask2D>();
            var bg = Viewport.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.001f);       // raycast target so a drag on empty space scrolls
            Content = UIKit.Rect(Viewport, "Content");
            Content.anchorMin = new Vector2(0, 1); Content.anchorMax = new Vector2(0, 1); Content.pivot = new Vector2(0, 1);
            Content.anchoredPosition = Vector2.zero; Content.sizeDelta = new Vector2(0, viewH);
            var h = Content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = gap; h.padding = new RectOffset((int)pad, (int)pad, 10, 10);
            h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            Content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = Viewport.gameObject.AddComponent<ScrollRect>();
            sr.content = Content; sr.viewport = Viewport; sr.horizontal = true; sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.inertia = false; sr.scrollSensitivity = 0f;
            Viewport.gameObject.AddComponent<CarouselSnap>().OnEnd = () => SnapTo(Nearest());
        }

        public void Clear() { UIKit.ClearChildren(Content); cards.Clear(); groups.Clear(); Focus = 0; Content.anchoredPosition = Vector2.zero; }

        /// <summary>Adds a card (rounded panel, pinch / poke selectable). Returns it so the caller can fill it.</summary>
        public RectTransform AddCard(Color accent, Action onSelect)
        {
            var box = UIKit.Box(Content, "Card", Theme.Panel, 26);
            UIKit.Sized(box.gameObject, cardH, cardW); box.rectTransform.sizeDelta = new Vector2(cardW, cardH);
            var cg = box.gameObject.AddComponent<CanvasGroup>();
            UIKit.Outline(box.transform, accent * new Color(1, 1, 1, 0.7f), 26, 2.5f);
            var btn = box.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.targetGraphic = box;
            box.gameObject.AddComponent<HoverFx>();
            if (onSelect != null) btn.onClick.AddListener(() => onSelect());
            cards.Add(box.rectTransform); groups.Add(cg);
            return box.rectTransform;
        }

        /// <summary>World position of the focused card centre (used by the automated touch test).</summary>
        public Vector3 FocusedWorld() { var r = cards[Mathf.Clamp(Focus, 0, cards.Count - 1)]; return r.TransformPoint(r.rect.center); }

        float CenterX(int i) { return pad + i * (cardW + gap) + cardW / 2f; }
        float ContentWidth() { return 2 * pad + cards.Count * cardW + Mathf.Max(0, cards.Count - 1) * gap; }
        float TargetX(int i) { return Mathf.Clamp(viewW / 2f - CenterX(i), viewW - ContentWidth(), 0f); }

        public int Nearest()
        {
            if (cards.Count == 0) return 0;
            float centre = viewW / 2f - Content.anchoredPosition.x;
            return Mathf.Clamp(Mathf.RoundToInt((centre - pad - cardW / 2f) / (cardW + gap)), 0, cards.Count - 1);
        }

        public void ScrollBy(int n) { if (cards.Count > 0) SnapTo(Mathf.Clamp(Nearest() + n, 0, cards.Count - 1)); }

        public void SnapTo(int idx, bool instant = false)
        {
            if (cards.Count == 0) return;
            Focus = idx;
            if (snapCo != null) host.StopCoroutine(snapCo);
            if (instant) { Content.anchoredPosition = new Vector2(TargetX(idx), 0); return; }
            snapCo = host.StartCoroutine(SnapRoutine(TargetX(idx)));
        }

        IEnumerator SnapRoutine(float x)
        {
            while (Mathf.Abs(Content.anchoredPosition.x - x) > 0.5f)
            {
                var p = Content.anchoredPosition; p.x = Mathf.Lerp(p.x, x, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f)); Content.anchoredPosition = p;
                yield return null;
            }
            Content.anchoredPosition = new Vector2(x, 0);
        }

        /// <summary>Call every frame while visible: focused card grows, the others fade a little.</summary>
        public void Tick()
        {
            if (cards.Count == 0) return;
            int near = Nearest();
            for (int i = 0; i < cards.Count; i++)
            {
                float s = Mathf.Lerp(cards[i].localScale.x, i == near ? 1.06f : 0.92f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
                cards[i].localScale = Vector3.one * s;
                groups[i].alpha = Mathf.Lerp(groups[i].alpha, i == near ? 1f : 0.72f, 0.2f);
            }
        }
    }

    /// <summary>Raises a callback when a drag on the carousel ends so it can snap to the nearest card.</summary>
    public class CarouselSnap : MonoBehaviour, IEndDragHandler
    {
        public Action OnEnd;
        public void OnEndDrag(PointerEventData e) { OnEnd?.Invoke(); }
    }
}
