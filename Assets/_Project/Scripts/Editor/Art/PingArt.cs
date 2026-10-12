using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// The co-op ping marks (PingView): a map pin for "come here", a warning triangle for "danger", an eye for
    /// "look here" and a banner for "gather". Drawn in greys so the game tints them with the player's colour.
    /// </summary>
    public static class PingArt
    {
        private static Color32 C(string hex) => PixelMaterial.Hex(hex);
        private static readonly Color32 Outline = PixelRenderer.Outline;
        /// <summary>Dark to light; the darkest is for holes and glyphs.</summary>
        private static readonly Color32[] Grey = { C("#3a3a42"), C("#7d7d88"), C("#a9a9b4"), C("#d2d2da"), C("#f2f2f6"), C("#ffffff") };

        public static Dictionary<string, PropArt.Sheet> Sheets() => new()
        {
            ["ping_come"] = One(Pin(), new Vector2(0.5f, 0.02f)),
            ["ping_danger"] = One(Warning(), new Vector2(0.5f, 0.02f)),
            ["ping_look"] = One(Eye(), new Vector2(0.5f, 0.02f)),
            ["ping_gather"] = One(Banner(), new Vector2(0.5f, 0.02f)),
        };

        private static PropArt.Sheet One(PixelCanvas c, Vector2 pivot) => new() { Frames = new List<PixelCanvas> { c }, Pivot = pivot };

        /// <summary>Light from the top-left: a shade index from how far up-left a pixel sits on the shape.</summary>
        private static Color32 Lit(float x, float y, float cx, float cy, float size)
        {
            float t = ((cx - x) * 0.6f + (y - cy)) / size; // -1 bottom-right .. 1 top-left
            return Grey[Mathf.Clamp(Mathf.RoundToInt(2.6f + t * 2f), 1, 5)];
        }

        private static void AddOutline(PixelCanvas c)
        {
            var copy = (Color32[])c.Pixels.Clone();
            for (int y = 0; y < c.Height; y++)
            for (int x = 0; x < c.Width; x++)
            {
                if (copy[y * c.Width + x].a != 0) continue;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= c.Width || ny >= c.Height || copy[ny * c.Width + nx].a < 200) continue;
                    c.Set(x, y, Outline);
                    break;
                }
            }
        }

        /// <summary>A map pin: a round head with a hole, tapering to a point on the ground.</summary>
        private static PixelCanvas Pin()
        {
            var c = new PixelCanvas(24, 32);
            const float cx = 11.5f, cy = 21f, r = 8.5f;
            for (int y = 1; y < 31; y++)
            for (int x = 1; x < 23; x++)
            {
                float dx = x - cx, dy = y - cy;
                bool head = dx * dx + dy * dy <= r * r;
                // The tail: a wedge from the head down to the tip at the bottom.
                float half = (y - 1.5f) / (cy - 1.5f) * r * 0.82f;
                bool tail = y < cy && Mathf.Abs(dx) <= half;
                if (!head && !tail) continue;
                bool hole = dx * dx + dy * dy <= 3.2f * 3.2f;
                c.Set(x, y, hole ? Grey[0] : Lit(x, y, cx, cy, r + 6f));
            }
            // A glint on the head.
            c.Set(7, 25, Grey[5]); c.Set(8, 26, Grey[5]); c.Set(7, 26, Grey[5]);
            AddOutline(c);
            return c;
        }

        /// <summary>A warning triangle with an exclamation mark cut into it.</summary>
        private static PixelCanvas Warning()
        {
            var c = new PixelCanvas(28, 26);
            const float cx = 13.5f, top = 23f, bottom = 2f;
            for (int y = 1; y < 25; y++)
            for (int x = 1; x < 27; x++)
            {
                if (y < bottom || y > top) continue;
                float half = (top - y) / (top - bottom) * 12.5f;
                if (Mathf.Abs(x - cx) > half) continue;
                bool bar = Mathf.Abs(x - cx) < 1.6f && y >= 9 && y <= 18;
                bool dot = Mathf.Abs(x - cx) < 1.6f && y >= 4 && y <= 6;
                c.Set(x, y, bar || dot ? Grey[0] : Lit(x, y, cx, 12f, 14f));
            }
            AddOutline(c);
            return c;
        }

        /// <summary>An open eye: almond white, iris, pupil and a glint.</summary>
        private static PixelCanvas Eye()
        {
            var c = new PixelCanvas(30, 20);
            const float cx = 14.5f, cy = 10f;
            for (int y = 1; y < 19; y++)
            for (int x = 1; x < 29; x++)
            {
                float u = (x - cx) / 13f;
                float half = 7.5f * (1f - u * u);
                if (Mathf.Abs(y - cy) > half) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                c.Set(x, y, d <= 2.2f ? Grey[0] : d <= 5.2f ? Grey[2] : Lit(x, y, cx, cy, 12f));
            }
            c.Set(12, 12, Grey[5]); c.Set(13, 12, Grey[5]); c.Set(12, 11, Grey[5]);
            AddOutline(c);
            return c;
        }

        /// <summary>A banner on a pole (gather in that room).</summary>
        private static PixelCanvas Banner()
        {
            var c = new PixelCanvas(24, 32);
            for (int y = 1; y < 31; y++) { c.Set(5, y, Grey[2]); c.Set(6, y, Grey[1]); }
            c.Set(5, 31, Grey[4]); c.Set(6, 31, Grey[3]);
            for (int y = 14; y < 29; y++)
            for (int x = 7; x < 22; x++)
            {
                // A swallow-tailed flag that ripples a little.
                float wave = Mathf.Sin(x * 0.7f) * 1.2f;
                if (y < 14 + wave || y > 28 + wave) continue;
                if (x > 17 && Mathf.Abs(y - (21f + wave)) < (x - 17) * 0.9f) continue; // the notch
                c.Set(x, y, Lit(x, y, 13f, 22f, 10f));
            }
            AddOutline(c);
            return c;
        }
    }
}
