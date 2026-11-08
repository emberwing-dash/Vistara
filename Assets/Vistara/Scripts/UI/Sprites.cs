using System.Collections.Generic;
using UnityEngine;

namespace Vistara
{
    /// <summary>Procedurally generated sprites (rounded rects, outlines, gradients, glyph icons). No external art needed.</summary>
    public static class Sprites
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            return t;
        }

        // signed distance to rounded rect centred in (w,h)
        static float RoundedSdf(float x, float y, float w, float h, float r)
        {
            float px = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
            float py = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(px, 0), oy = Mathf.Max(py, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(px, py), 0) - r;
        }

        public static Sprite Rounded(int radius)
        {
            string key = "r" + radius;
            if (cache.TryGetValue(key, out var s)) return s;
            int size = radius * 2 + 4;
            var t = NewTex(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedSdf(x + 0.5f, y + 0.5f, size, size, radius);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(0.5f - d)));
                }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            cache[key] = s;
            return s;
        }

        public static Sprite Outline(int radius, float thickness = 2f)
        {
            string key = $"o{radius}_{thickness}";
            if (cache.TryGetValue(key, out var s)) return s;
            int size = radius * 2 + 4;
            var t = NewTex(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedSdf(x + 0.5f, y + 0.5f, size, size, radius);
                    float a = Mathf.Clamp01(0.5f - Mathf.Abs(d + thickness * 0.5f) + thickness * 0.5f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            cache[key] = s;
            return s;
        }

        /// <summary>Soft glow: rounded rect with a gaussian-ish falloff, for neon halos.</summary>
        public static Sprite Glow(int radius = 24, int blur = 24)
        {
            string key = $"g{radius}_{blur}";
            if (cache.TryGetValue(key, out var s)) return s;
            int size = (radius + blur) * 2 + 4;
            var t = NewTex(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedSdf(x + 0.5f, y + 0.5f, size - blur * 2, size - blur * 2, radius);
                    d = RoundedSdf(x + 0.5f - blur, y + 0.5f - blur, size - blur * 2, size - blur * 2, radius);
                    float a = d <= 0 ? 0f : Mathf.Exp(-(d * d) / (blur * blur * 0.35f));
                    t.SetPixel(x, y, new Color(1, 1, 1, a * 0.9f));
                }
            t.Apply();
            int b = radius + blur;
            s = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            cache[key] = s;
            return s;
        }

        public static Sprite Circle()
        {
            if (cache.TryGetValue("circle", out var s)) return s;
            int n = 128;
            var t = NewTex(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) - n / 2f + 1.5f;
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(0.5f - d)));
                }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            cache["circle"] = s;
            return s;
        }

        public static Sprite Ring(float thickness = 0.06f)
        {
            string key = "ring" + thickness;
            if (cache.TryGetValue(key, out var s)) return s;
            int n = 256;
            var t = NewTex(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - (1f - thickness)) / thickness * 1.0f);
                    // add gaps so the rotating ring looks like a hologram dial
                    float ang = Mathf.Atan2(y - n / 2f, x - n / 2f);
                    float gap = Mathf.Sin(ang * 6f) > 0.85f ? 0.15f : 1f;
                    t.SetPixel(x, y, new Color(1, 1, 1, a * gap));
                }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = s;
            return s;
        }

        public static Sprite VGradient()
        {
            if (cache.TryGetValue("vgrad", out var s)) return s;
            var t = NewTex(2, 64);
            for (int y = 0; y < 64; y++) { float a = y / 63f; for (int x = 0; x < 2; x++) t.SetPixel(x, y, new Color(1, 1, 1, a)); }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, 2, 64), new Vector2(0.5f, 0.5f), 100f);
            cache["vgrad"] = s;
            return s;
        }

        public static Sprite White()
        {
            if (cache.TryGetValue("white", out var s)) return s;
            var t = NewTex(4, 4);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) t.SetPixel(x, y, Color.white);
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            cache["white"] = s;
            return s;
        }

        // ---------------------- glyph icons ----------------------
        public enum Icon { Play, Pause, Back, Forward, Prev, Next, Folder, Film, Up, Chevron, ChevronL, Bolt, Check, Download, Home, Chart, Refresh, Stop }

        public static Sprite IconSprite(Icon icon)
        {
            string key = "i" + icon;
            if (cache.TryGetValue(key, out var s)) return s;
            const int N = 128;
            var px = new float[N * N];
            const int SS = 2;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float acc = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                            acc += Inside(icon, (x + (sx + 0.5f) / SS) / N, (y + (sy + 0.5f) / SS) / N) ? 1f : 0f;
                    px[y * N + x] = acc / (SS * SS);
                }
            var t = NewTex(N, N);
            var cols = new Color[N * N];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color(1, 1, 1, px[i]);
            t.SetPixels(cols); t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = s;
            return s;
        }

        static bool Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (x - b.x) * (a.y - b.y) - (a.x - b.x) * (y - b.y);
            float d2 = (x - c.x) * (b.y - c.y) - (b.x - c.x) * (y - c.y);
            float d3 = (x - a.x) * (c.y - a.y) - (c.x - a.x) * (y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
        static bool Rect(float x, float y, float x0, float y0, float x1, float y1) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
        static bool Seg(float x, float y, Vector2 a, Vector2 b, float w)
        {
            var p = new Vector2(x, y); var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t) <= w;
        }

        static bool Inside(Icon icon, float x, float y)
        {
            switch (icon)
            {
                case Icon.Play: return Tri(x, y, new Vector2(.30f, .15f), new Vector2(.30f, .85f), new Vector2(.84f, .5f));
                case Icon.Pause: return Rect(x, y, .24f, .18f, .42f, .82f) || Rect(x, y, .58f, .18f, .76f, .82f);
                case Icon.Stop: return Rect(x, y, .22f, .22f, .78f, .78f);
                case Icon.Back: return Tri(x, y, new Vector2(.12f, .5f), new Vector2(.50f, .2f), new Vector2(.50f, .8f)) || Tri(x, y, new Vector2(.46f, .5f), new Vector2(.84f, .2f), new Vector2(.84f, .8f));
                case Icon.Forward: return Tri(x, y, new Vector2(.88f, .5f), new Vector2(.50f, .2f), new Vector2(.50f, .8f)) || Tri(x, y, new Vector2(.54f, .5f), new Vector2(.16f, .2f), new Vector2(.16f, .8f));
                case Icon.Prev: return Rect(x, y, .18f, .2f, .28f, .8f) || Tri(x, y, new Vector2(.82f, .2f), new Vector2(.82f, .8f), new Vector2(.30f, .5f));
                case Icon.Next: return Rect(x, y, .72f, .2f, .82f, .8f) || Tri(x, y, new Vector2(.18f, .2f), new Vector2(.18f, .8f), new Vector2(.70f, .5f));
                case Icon.Chevron: return Seg(x, y, new Vector2(.35f, .2f), new Vector2(.68f, .5f), .07f) || Seg(x, y, new Vector2(.68f, .5f), new Vector2(.35f, .8f), .07f);
                case Icon.ChevronL: return Seg(x, y, new Vector2(.65f, .2f), new Vector2(.32f, .5f), .07f) || Seg(x, y, new Vector2(.32f, .5f), new Vector2(.65f, .8f), .07f);
                case Icon.Up: return Seg(x, y, new Vector2(.5f, .2f), new Vector2(.5f, .8f), .06f) || Seg(x, y, new Vector2(.5f, .8f), new Vector2(.25f, .55f), .06f) || Seg(x, y, new Vector2(.5f, .8f), new Vector2(.75f, .55f), .06f);
                case Icon.Folder: return Rect(x, y, .10f, .22f, .90f, .74f) || Rect(x, y, .10f, .74f, .42f, .84f);
                case Icon.Film:
                    if (!Rect(x, y, .12f, .2f, .88f, .8f)) return false;
                    for (int i = 0; i < 4; i++) { float cy = .3f + i * .13f; if (Rect(x, y, .15f, cy - .03f, .22f, cy + .03f) || Rect(x, y, .78f, cy - .03f, .85f, cy + .03f)) return false; }
                    return !Rect(x, y, .28f, .28f, .72f, .72f) || Tri(x, y, new Vector2(.42f, .36f), new Vector2(.42f, .64f), new Vector2(.62f, .5f));
                case Icon.Bolt: return Tri(x, y, new Vector2(.55f, .92f), new Vector2(.22f, .45f), new Vector2(.5f, .45f)) || Tri(x, y, new Vector2(.45f, .08f), new Vector2(.78f, .55f), new Vector2(.5f, .55f));
                case Icon.Check: return Seg(x, y, new Vector2(.2f, .5f), new Vector2(.42f, .28f), .07f) || Seg(x, y, new Vector2(.42f, .28f), new Vector2(.8f, .74f), .07f);
                case Icon.Download: return Seg(x, y, new Vector2(.5f, .85f), new Vector2(.5f, .35f), .06f) || Seg(x, y, new Vector2(.5f, .25f), new Vector2(.28f, .5f), .06f) || Seg(x, y, new Vector2(.5f, .25f), new Vector2(.72f, .5f), .06f) || Rect(x, y, .2f, .12f, .8f, .18f);
                case Icon.Home: return Tri(x, y, new Vector2(.1f, .5f), new Vector2(.9f, .5f), new Vector2(.5f, .88f)) || Rect(x, y, .22f, .15f, .78f, .5f);
                case Icon.Chart: return Rect(x, y, .16f, .15f, .30f, .45f) || Rect(x, y, .43f, .15f, .57f, .85f) || Rect(x, y, .70f, .15f, .84f, .62f);
                case Icon.Refresh: { float dx = x - .5f, dy = y - .5f; float r = Mathf.Sqrt(dx * dx + dy * dy); float ang = Mathf.Atan2(dy, dx); return r > .26f && r < .38f && !(ang > 0.2f && ang < 0.9f) || Tri(x, y, new Vector2(.62f, .86f), new Vector2(.62f, .62f), new Vector2(.86f, .74f)); }
            }
            return false;
        }
    }
}
