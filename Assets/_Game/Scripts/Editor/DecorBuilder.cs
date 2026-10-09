using System.Linq;
using Courtyard.Kitchen;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// Swaps kitchen placeholders for decor from asset packs (see CREDITS.md). Idempotent: it finds objects by name
    /// and re-applies sprites and placement, so it also runs after the content builder regenerates the Kitchen scene.
    /// A missing sprite file leaves that placeholder as it is.
    /// </summary>
    public static class DecorBuilder
    {
        private const string OddblotFolder = "Assets/_Game/Art/Sprites/Decor/ODDBLOT/";

        [MenuItem("Courtyard/Setup/Apply Pack Decor")]
        public static void Apply()
        {
            ConfigureImports(OddblotFolder);
            EditScene(BuildScript.Scenes[1], ApplyToKitchen);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyToKitchen(Scene scene)
        {
            var kitchen = scene.GetRootGameObjects().First(g => g.name == "Kitchen").transform;

            // Ceiling: hanging bulb; the lamp light sits on the bulb.
            if (Fit(kitchen, "CeilingLamp", "Oddblot_light", new Vector2(0f, 4.25f), 2.3f, "Wall", 1))
                Find(kitchen, "LampLight").localPosition = new Vector3(0f, 3.75f, 0f);

            // Left wall: shelf with dishes (replaces the plank and the instant coffee jar placeholder).
            if (Fit(kitchen, "Shelf", "Oddblot_shelf", new Vector2(-7.3f, 1.6f), 1.9f, "Wall", 1))
                Find(kitchen, "InstantCoffeeJar").gameObject.SetActive(false);

            // Bag of real beans from Day 5 ("Bought real beans today") stands at the left end of the counter.
            Fit(kitchen, "BeansJar", "Oddblot_deco", new Vector2(-8.85f, -2.2f), 1.15f, "Props", 1);
            Find(kitchen, "CinnamonJar").localPosition = new Vector3(-8.1f, -2.4f, 0f);
            SetLayer(Find(kitchen, "CinnamonJar"), "Props", 1);
            Find(kitchen, "Scarf").localPosition = new Vector3(8.25f, -1.3f, 0f);

            // Window sill: flower in a glass (tinted per PlantStage by the KitchenDirector).
            if (Fit(kitchen, "Plant", "Oddblot_tbldeco2", new Vector2(2.9f, -0.25f), 1f, "Wall", 7))
            {
                Find(kitchen, "PlantPot").gameObject.SetActive(false);
                Edit(kitchen.GetComponent<KitchenDirector>(), so =>
                {
                    var tints = new[]
                    {
                        new Color(0.72f, 0.62f, 0.5f), new Color(0.88f, 0.84f, 0.74f), Color.white, new Color(1f, 0.93f, 0.95f),
                    };
                    var array = Property(so, "plantStageColors");
                    array.arraySize = tints.Length;
                    for (var i = 0; i < tints.Length; i++) array.GetArrayElementAtIndex(i).colorValue = tints[i];
                });
            }

            // Right wall: a small framed picture above the mug rack.
            var picture = Find(kitchen, "WallPicture", false);
            if (picture == null)
            {
                picture = Empty(kitchen, "WallPicture", Vector2.zero);
                picture.gameObject.AddComponent<SpriteRenderer>();
            }

            Fit(kitchen, "WallPicture", "Oddblot_walldeco", new Vector2(5.9f, 3.2f), 1.8f, "Wall", 1);
        }

        /// <summary>
        /// Puts <paramref name="spriteName"/> on the named placeholder so its visible (non-transparent) area is
        /// <paramref name="height"/> units tall and centred on <paramref name="center"/>. Returns false if the sprite is missing.
        /// </summary>
        private static bool Fit(Transform root, string objectName, string spriteName, Vector2 center, float height, string layer, int order)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OddblotFolder + spriteName + ".png");
            if (sprite == null) return false;

            var target = Find(root, objectName);
            var renderer = target.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            SetLayer(target, layer, order);

            var collider = target.GetComponent<BoxCollider2D>();
            if (collider != null) Object.DestroyImmediate(collider);

            var vertices = sprite.vertices;
            var min = new Vector2(vertices.Min(v => v.x), vertices.Min(v => v.y));
            var max = new Vector2(vertices.Max(v => v.x), vertices.Max(v => v.y));
            var scale = height / (max.y - min.y);
            target.localScale = new Vector3(scale, scale, 1f);
            target.localPosition = center - (min + max) * 0.5f * scale;
            return true;
        }

        private static void SetLayer(Transform target, string layer, int order)
        {
            var renderer = target.GetComponent<SpriteRenderer>();
            renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;
        }

        private static Transform Find(Transform root, string objectName, bool required = true)
        {
            var found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == objectName);
            if (found == null && required) throw new System.InvalidOperationException($"Kitchen has no '{objectName}' object.");
            return found;
        }

        private static void ConfigureImports(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                var changed = importer.textureType != TextureImporterType.Sprite || settings.spriteMeshType != SpriteMeshType.Tight
                              || importer.maxTextureSize != 1024 || importer.mipmapEnabled;
                if (!changed) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 1024;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.Tight;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
    }
}
