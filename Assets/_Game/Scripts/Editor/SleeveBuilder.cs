using System.Collections.Generic;
using System.IO;
using System.Linq;
using Courtyard.Kitchen;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// Makes every arm drawing's sleeve continue past the screen edge (art manifest: never show where the sleeve ends).
    /// For each arm sprite it finds the sleeve's far end, picks a clean cross-section (fewest fold lines) and saves that
    /// one row of the artist's pixels as a thin strip, which <see cref="SleeveExtender"/> stretches along the arm.
    /// Purely mechanical: no new drawing.
    /// </summary>
    internal static class SleeveBuilder
    {
        private const string LibraryPath = "Assets/_Game/Data/Art/SleeveLibrary.asset";
        private const string OutputFolder = "Assets/_Game/Art/Sprites/Hands/Sleeves";
        private static readonly string[] ArmFolders = { "Assets/_Game/Art/Sprites/Hands", "Assets/_Game/Art/Hands" };
        private static readonly string[] ExtraArms = { "Assets/_Game/Art/Sprites/Kettle/Kettle_HandGrey.png", "Assets/_Game/Art/Sprites/Kettle/Kettle_HandBeige.png" };
        private const int StripRows = 4;

        public static SleeveLibrary BuildLibrary()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder("Assets/_Game/Art/Sprites/Hands", "Sleeves");

            var arms = ArmFolders.Where(AssetDatabase.IsValidFolder)
                .SelectMany(f => AssetDatabase.FindAssets("t:Texture2D", new[] { f }).Select(AssetDatabase.GUIDToAssetPath))
                .Where(p => !p.StartsWith(OutputFolder) && Path.GetFileName(p).StartsWith("Hand"))
                .Concat(ExtraArms.Where(File.Exists))
                .Distinct()
                .ToList();

            var entries = new List<(Sprite sprite, string strip, Vector2 anchor, float angle)>();
            foreach (var path in arms)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                var result = BuildStrip(path, sprite);
                if (result.HasValue) entries.Add((sprite, result.Value.stripPath, result.Value.anchor, result.Value.angle));
            }

            AssetDatabase.Refresh();
            var library = LoadOrCreateAsset<SleeveLibrary>(LibraryPath, _ => { });
            Edit(library, so =>
            {
                var list = Property(so, "entries");
                list.arraySize = entries.Count;
                for (var i = 0; i < entries.Count; i++)
                {
                    var e = list.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("sprite").objectReferenceValue = entries[i].sprite;
                    e.FindPropertyRelative("strip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(entries[i].strip);
                    e.FindPropertyRelative("anchor").vector2Value = entries[i].anchor;
                    e.FindPropertyRelative("angle").floatValue = entries[i].angle;
                }
            });
            AssetDatabase.SaveAssets();
            return library;
        }

        /// <summary>Adds a SleeveExtender to the hand and to the hand-held kettle sprites in the Kitchen scene.</summary>
        public static void WireScene(Scene scene, SleeveLibrary library)
        {
            var arms = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true))
                .Where(r => (r.name == "Hand" && r.GetComponentInParent<HandsView>(true) != null)
                            || ((r.name is "PourGrey" or "PourBeige") && r.GetComponentInParent<KettleView>(true) != null))
                .ToList();

            foreach (var arm in arms)
            {
                var stripTransform = arm.transform.Find("Sleeve");
                if (stripTransform == null)
                {
                    stripTransform = new GameObject("Sleeve").transform;
                    stripTransform.SetParent(arm.transform, false);
                }

                var strip = stripTransform.GetComponent<SpriteRenderer>();
                if (strip == null) strip = stripTransform.gameObject.AddComponent<SpriteRenderer>();
                strip.sortingLayerID = arm.sortingLayerID;
                strip.sortingOrder = arm.sortingOrder + 1;
                strip.color = arm.color;

                var extender = arm.GetComponent<SleeveExtender>();
                if (extender == null) extender = arm.gameObject.AddComponent<SleeveExtender>();
                Wire(extender, "library", library);
                Wire(extender, "strip", strip);
            }
        }

        private static (string stripPath, Vector2 anchor, float angle)? BuildStrip(string path, Sprite sprite)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(path));
            var w = source.width;
            var h = source.height;
            var pixels = source.GetPixels32();

            bool Opaque(Vector2 p, byte threshold = 128)
            {
                var x = Mathf.RoundToInt(p.x);
                var y = Mathf.RoundToInt(p.y);
                return x >= 0 && y >= 0 && x < w && y < h && pixels[y * w + x].a >= threshold;
            }

            // Pivot (grip point / spout tip) in texture pixels, y up.
            var pivot = sprite.pivot * (w / sprite.rect.width);

            // Far end of the sleeve = farthest opaque pixel from the pivot.
            var end = pivot;
            var far = 0f;
            for (var y = 0; y < h; y += 2)
            for (var x = 0; x < w; x += 2)
            {
                if (pixels[y * w + x].a < 128) continue;
                var d = (new Vector2(x, y) - pivot).sqrMagnitude;
                if (d <= far) continue;
                far = d;
                end = new Vector2(x, y);
            }

            var length = Mathf.Sqrt(far);
            if (length < 10f) return null;
            var dir = (end - pivot) / length;
            var perp = new Vector2(-dir.y, dir.x); // strip's local +x after rotation

            // Pick the cleanest cross-section near the sleeve end.
            (float score, Vector2 centre, float s0, float s1)? best = null;
            for (var t = 0.6f; t <= 0.9f; t += 0.02f)
            {
                var c = pivot + dir * (t * length);
                if (!Opaque(c)) continue;
                float s0 = 0f, s1 = 0f;
                while (Opaque(c + perp * (s0 - 1f), 40)) s0 -= 1f;
                while (Opaque(c + perp * (s1 + 1f), 40)) s1 += 1f;
                if (s1 - s0 < 8f) continue;

                var samples = new List<Color32>();
                var inner0 = s0 + (s1 - s0) * 0.15f;
                var inner1 = s1 - (s1 - s0) * 0.15f;
                for (var s = inner0; s <= inner1; s += 1f) samples.Add(Sample(pixels, w, h, c + perp * s));
                var dominant = samples.GroupBy(p => (p.r / 8, p.g / 8, p.b / 8)).OrderByDescending(g => g.Count()).First().First();
                var noisy = samples.Count(p => Mathf.Abs(p.r - dominant.r) + Mathf.Abs(p.g - dominant.g) + Mathf.Abs(p.b - dominant.b) > 30);
                var score = noisy / (float)samples.Count + Mathf.Abs(t - 0.78f) * 0.1f;
                if (best == null || score < best.Value.score) best = (score, c, s0, s1);
            }

            if (best == null) return null;
            var (_, centre, from, to) = best.Value;
            from -= 1f;
            to += 1f;

            // One row of the artist's pixels across the sleeve, repeated a few times.
            var width = Mathf.CeilToInt(to - from) + 1;
            var strip = new Texture2D(width, StripRows, TextureFormat.RGBA32, false);
            var row = new Color32[width];
            for (var x = 0; x < width; x++) row[x] = Sample(pixels, w, h, centre + perp * (from + x));
            for (var y = 0; y < StripRows; y++) strip.SetPixels32(0, y, width, 1, row);
            strip.Apply();

            var stripPath = $"{OutputFolder}/{Path.GetFileNameWithoutExtension(path)}_sleeve.png";
            File.WriteAllBytes(stripPath, strip.EncodeToPNG());
            Object.DestroyImmediate(strip);
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(stripPath);
            ConfigureStrip(stripPath, sprite.pixelsPerUnit * (w / sprite.rect.width));

            // Anchor: middle of the slice, relative to the arm's pivot, in the arm sprite's local units.
            var mid = centre + perp * ((from + to) * 0.5f);
            var ppu = sprite.pixelsPerUnit * (w / sprite.rect.width);
            var anchor = (mid - pivot) / ppu;
            var angle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
            return (stripPath, anchor, angle);
        }

        private static Color32 Sample(Color32[] pixels, int w, int h, Vector2 p)
        {
            var x = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, w - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(p.y), 0, h - 1);
            return pixels[y * w + x];
        }

        private static void ConfigureStrip(string path, float pixelsPerUnit)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 1f); // starts at the slice, runs away from the hand
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
