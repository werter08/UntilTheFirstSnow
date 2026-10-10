using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Courtyard.Editor
{
    /// <summary>
    /// Splits the dark ink outline out of each drawn mug, so the outline can sit in front of the drink while the mug's
    /// (opaque) body stays behind it. Purely mechanical: keeps the artist's own dark pixels, no new drawing.
    /// </summary>
    internal static class MugOutlineBuilder
    {
        public const string OutputFolder = "Assets/_Game/Art/Props/Outlines";
        private const int DarkMax = 80; // the ink is ~(32,32,32); the darkest shading on any mug is ≥ 80 in some channel

        public static string OutlinePath(string mugName) => $"{OutputFolder}/{mugName}_outline.png";

        /// <summary>Writes an outline PNG for each mug whose outline is missing or older than the mug art.</summary>
        public static void Build(string propsFolder, params string[] mugNames)
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder(propsFolder.TrimEnd('/'), "Outlines");

            foreach (var name in mugNames)
            {
                var source = propsFolder + name + ".png";
                var target = OutlinePath(name);
                if (!File.Exists(source)) continue;
                if (File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(source)) continue;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(File.ReadAllBytes(source));
                var pixels = texture.GetPixels32();
                for (var i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    var dark = Mathf.Max(p.r, Mathf.Max(p.g, p.b)) < DarkMax;
                    if (!dark) pixels[i] = new Color32(0, 0, 0, 0);
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(target, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(target);
            }
        }

        public const string InMugFolder = "Assets/_Game/Art/Drinks/InMug";

        public static string InMugPath(string drinkName, string suffix = "") => $"{InMugFolder}/{drinkName}{suffix}.png";

        /// <summary>
        /// Clips each drink tile to the mug's silhouette (the art's own alpha), so the square tile never pokes out past
        /// the rounded rim. <paramref name="tileCentre"/> is the tile centre in the mug's local units (mug pivot = centre,
        /// 100 px per unit). Rebuilt every time: it depends on where the tile sits.
        /// </summary>
        public static void ClipDrinks(string mugPath, string suffix, string drinksFolder, Vector2 tileCentre, float tileScaleY, params string[] drinkNames)
        {
            if (!AssetDatabase.IsValidFolder(InMugFolder)) AssetDatabase.CreateFolder(drinksFolder.TrimEnd('/'), "InMug");
            if (!File.Exists(mugPath)) return;

            var mug = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            mug.LoadImage(File.ReadAllBytes(mugPath));
            var mugPixels = mug.GetPixels32();
            int mw = mug.width, mh = mug.height;

            foreach (var name in drinkNames)
            {
                var source = drinksFolder + name + ".png";
                if (!File.Exists(source)) continue;
                var tile = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tile.LoadImage(File.ReadAllBytes(source));
                var pixels = tile.GetPixels32();
                int tw = tile.width, th = tile.height;
                // Tile pixel (x, y) (y up, as in Texture2D) → mug pixel; the tile is drawn squeezed by tileScaleY.
                var offsetX = Mathf.RoundToInt(mw * 0.5f + tileCentre.x * 100f - tw * 0.5f);
                for (var y = 0; y < th; y++)
                for (var x = 0; x < tw; x++)
                {
                    int mx = x + offsetX, my = Mathf.RoundToInt(mh * 0.5f + tileCentre.y * 100f + (y + 0.5f - th * 0.5f) * tileScaleY);
                    var mugAlpha = mx >= 0 && my >= 0 && mx < mw && my < mh ? mugPixels[my * mw + mx].a : (byte)0;
                    var p = pixels[y * tw + x];
                    p.a = (byte)(p.a * mugAlpha / 255);
                    pixels[y * tw + x] = p;
                }

                tile.SetPixels32(pixels);
                tile.Apply();
                var target = InMugPath(name, suffix);
                var bytes = tile.EncodeToPNG();
                if (!File.Exists(target) || !File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes))
                {
                    File.WriteAllBytes(target, bytes);
                    AssetDatabase.ImportAsset(target);
                }

                UnityEngine.Object.DestroyImmediate(tile);
            }

            UnityEngine.Object.DestroyImmediate(mug);
        }
    }
}
