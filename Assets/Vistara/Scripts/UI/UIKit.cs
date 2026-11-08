using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Vistara
{
    /// <summary>Code-first UI factory for world-space XR canvases. Layout origin is top-left (x right, y down, in canvas pixels).</summary>
    public static class UIKit
    {
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform TL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Fill(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static Canvas WorldCanvas(string name, float w, float h, Transform parent = null, float scale = 0.001f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster), typeof(GraphicRaycaster), typeof(CanvasGroup));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.worldCamera = Camera.main;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(w, h);
            rt.localScale = Vector3.one * scale;
            var s = go.GetComponent<CanvasScaler>();
            s.dynamicPixelsPerUnit = 3f;
            s.referencePixelsPerUnit = 100f;
            return c;
        }

        public static Image Box(Transform parent, string name, Color c, int radius = 14, bool raycast = true)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = radius > 0 ? Sprites.Rounded(radius) : Sprites.White();
            img.type = radius > 0 ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = c;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Outline(Transform parent, Color c, int radius = 14, float thickness = 2f)
        {
            var img = Box(parent, "Outline", c, 0, false);
            img.sprite = Sprites.Outline(radius, thickness);
            img.type = Image.Type.Sliced;
            Fill(img.rectTransform);
            return img;
        }

        public static Image Glow(Transform parent, Color c, int radius = 18, int blur = 26, float pad = 26)
        {
            var img = Box(parent, "Glow", c, 0, false);
            img.sprite = Sprites.Glow(radius, blur);
            img.type = Image.Type.Sliced;
            Fill(img.rectTransform, -pad, -pad, -pad, -pad);
            img.transform.SetAsFirstSibling();
            return img;
        }

        public static Image Icon(Transform parent, Sprites.Icon icon, Color c, float x, float y, float s)
        {
            var img = Box(parent, "Icon", c, 0, false);
            img.sprite = Sprites.IconSprite(icon);
            TL(img.rectTransform, x, y, s, s);
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal, string name = "Text")
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Theme.Text;
            t.alignment = align;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        public static TextMeshProUGUI TextAt(Transform parent, string text, float size, float x, float y, float w, float h,
            Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            var t = Text(parent, text, size, color, align, style);
            TL(t.rectTransform, x, y, w, h);
            return t;
        }

        public static Button Button(Transform parent, string label, float x, float y, float w, float h, Action onClick,
            Color? fill = null, Color? textColor = null, Sprites.Icon? icon = null, float fontSize = 26, int radius = 14, bool glow = false)
        {
            var bg = Box(parent, "Btn_" + label, fill ?? Theme.Card, radius);
            TL(bg.rectTransform, x, y, w, h);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            if (glow) Glow(bg.transform, (fill ?? Theme.Cyan) * new Color(1, 1, 1, 0.55f), radius, 22, 14);
            Outline(bg.transform, Theme.Cyan * new Color(1, 1, 1, 0.35f), radius, 2f);
            float tx = 0, tw = w;
            if (icon.HasValue)
            {
                float s = Mathf.Min(h * 0.5f, 44);
                var ic = Icon(bg.transform, icon.Value, textColor ?? Theme.Text, 18, (h - s) / 2, s);
                ic.name = "Icon";
                tx = string.IsNullOrEmpty(label) ? 0 : s + 24; tw = w - tx;
                if (string.IsNullOrEmpty(label)) { ic.rectTransform.anchoredPosition = new Vector2((w - s) / 2, -(h - s) / 2); }
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = TextAt(bg.transform, label, fontSize, tx, 0, tw, h, textColor ?? Theme.Text,
                    icon.HasValue ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center, FontStyles.Bold);
                t.overflowMode = TextOverflowModes.Overflow;
            }
            bg.gameObject.AddComponent<HoverFx>();
            return btn;
        }

        public static Slider Slider(Transform parent, float x, float y, float w, float h, float min, float max, float value,
            Action<float> onChange, Color? fillColor = null)
        {
            var root = Rect(parent, "Slider");
            TL(root, x, y, w, h);
            var bg = Box(root, "Background", Theme.Track, (int)(h / 4f));
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f); bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, h * 0.3f);
            bg.raycastTarget = true;
            var fillArea = Rect(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-h, h * 0.3f);
            var fill = Box(fillArea, "Fill", fillColor ?? Theme.Cyan, (int)(h / 4f), false);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1); fill.rectTransform.sizeDelta = new Vector2(h * 0.5f, 0);
            var handleArea = Rect(root, "Handle Slide Area");
            Fill(handleArea, h / 2, 0, h / 2, 0);
            var handle = Box(handleArea, "Handle", Color.white, 0);
            handle.sprite = Sprites.Circle(); handle.type = Image.Type.Simple;
            handle.rectTransform.sizeDelta = new Vector2(h * 0.8f, h * 0.8f);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.transition = Selectable.Transition.None;
            if (onChange != null) s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        public static Image Bar(Transform parent, float x, float y, float w, float h, Color fill, out Image fillImg)
        {
            var bg = Box(parent, "Bar", Theme.Track, (int)(h / 2), false);
            TL(bg.rectTransform, x, y, w, h);
            fillImg = Box(bg.transform, "Fill", fill, (int)(h / 2), false);
            fillImg.rectTransform.anchorMin = Vector2.zero; fillImg.rectTransform.anchorMax = new Vector2(0, 1);
            fillImg.rectTransform.pivot = new Vector2(0, 0.5f);
            fillImg.rectTransform.offsetMin = Vector2.zero; fillImg.rectTransform.offsetMax = Vector2.zero;
            return bg;
        }

        public static void SetBar(Image fillImg, float v)
        {
            var rt = fillImg.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(v), 1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Vertical scroll list; returns the content rect (children get a VerticalLayoutGroup).</summary>
        public static RectTransform ScrollList(Transform parent, float x, float y, float w, float h, float spacing = 6, Color? bg = null)
        {
            var frame = Box(parent, "Scroll", bg ?? new Color(0, 0, 0, 0.25f), 14, true);
            TL(frame.rectTransform, x, y, w, h);
            var viewport = Rect(frame.transform, "Viewport");
            Fill(viewport, 8, 8, 8, 8);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 0);
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = frame.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = viewport; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40f; sr.inertia = true;
            return content;
        }

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static LayoutElement Sized(GameObject go, float h, float w = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
            if (w > 0) { le.minWidth = w; le.preferredWidth = w; }
            return le;
        }
    }
}
