using UnityEngine;

namespace Vistara
{
    public static class Theme
    {
        public static readonly Color Bg = Hex("070B18");
        public static readonly Color Panel = Hex("0E1530", 0.92f);
        public static readonly Color PanelHi = Hex("162046", 0.95f);
        public static readonly Color Card = Hex("1A2350", 0.85f);
        public static readonly Color Cyan = Hex("19E3FF");
        public static readonly Color Magenta = Hex("FF2E88");
        public static readonly Color Violet = Hex("8A5CFF");
        public static readonly Color Orange = Hex("FF8A1F");
        public static readonly Color Green = Hex("3DFFA6");
        public static readonly Color Text = Hex("EAF1FF");
        public static readonly Color TextDim = Hex("8E9BC4");
        public static readonly Color Track = Hex("232C58");

        public static Color Hex(string h, float a = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var c);
            c.a = a;
            return c;
        }

        /// <summary>Importance 0..1 -> cool-to-hot colour ramp.</summary>
        public static Color Ramp(float v)
        {
            v = Mathf.Clamp01(v);
            return v < 0.5f ? Color.Lerp(Violet, Cyan, v * 2f) : Color.Lerp(Cyan, Orange, (v - 0.5f) * 2f);
        }
    }
}
