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
        private const string WindowFolder = "Assets/_Game/Art/Sprites/Window/";

        // Window layout (world units). The frame art sets the size; everything else hangs off it.
        private static readonly Vector2 WindowCenter = new(0f, 1.75f);
        private const float FrameHeight = 4.8f;
        private static readonly Vector2 GlassSize = new(5.8f, 4.4f);
        private static readonly Vector2 ShadowOffset = new(0.07f, -0.09f);
        private static readonly Color WallColour = new(0.86f, 0.83f, 0.8f);

        [MenuItem("Courtyard/Setup/Apply Pack Decor")]
        public static void Apply()
        {
            ConfigureImports(OddblotFolder);
            ConfigureImports(WindowFolder);
            EditScene(BuildScript.Scenes[1], ApplyToKitchen);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyToKitchen(Scene scene)
        {
            var kitchen = scene.GetRootGameObjects().First(g => g.name == "Kitchen").transform;
            ApplyWindow(kitchen);

            // Ceiling: hanging bulb; the lamp light sits on the bulb.
            if (Fit(kitchen, "CeilingLamp", "Oddblot_light", new Vector2(0f, 5.05f), 1.6f, "Wall", 13))
                Find(kitchen, "LampLight").localPosition = new Vector3(0f, 4.8f, 0f);

            // Left wall: shelf with dishes (replaces the plank and the instant coffee jar placeholder).
            if (Fit(kitchen, "Shelf", "Oddblot_shelf", new Vector2(-7.3f, 1.6f), 1.9f, "Wall", 1))
                Find(kitchen, "InstantCoffeeJar").gameObject.SetActive(false);

            // Bag of real beans from Day 5 ("Bought real beans today") stands at the left end of the counter.
            Fit(kitchen, "BeansJar", "Oddblot_deco", new Vector2(-8.85f, -2.2f), 1.15f, "Props", 1);
            Find(kitchen, "CinnamonJar").localPosition = new Vector3(-8.1f, -2.4f, 0f);
            SetLayer(Find(kitchen, "CinnamonJar"), "Props", 1);
            Find(kitchen, "Scarf").localPosition = new Vector3(8.25f, -1.3f, 0f);

            // Window sill: flower in a glass (tinted per PlantStage by the KitchenDirector).
            if (Fit(kitchen, "Plant", "Oddblot_tbldeco2", new Vector2(1.4f, -0.12f), 1f, "Wall", 12))
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
        /// Hand-drawn window: frame with transparent glass over the courtyard, curtain rod, two curtains (one drawing,
        /// mirrored), window sill, and soft drop shadows. Mira's window and the tree move so the curtains never hide her.
        /// </summary>
        private static void ApplyWindow(Transform kitchen)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(WindowFolder + "Window_Frame.png") == null) return;

            Find(kitchen, "Wall").GetComponent<SpriteRenderer>().color = WallColour;

            // Glass area: courtyard, weather mask and everything seen outside.
            var glass = Find(kitchen, "Courtyard").GetComponent<SpriteRenderer>();
            glass.transform.localPosition = WindowCenter;
            glass.size = GlassSize;
            Find(kitchen, "WindowMask").localScale = new Vector3(GlassSize.x, GlassSize.y, 1f);
            Find(kitchen, "WindowMask").localPosition = WindowCenter;
            foreach (var name in new[] { "OppositeWall", "GalleryRailing" })
            {
                var piece = Find(kitchen, name).GetComponent<SpriteRenderer>();
                piece.size = new Vector2(GlassSize.x, piece.size.y);
            }

            Find(kitchen, "MiraWindow").localPosition = new Vector3(0.85f, 2.75f, 0f);
            Find(kitchen, "MiraWindowLight").localPosition = new Vector3(0.85f, 2.75f, 0f);
            Find(kitchen, "TreeTrunk").localPosition = new Vector3(-1.5f, 0.5f, 0f);
            Find(kitchen, "TreeCrown").localPosition = new Vector3(-1.5f, 2.3f, 0f);

            // Frame in front of the courtyard, its shadow just behind it.
            Fit(kitchen, "WindowFrame", "Window_Frame", WindowCenter, FrameHeight, "Wall", 9, WindowFolder);
            var frame = Find(kitchen, "WindowFrame");
            ShadowOf(kitchen, frame, "WindowFrameShadow", "Window_FrameShadow", 8);

            var top = WindowCenter.y + FrameHeight * 0.5f;
            var bottom = WindowCenter.y - FrameHeight * 0.5f;

            // Sill under the frame.
            Fit(kitchen, "WindowSill", "Window_Sill", new Vector2(0f, bottom - 0.14f), 0.5f, "Wall", 11, WindowFolder);
            ShadowOf(kitchen, Find(kitchen, "WindowSill"), "WindowSillShadow", "Window_SillShadow", 7);

            // Curtains hang from the rod down to the sill; the drawing is the right curtain, the left one is mirrored.
            var curtainHeight = top + 0.2f - (bottom + 0.05f);
            var halfWidth = FrameHeight * 1.3f * 0.5f;
            EnsureSprite(kitchen, "CurtainRight");
            EnsureSprite(kitchen, "CurtainLeft");
            Fit(kitchen, "CurtainRight", "Window_Curtain", new Vector2(halfWidth - 0.7f, (top + 0.2f + bottom + 0.05f) * 0.5f), curtainHeight, "Wall", 10, WindowFolder);
            Fit(kitchen, "CurtainLeft", "Window_Curtain", new Vector2(-(halfWidth - 0.7f), (top + 0.2f + bottom + 0.05f) * 0.5f), curtainHeight, "Wall", 10, WindowFolder);
            Find(kitchen, "CurtainLeft").GetComponent<SpriteRenderer>().flipX = true;

            // Rod across the top, over the curtain tops.
            EnsureSprite(kitchen, "CurtainRod");
            Fit(kitchen, "CurtainRod", "Window_Rod", new Vector2(0f, top + 0.22f), 0.25f, "Wall", 12, WindowFolder);
            ShadowOf(kitchen, Find(kitchen, "CurtainRod"), "CurtainRodShadow", "Window_RodShadow", 7);
        }

        /// <summary>Soft drop shadow: same size as <paramref name="caster"/>, nudged down-right, drawn behind it.</summary>
        private static void ShadowOf(Transform root, Transform caster, string objectName, string spriteName, int order)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WindowFolder + spriteName + ".png");
            if (sprite == null) return;

            EnsureSprite(root, objectName);
            var shadow = Find(root, objectName);
            var renderer = shadow.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            SetLayer(shadow, "Wall", order);

            // Shadow PNGs are exported at the caster's pixel size, so matching scale and centre lines them up.
            var casterSprite = caster.GetComponent<SpriteRenderer>().sprite;
            var scale = caster.localScale.x * casterSprite.rect.width / sprite.rect.width * (sprite.pixelsPerUnit / casterSprite.pixelsPerUnit);
            shadow.localScale = new Vector3(scale, scale, 1f);
            var casterCentre = (Vector2)caster.localPosition + (Vector2)(casterSprite.bounds.center * caster.localScale.x);
            shadow.localPosition = casterCentre + ShadowOffset - (Vector2)(sprite.bounds.center * scale);
        }

        private static void EnsureSprite(Transform root, string objectName)
        {
            if (Find(root, objectName, false) != null) return;
            Empty(root, objectName, Vector2.zero).gameObject.AddComponent<SpriteRenderer>();
        }

        /// <summary>
        /// Puts <paramref name="spriteName"/> on the named placeholder so its visible (non-transparent) area is
        /// <paramref name="height"/> units tall and centred on <paramref name="center"/>. Returns false if the sprite is missing.
        /// </summary>
        private static bool Fit(Transform root, string objectName, string spriteName, Vector2 center, float height, string layer, int order,
            string folder = OddblotFolder)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + spriteName + ".png");
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
