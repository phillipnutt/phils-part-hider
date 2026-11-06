using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhilsPartHider
{
    // The look Phil's mods share with PIP: flat dark panels, PIP orange,
    // Bahnschrift for names and numbers and Segoe UI for text (both ship with
    // Windows; other systems fall back to their own sans-serif). Every box and
    // symbol is drawn here in code, so the mod ships no art but its toolbar icon.
    // Made once and kept for the whole session.
    static class Look
    {
        public static readonly Color Void = Hex(0x090c13), Panel = Hex(0x111725), Panel2 = Hex(0x171f31), Panel3 = Hex(0x1e2840),
            Rail = Hex(0x0f1421), Hero = Hex(0x141b2b), Line = Hex(0x263049), Line2 = Hex(0x334063),
            Ink = Hex(0xe8ecf5), Ink2 = Hex(0x9ba7c0), Ink3 = Hex(0x66718b),
            Amber = Hex(0xffa62b), AmberInk = Hex(0x2a1700), Good = Hex(0x62d595), Warn = Hex(0xffcf5a);

        public static readonly Color AmberLine = Color.Lerp(Panel, Amber, 0.45f);
        public static readonly Color AmberHover = Color.Lerp(Amber, Color.white, 0.12f);

        // Orange washed over a background, as an opaque color.
        public static Color Wash(Color under, float amount = 0.13f) => Color.Lerp(under, Amber, amount);
        public static Color Fade(Color c, float alpha) { c.a *= alpha; return c; }
        public static string Html(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        public static Font Display, Body;
        public static Texture2D Eye, EyeSlash, Search, Cross, Up, Down;

        static bool ready;
        static readonly Dictionary<string, Sprite> rounds = new Dictionary<string, Sprite>();

        public static void Init()
        {
            if (ready) return;
            ready = true;
            Display = OsFont("Bahnschrift", "Segoe UI", "Helvetica Neue", "Arial", "Liberation Sans", "DejaVu Sans");
            Body = OsFont("Segoe UI", "Helvetica Neue", "Arial", "Liberation Sans", "DejaVu Sans");

            // Symbols, drawn at twice the size they're shown so they stay sharp at
            // larger UI scales. Coordinates run -1..1, y up.
            Eye = Glyph(48, p => Min(EyeOutline(p), Circle(p, Vector2.zero, 0.27f)));
            EyeSlash = Glyph(48, p =>
            {
                float slash = Segment(p, new Vector2(-0.66f, -0.66f), new Vector2(0.66f, 0.66f));
                float eye = Min(EyeOutline(p), Circle(p, Vector2.zero, 0.27f));
                return Min(Mathf.Max(eye, -(slash - 0.2f)), slash - 0.075f);
            });
            Search = Glyph(32, p => Min(Mathf.Abs(Circle(p, new Vector2(-0.17f, 0.17f), 0.5f)) - 0.1f,
                                        Segment(p, new Vector2(0.2f, -0.2f), new Vector2(0.78f, -0.78f)) - 0.12f));
            Cross = Glyph(32, p => Min(Segment(p, new Vector2(-0.62f, -0.62f), new Vector2(0.62f, 0.62f)),
                                       Segment(p, new Vector2(-0.62f, 0.62f), new Vector2(0.62f, -0.62f))) - 0.12f);
            Up = Glyph(20, p => Triangle(p, new Vector2(0, 0.6f), new Vector2(0.75f, -0.55f), new Vector2(-0.75f, -0.55f)));
            Down = Glyph(20, p => Triangle(p, new Vector2(0, -0.6f), new Vector2(-0.75f, 0.55f), new Vector2(0.75f, 0.55f)));
        }

        // A rounded rectangle with a 1-pixel border, as a nine-sliced sprite.
        public static Sprite Round(Color fill, Color border, int radius)
        {
            string key = ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(border) + radius;
            if (rounds.TryGetValue(key, out Sprite s) && s != null) return s;
            int size = 2 * radius + 3;
            Texture2D t = Make(size, size, (x, y) =>
            {
                float d = RoundDistance(x, y, size, radius);
                Color c = Color.Lerp(fill, border, Mathf.Clamp01(d + 1.5f));
                c.a *= Mathf.Clamp01(0.5f - d);
                return c;
            });
            float b = radius + 1;
            s = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            s.hideFlags = HideFlags.HideAndDontSave;
            return rounds[key] = s;
        }

        #region Drawing

        static Font OsFont(params string[] names)
        {
            try
            {
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
                string[] have = names.Where(installed.Contains).ToArray();
                if (have.Length > 0)
                {
                    Font f = Font.CreateDynamicFontFromOSFont(have, 14);
                    if (f != null)
                    {
                        f.hideFlags = HideFlags.HideAndDontSave;
                        return f;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PhilsPartHider] System fonts unavailable: {e.Message}");
            }
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        static Texture2D Make(int w, int h, Func<int, int, Color> pixel)
        {
            var t = new Texture2D(w, h, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = pixel(x, y);
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }

        // Signed distance from a pixel centre to a rounded rectangle filling the texture.
        static float RoundDistance(int x, int y, int size, float radius)
        {
            float half = size / 2f;
            float qx = Mathf.Abs(x + 0.5f - half) - (half - radius), qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
        }

        // A white symbol on transparent, from a signed distance function in -1..1.
        static Texture2D Glyph(int size, Func<Vector2, float> distance)
        {
            float pixel = 2f / size;
            return Make(size, size, (x, y) =>
            {
                var p = new Vector2((x + 0.5f) * pixel - 1, (y + 0.5f) * pixel - 1);
                return new Color(1, 1, 1, Mathf.Clamp01(0.5f - distance(p) / pixel));
            });
        }

        static float Min(float a, float b) => Mathf.Min(a, b);

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + t * ab)).magnitude;
        }

        // The eye's outline: a lens 1.8 wide and 1.04 tall, drawn as a ring.
        static float EyeOutline(Vector2 p)
        {
            const float w = 0.9f, h = 0.52f;
            const float d = (w * w - h * h) / (2 * h), r = d + h;
            float lens = Mathf.Max(Circle(p, new Vector2(0, -d), r), Circle(p, new Vector2(0, d), r));
            return Mathf.Abs(lens) - 0.08f;
        }

        // A filled triangle (corners in either order).
        static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float sign = Mathf.Sign((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
            float Edge(Vector2 u, Vector2 v)
            {
                Vector2 e = v - u;
                Vector2 n = new Vector2(e.y, -e.x).normalized * sign;
                return Vector2.Dot(p - u, n);
            }
            return Mathf.Max(Edge(a, b), Mathf.Max(Edge(b, c), Edge(c, a)));
        }

        #endregion
    }
}
