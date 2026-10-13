using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>
    /// Normal maps made from the pixel art itself, so the candles and torches shade it: a sprite is raised like a
    /// cushion from its outline (rounded bodies, pillars), and its darker pixels sit lower than its lighter ones
    /// (mortar between bricks, folds of cloth, cracks). Written next to the art as name_n.png (linear, uncompressed)
    /// and given to the sprites as their "_NormalMap" (SpriteSheet.NormalTexture, or the importer for single sprites).
    /// </summary>
    public static class NormalMaps
    {
        public const string Suffix = "_n";

        /// <param name="tiles">Seamless tiles: the edges wrap around so the shading does not seam.</param>
        /// <param name="dome">How strongly the outline rounds the shape (0 for flat things like floors).</param>
        public static Texture2D Make(string path, bool tiles, float dome = 1f, float relief = 1f)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(path));
            int w = source.width, h = source.height;
            var px = source.GetPixels32();
            Object.DestroyImmediate(source);

            // Height: a dome rising over a few pixels from the outline, plus the brightness of the pixel.
            var height = new float[w * h];
            var edge = new float[w * h];
            const int Reach = 4;
            for (int i = 0; i < px.Length; i++) edge[i] = px[i].a > 8 ? Reach : 0f;
            for (int pass = 0; pass < Reach; pass++)
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (edge[i] <= 0f) continue;
                    float m = edge[i];
                    m = Mathf.Min(m, Neighbour(edge, w, h, x - 1, y, tiles) + 1f);
                    m = Mathf.Min(m, Neighbour(edge, w, h, x + 1, y, tiles) + 1f);
                    m = Mathf.Min(m, Neighbour(edge, w, h, x, y - 1, tiles) + 1f);
                    m = Mathf.Min(m, Neighbour(edge, w, h, x, y + 1, tiles) + 1f);
                    edge[i] = m;
                }
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a <= 8) continue;
                float lum = (0.3f * px[i].r + 0.59f * px[i].g + 0.11f * px[i].b) / 255f;
                float rim = Mathf.Sqrt(Mathf.Clamp01(edge[i] / Reach));
                height[i] = dome * rim * 2.4f + relief * lum * 3.2f;
            }

            var normals = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (px[i].a <= 8)
                {
                    normals[i] = new Color32(128, 128, 255, 255);
                    continue;
                }
                float dx = Height(height, px, w, h, x + 1, y, tiles, height[i]) - Height(height, px, w, h, x - 1, y, tiles, height[i]);
                float dy = Height(height, px, w, h, x, y + 1, tiles, height[i]) - Height(height, px, w, h, x, y - 1, tiles, height[i]);
                var n = new Vector3(-dx, -dy, 0.9f).normalized;
                normals[i] = new Color32((byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(normals);
            tex.Apply();
            string outPath = NormalPath(path);
            ArtIO.WriteBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        public static string NormalPath(string path) => Path.ChangeExtension(path, null) + Suffix + ".png";

        private static float Neighbour(float[] a, int w, int h, int x, int y, bool wrap)
        {
            if (wrap) { x = (x % w + w) % w; y = (y % h + h) % h; }
            else if (x < 0 || y < 0 || x >= w || y >= h) return 0f;
            return a[y * w + x];
        }

        /// <summary>Height at a neighbour; outside the sprite counts as the floor (a little below the outline).</summary>
        private static float Height(float[] height, Color32[] px, int w, int h, int x, int y, bool wrap, float self)
        {
            if (wrap) { x = (x % w + w) % w; y = (y % h + h) % h; }
            else if (x < 0 || y < 0 || x >= w || y >= h) return self - 0.6f;
            int i = y * w + x;
            return px[i].a <= 8 ? self - 0.6f : height[i];
        }
    }
}
