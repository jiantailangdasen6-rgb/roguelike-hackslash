using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>A colour ramp (dark → light). Auto-shaded parts pick a shade by lighting from the top-left.</summary>
    public sealed class PixelMaterial
    {
        public readonly Color32[] Ramp;
        public readonly int BaseShade;
        public readonly bool Fixed;

        public PixelMaterial(int baseShade, params string[] hexRamp)
        {
            Ramp = Array.ConvertAll(hexRamp, Hex);
            BaseShade = baseShade;
        }

        /// <summary>
        /// Palette swap: while set, parts built with a material listed here use its replacement instead (monster
        /// variants drawn by an existing generator in other colours, EnemyArt.Lands).
        /// </summary>
        public static System.Collections.Generic.Dictionary<int, PixelMaterial> Swaps;

        /// <summary>A material is matched by its darkest colour and ramp length (inline materials are found too).</summary>
        public static int SwapKey(PixelMaterial m) => (m.Ramp[0].r | m.Ramp[0].g << 8 | m.Ramp[0].b << 16) ^ (m.Ramp.Length << 24);

        public static PixelMaterial Swapped(PixelMaterial m) => Swaps != null && Swaps.TryGetValue(SwapKey(m), out var to) ? to : m;

        /// <summary>A single colour that is never shaded (eyes, glows, holes).</summary>
        public PixelMaterial(string hex)
        {
            Ramp = new[] { Hex(hex) };
            Fixed = true;
        }

        public static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }
    }

    /// <summary>
    /// One body part drawn as ASCII (row 0 = top). Lowercase letters are auto-shaded materials,
    /// the uppercase letter of a material forces its highlight, '.' is transparent.
    /// Pivot is in part pixels with origin at the bottom-left (y up).
    /// </summary>
    public sealed class PixelPart
    {
        public readonly string Name;
        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels; // bottom-up rows
        public readonly Vector2 Pivot;

        public PixelPart(string name, Vector2 pivot, IReadOnlyDictionary<char, PixelMaterial> materials, int shadeOffset, params string[] rows)
        {
            Name = name;
            Pivot = pivot;
            Height = rows.Length;
            Width = 0;
            foreach (var r in rows) Width = Math.Max(Width, r.Length);
            Pixels = new Color32[Width * Height];

            bool Solid(int x, int y) // y in top-down row space
            {
                if (y < 0 || y >= Height || x < 0 || x >= rows[y].Length) return false;
                return rows[y][x] != '.' && rows[y][x] != ' ';
            }

            for (int row = 0; row < Height; row++)
            for (int x = 0; x < rows[row].Length; x++)
            {
                char ch = rows[row][x];
                if (ch == '.' || ch == ' ') continue;
                bool highlight = char.IsUpper(ch) && !materials.ContainsKey(ch);
                if (!materials.TryGetValue(highlight ? char.ToLowerInvariant(ch) : ch, out var mat))
                    throw new ArgumentException($"Part '{name}': unknown material '{ch}'");
                mat = PixelMaterial.Swapped(mat);

                Color32 color;
                if (mat.Fixed)
                {
                    color = mat.Ramp[0];
                }
                else
                {
                    int shade = mat.BaseShade + shadeOffset;
                    if (!Solid(x, row - 1)) shade++;          // lit from above
                    if (!Solid(x - 1, row)) shade++;          // lit from the left
                    if (!Solid(x, row + 1)) shade--;          // underside in shadow
                    if (!Solid(x + 1, row) && Solid(x - 1, row)) shade--;
                    if (highlight) shade = mat.Ramp.Length - 1;
                    color = mat.Ramp[Mathf.Clamp(shade, 0, mat.Ramp.Length - 1)];
                }
                Pixels[(Height - 1 - row) * Width + x] = color;
            }
        }

        public Color32 Sample(int x, int y) =>
            x < 0 || y < 0 || x >= Width || y >= Height ? default : Pixels[y * Width + x];
    }

    /// <summary>Where a part goes in a frame: pivot position on the canvas, rotation (degrees, CCW), draw order.</summary>
    public struct PartPose
    {
        public PixelPart Part;
        public Vector2 Position;
        public float Angle;
        public int Z;
        public int Brightness; // −1 darkens one step toward black (back limbs), +1 brightens

        public PartPose(PixelPart part, Vector2 position, float angle, int z, int brightness = 0)
        {
            Part = part;
            Position = position;
            Angle = angle;
            Z = z;
            Brightness = brightness;
        }
    }

    /// <summary>Crescent left behind by a weapon swing: starts at <see cref="FromAngle"/> and sweeps by <see cref="Sweep"/> degrees (signed).</summary>
    public struct Smear
    {
        public Vector2 Center;
        public float InnerRadius;
        public float OuterRadius;
        public float FromAngle;
        public float Sweep;
        public Color32 Color;
    }

    public sealed class Frame
    {
        public readonly List<PartPose> Parts = new();
        public readonly List<Smear> Smears = new();
        /// <summary>Whole-body rotation (degrees) around <see cref="BodyPivot"/>, for rolls and falls.</summary>
        public float BodyAngle;
        public Vector2 BodyPivot;
        public Vector2 BodyOffset;

        public Frame Add(PixelPart part, Vector2 position, float angle, int z, int brightness = 0)
        {
            Parts.Add(new PartPose(part, position, angle, z, brightness));
            return this;
        }
    }

    public sealed class Clip
    {
        public readonly string Name;
        public readonly float Fps;
        public readonly bool Loop;
        public readonly List<Frame> Frames = new();

        public Clip(string name, float fps, bool loop)
        {
            Name = name;
            Fps = fps;
            Loop = loop;
        }
    }

    /// <summary>Software rasterizer: composes posed parts into crisp pixel frames, then outlines them.</summary>
    public static class PixelRenderer
    {
        public static readonly Color32 Outline = PixelMaterial.Hex("#0b080c");

        public static Color32[] Render(Frame frame, int size)
        {
            var color = new Color32[size * size];
            var depth = new int[size * size];
            Array.Fill(depth, int.MinValue);

            var parts = new List<PartPose>(frame.Parts);
            parts.Sort((a, b) => a.Z.CompareTo(b.Z));

            foreach (var smear in frame.Smears) DrawSmear(color, depth, size, smear, frame);
            foreach (var pose in parts) DrawPart(color, depth, size, pose, frame);

            SeparateParts(color, depth, size);
            AddOutline(color, size);
            return color;
        }

        private static Vector2 BodyTransform(Frame frame, Vector2 p)
        {
            if (frame.BodyAngle != 0f) p = Rotate(p - frame.BodyPivot, frame.BodyAngle) + frame.BodyPivot;
            return p + frame.BodyOffset;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static void DrawPart(Color32[] color, int[] depth, int size, PartPose pose, Frame frame)
        {
            var part = pose.Part;
            float angle = pose.Angle + frame.BodyAngle;
            Vector2 origin = BodyTransform(frame, pose.Position);

            // Bounding box of the transformed part.
            float reach = Mathf.Sqrt(part.Width * part.Width + part.Height * part.Height) + 1f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(origin.x - reach));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(origin.x + reach));
            int minY = Mathf.Max(0, Mathf.FloorToInt(origin.y - reach));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(origin.y + reach));

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                // Inverse-map the canvas pixel centre into part space (nearest neighbour, no holes).
                Vector2 local = Rotate(new Vector2(x + 0.5f, y + 0.5f) - origin, -angle) + part.Pivot;
                var c = part.Sample(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
                if (c.a == 0) continue;
                int i = y * size + x;
                if (depth[i] > pose.Z) continue;
                color[i] = Adjust(c, pose.Brightness);
                depth[i] = pose.Z;
            }
        }

        private static void DrawSmear(Color32[] color, int[] depth, int size, Smear smear, Frame frame)
        {
            Vector2 center = BodyTransform(frame, smear.Center);
            float sweep = Mathf.Abs(smear.Sweep);
            if (sweep < 1f) return;
            var light = Color32.Lerp(smear.Color, new Color32(255, 255, 255, 255), 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - center;
                float r = d.magnitude;
                if (r > smear.OuterRadius || r < smear.InnerRadius) continue;
                float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - frame.BodyAngle;
                // Progress along the swing: 0 at the start, 1 where the blade is now.
                float along = Mathf.Repeat((a - smear.FromAngle) * Mathf.Sign(smear.Sweep), 360f);
                if (along > sweep) continue;
                float t = along / sweep;
                // The crescent thickens toward the blade and thins out at the trailing end.
                float thickness = Mathf.Lerp(1.5f, smear.OuterRadius - smear.InnerRadius, t);
                if (r < smear.OuterRadius - thickness) continue;
                int i = y * size + x;
                color[i] = r > smear.OuterRadius - 1.5f && t > 0.3f ? light : smear.Color;
                depth[i] = -1000;
            }
        }

        /// <summary>Darkens pixels of a part that touch a part drawn in front of it, so overlapping limbs read clearly.</summary>
        private static void SeparateParts(Color32[] color, int[] depth, int size)
        {
            var copy = (Color32[])color.Clone();
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (copy[i].a == 0 || depth[i] == -1000) continue;
                bool behind = false;
                if (x > 0 && depth[i - 1] > depth[i] && copy[i - 1].a > 0) behind = true;
                if (x < size - 1 && depth[i + 1] > depth[i] && copy[i + 1].a > 0) behind = true;
                if (y > 0 && depth[i - size] > depth[i] && copy[i - size].a > 0) behind = true;
                if (y < size - 1 && depth[i + size] > depth[i] && copy[i + size].a > 0) behind = true;
                if (behind) color[i] = Color32.Lerp(copy[i], Outline, 0.65f);
            }
        }

        private static void AddOutline(Color32[] color, int size)
        {
            var copy = (Color32[])color.Clone();
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (copy[i].a != 0) continue;
                bool touch = (x > 0 && copy[i - 1].a != 0) || (x < size - 1 && copy[i + 1].a != 0)
                             || (y > 0 && copy[i - size].a != 0) || (y < size - 1 && copy[i + size].a != 0);
                if (touch) color[i] = Outline;
            }
        }

        private static Color32 Adjust(Color32 c, int brightness)
        {
            if (brightness == 0) return c;
            float k = brightness < 0 ? 0.72f : 1.18f;
            return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);
        }

        /// <summary>Writes clips as a sheet: one row per clip (top = first clip), one column per frame.</summary>
        public static void WriteSheet(string path, IReadOnlyList<Clip> clips, int frameSize, bool flash = false)
        {
            int columns = 0;
            foreach (var clip in clips) columns = Mathf.Max(columns, clip.Frames.Count);
            int w = columns * frameSize, h = clips.Count * frameSize;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(new Color32[w * h]);

            for (int row = 0; row < clips.Count; row++)
            for (int col = 0; col < clips[row].Frames.Count; col++)
            {
                var pixels = Render(clips[row].Frames[col], frameSize);
                if (flash)
                    for (int p = 0; p < pixels.Length; p++)
                        if (pixels[p].a != 0) pixels[p] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(col * frameSize, (clips.Count - 1 - row) * frameSize, frameSize, frameSize, pixels);
            }

            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ArtIO.WriteBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <summary>Enlarged preview on a dark background with frame grid, for reviewing the art.</summary>
        public static void WritePreview(string path, IReadOnlyList<Clip> clips, int frameSize, int scale)
        {
            int columns = 0;
            foreach (var clip in clips) columns = Mathf.Max(columns, clip.Frames.Count);
            int cell = frameSize * scale;
            int w = columns * cell, h = clips.Count * cell;
            var bg = PixelMaterial.Hex("#23212a");
            var bgAlt = PixelMaterial.Hex("#2b2833");
            var pixels = new Color32[w * h];

            for (int row = 0; row < clips.Count; row++)
            for (int col = 0; col < columns; col++)
            {
                var frame = col < clips[row].Frames.Count ? Render(clips[row].Frames[col], frameSize) : null;
                int ox = col * cell, oy = (clips.Count - 1 - row) * cell;
                for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                {
                    var c = (col + row) % 2 == 0 ? bg : bgAlt;
                    if (frame != null)
                    {
                        var p = frame[(y / scale) * frameSize + x / scale];
                        if (p.a != 0) c = p;
                    }
                    pixels[(oy + y) * w + ox + x] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ArtIO.WriteBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
