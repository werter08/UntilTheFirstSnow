using System.Linq;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// The courtyard seen through the window, from the team's art in Art/Outside (1 px = 1 px on the 1920×1080
    /// reference, never scaled): sky colour, the opposite building, Mira's window (+ her warm light) and the chestnut
    /// tree slots. Back → front: Sky → wall (Outside 0) → Mira's window (1) → trunk (2) → crown (3) → weather → glass →
    /// frame. Everything outside is clipped by the window mask. Runs after the art manifest. Idempotent.
    /// </summary>
    internal static class OutsideBuilder
    {
        private const string Folder = "Assets/_Game/Art/Outside/";

        /// <summary>Per day: sky colour, Mira's window, chestnut crown art.</summary>
        private static readonly (int day, string sky, MiraWindowState window, string crown)[] Days =
        {
            (1, "#9aa6b0", MiraWindowState.Silhouette, "tree_1_green"), // grey morning; she's awake with a mug
            (5, "#5c6878", MiraWindowState.Lit, "tree_2_orange"),       // rainy evening; she waves on the WaveBack beat
            (10, "#a8c8e0", MiraWindowState.Lit, "tree_3_golden"),      // she's at his door, her light is on
            (20, "#1e2438", MiraWindowState.Note, "tree_4_half"),       // night: "Heating broke"; Lit after "Hold on."
            (30, "#c8ccd4", MiraWindowState.Dark, "tree_5_snow"),       // snowy evening; she's in his kitchen
        };

        /// <summary>
        /// Outside plan coordinates → world. The outside art is planned for a window opening of x 660–1260, y 110–560;
        /// the kitchen's real opening (the window mask, from the art manifest) sits a little lower, so the whole
        /// courtyard moves with it (no scaling): what you see through the window matches the plan.
        /// </summary>
        private static Vector2 Px(float x, float y) => new Vector2((x - 960f) / 100f, (540f - y) / 100f) + _planOffset;

        private static readonly Vector2 PlannedOpeningCentre = new(0f, (540f - 335f) / 100f); // (960, 335)
        private static Vector2 _planOffset;

        private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + name + ".png");

        public static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && importer.spritePixelsPerUnit == 100f && !importer.mipmapEnabled) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f; // 1 px = 1 px on the reference screen
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        // ---------------------------------------------------------------- scene

        public static void ApplyToKitchen(Transform kitchen)
        {
            var wall = Load("outside_wall");
            if (wall == null)
            {
                Debug.LogWarning($"OutsideBuilder: {Folder}outside_wall.png missing — the placeholder courtyard stays.");
                return;
            }

            var mask = Find(kitchen, "WindowMask");
            _planOffset = (Vector2)mask.position - PlannedOpeningCentre;
            Debug.Log($"OutsideBuilder: courtyard offset to the real window opening = {_planOffset * 100f} px (x right, y up).");

            // Sky: a solid colour behind everything outside (the old courtyard quad), coloured per level.
            var sky = Find(kitchen, "Courtyard").GetComponent<SpriteRenderer>();
            Layer(sky, "Sky", 0);

            // Opposite building. The export is cropped to the building: centred on x 960 with the roof edge at y 70.
            var opposite = Find(kitchen, "OutsideWall", false) ?? Empty(kitchen, "OutsideWall", Vector2.zero);
            var keepWall = IsHandPlaced(opposite);
            var wallScale = opposite.localScale;
            Sprite(opposite, wall, "Outside", 0);
            if (keepWall) opposite.localScale = wallScale; // hand-placed: keep the team's position and scale
            else
            {
                var top = Px(960f, 70f);
                opposite.position = new Vector3(top.x, top.y - wall.bounds.extents.y, 0f);
            }
            foreach (var placeholder in new[] { "OppositeWall", "GalleryRailing", "NeighbourWindows" })
            {
                var t = Find(kitchen, placeholder, false);
                if (t != null) t.gameObject.SetActive(false); // the drawn wall has its own (always dark) windows
            }

            ApplyMiraWindow(kitchen);
            ApplyTree(kitchen);
            ScopeLights(kitchen);
            ApplyWeatherSprites(kitchen);
            WireArray(kitchen.GetComponent<KitchenDirector>(), "outsideShaded", new Object[]
            {
                opposite.GetComponent<SpriteRenderer>(), Find(kitchen, "TreeTrunk").GetComponent<SpriteRenderer>(),
                Find(kitchen, "TreeCrown").GetComponent<SpriteRenderer>(),
            });
            Wire(kitchen.GetComponent<KitchenDirector>(), "courtyard", sky);
        }

        private static void ApplyMiraWindow(Transform kitchen)
        {
            var sprites = new[] { "lit", "dark", "silhouette", "wave", "note" }.Select(s => Load("mira_window_" + s)).ToArray();
            if (sprites.Any(s => s == null))
            {
                Debug.LogWarning($"OutsideBuilder: some {Folder}mira_window_*.png are missing — the placeholder window stays.");
                return;
            }

            var window = Find(kitchen, "MiraWindow");
            var keepWindow = IsHandPlaced(window);
            var windowScale = window.localScale;
            var renderer = Sprite(window, sprites[0], "Outside", 1);
            if (keepWindow) window.localScale = windowScale;
            else window.position = (Vector3)Px(960f, 225f);
            foreach (Transform child in window) child.gameObject.SetActive(false); // placeholder silhouette, arm, note
            var collider = window.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                collider.size = sprites[0].bounds.size;
                collider.offset = sprites[0].bounds.center;
            }

            // Her warm light: small and soft, the warm spot in a cold scene (especially Day 20's night).
            var light = Find(kitchen, "MiraWindowLight").GetComponent<Light2D>();
            light.transform.position = window.position;
            light.color = new Color(0xf2 / 255f, 0xd2 / 255f, 0x7a / 255f);
            light.pointLightOuterRadius = 1.2f;
            light.pointLightInnerRadius = 0.15f;
            light.intensity = 0.7f;
            light.falloffIntensity = 0.6f;

            var art = window.GetComponent<MiraWindow>();
            if (art == null) art = window.gameObject.AddComponent<MiraWindow>();
            Edit(art, so =>
            {
                so.FindProperty("window").objectReferenceValue = renderer;
                so.FindProperty("lit").objectReferenceValue = sprites[0];
                so.FindProperty("dark").objectReferenceValue = sprites[1];
                so.FindProperty("silhouette").objectReferenceValue = sprites[2];
                so.FindProperty("wave").objectReferenceValue = sprites[3];
                so.FindProperty("note").objectReferenceValue = sprites[4];
                so.FindProperty("glow").objectReferenceValue = light;
                so.FindProperty("waveFrameSeconds").floatValue = 0.4f;
            });
            Wire(kitchen.GetComponent<KitchenDirector>(), "miraWindowArt", art);
            ApplyMiraArm(window, art);
        }

        /// <summary>
        /// Her waving arm (mira_arm.png): pivot at the shoulder, placed where the arm is in the waving drawing
        /// (window pixels x 92–123, y 46–106; shoulder just under it, behind her silhouette's shoulder).
        /// </summary>
        private static void ApplyMiraArm(Transform window, MiraWindow art)
        {
            const string armPath = Folder + "mira_arm.png";
            if (!System.IO.File.Exists(armPath))
            {
                Debug.LogWarning($"OutsideBuilder: {armPath} missing — Mira waves by swapping drawings.");
                return;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(armPath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            var pivot = new Vector2(0.5f, 0.03f);
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom || settings.spritePivot != pivot)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            var armSprite = AssetDatabase.LoadAssetAtPath<Sprite>(armPath);
            var windowSprite = window.GetComponent<SpriteRenderer>().sprite;
            var armT = window.Find("Arm");
            if (armT == null)
            {
                armT = new GameObject("Arm").transform;
                armT.SetParent(window, false);
            }

            var renderer = armT.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = armT.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = armSprite;
            renderer.color = Color.white;
            Layer(renderer, "Outside", 2);
            // Window pixel (107, 110) — the shoulder — relative to the window sprite's centre, in units (1 px = 0.01).
            var w = windowSprite.rect.width;
            var h = windowSprite.rect.height;
            armT.localPosition = new Vector3((107f - w * 0.5f) / 100f, (h * 0.5f - 110f) / 100f, 0f);
            armT.localScale = Vector3.one * 0.9f;
            armT.localRotation = Quaternion.identity;
            armT.gameObject.SetActive(false);
            Wire(art, "arm", armT);
        }

        /// <summary>
        /// Weather particles drawn by the team (Art/FX/weather_leaf, weather_snow, weather_rain). Each waits, with a
        /// warning, until its PNG is there; the particle keeps its current look until then.
        /// </summary>
        private static void ApplyWeatherSprites(Transform kitchen)
        {
            foreach (var (system, file) in new[] { ("Leaves", "weather_leaf"), ("Snow", "weather_snow"), ("Rain", "weather_rain") })
            {
                var path = $"Assets/_Game/Art/FX/{file}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var particles = Find(kitchen, system, false)?.GetComponent<ParticleSystem>();
                if (particles == null) continue;
                if (sprite == null)
                {
                    Debug.LogWarning($"OutsideBuilder: {path} missing — {system} keeps its current particle look.");
                    continue;
                }

                var sheet = particles.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Sprites;
                while (sheet.spriteCount > 0) sheet.RemoveSprite(0);
                sheet.AddSprite(sprite);
                var main = particles.main;
                main.startColor = Color.white; // the drawing carries its own colour
                // Drawn size (1 px = 1/100 unit) scaled for the courtyard's depth, keeping the drawing's proportions.
                var depth = system == "Leaves" ? 0.55f : system == "Snow" ? 0.5f : 0.7f;
                var w = sprite.rect.width / 100f * depth;
                var h = sprite.rect.height / 100f * depth;
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(w * 0.75f, w);
                main.startSizeY = new ParticleSystem.MinMaxCurve(h * 0.75f, h);
                main.startSizeZ = 1f;
                ApplyWeatherMotion(particles, system);
                EditorUtility.SetDirty(particles);
            }
        }

        /// <summary>
        /// How each weather falls: rain as fast stretched streaks, leaves slowly with spin and a side sway, snow slow and
        /// soft with a slight drift.
        /// </summary>
        private static void ApplyWeatherMotion(ParticleSystem particles, string system)
        {
            var main = particles.main;
            var velocity = particles.velocityOverLifetime;
            var noise = particles.noise;
            var spin = particles.rotationOverLifetime;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share the curve mode (two constants)
            switch (system)
            {
                case "Rain":
                    velocity.x = new ParticleSystem.MinMaxCurve(-0.6f, -0.3f);
                    velocity.y = new ParticleSystem.MinMaxCurve(-11f, -9f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.8f);
                    main.startRotation = 0f;
                    renderer.renderMode = ParticleSystemRenderMode.Billboard; // the drawn drop is already a streak
                    noise.enabled = false;
                    spin.enabled = false;
                    break;
                case "Leaves":
                    velocity.x = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                    velocity.y = new ParticleSystem.MinMaxCurve(-0.9f, -0.6f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 11f);
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    spin.enabled = true;
                    spin.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f); // radians/s, both ways
                    noise.enabled = true;                                 // side sway as they fall
                    noise.separateAxes = true;
                    noise.strengthX = 0.6f;
                    noise.strengthY = 0.1f;
                    noise.frequency = 0.35f;
                    noise.scrollSpeed = 0.2f;
                    break;
                case "Snow":
                    velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.15f);
                    velocity.y = new ParticleSystem.MinMaxCurve(-0.6f, -0.4f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f);
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    spin.enabled = true;
                    spin.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                    noise.enabled = true;                                 // slight drift
                    noise.separateAxes = false;
                    noise.strength = 0.18f;
                    noise.frequency = 0.25f;
                    noise.scrollSpeed = 0.1f;
                    break;
            }
        }

        private static readonly string[] OutsideLayers = { "Sky", "Outside", "Weather" };

        /// <summary>
        /// The kitchen lamp lights only the kitchen and Mira's light only the courtyard, so the courtyard follows the
        /// day's global light (a dark blue night on Day 20) and her window is the warm spot in it. The global light still
        /// targets every layer (unlit layers would render black).
        /// </summary>
        private static void ScopeLights(Transform kitchen)
        {
            var all = SortingLayer.layers.Select(l => l.id).ToArray();
            var outside = OutsideLayers.Select(SortingLayer.NameToID).ToArray();
            var lamp = Find(kitchen, "LampLight", false)?.GetComponent<Light2D>();
            if (lamp != null) lamp.targetSortingLayers = all.Where(id => !outside.Contains(id)).ToArray();
            var mira = Find(kitchen, "MiraWindowLight", false)?.GetComponent<Light2D>();
            if (mira != null) mira.targetSortingLayers = outside;
        }

        private static readonly string[] TreeArt = { "tree_1_green", "tree_2_orange", "tree_3_golden", "tree_4_half", "tree_5_snow" };

        /// <summary>
        /// The chestnut tree. The drawings are whole trees (trunk included, one per day), so each stands on its own trunk
        /// base at (720, 635) and the separate trunk slot is hidden; without the drawings the placeholders stay.
        /// </summary>
        private static void ApplyTree(Transform kitchen)
        {
            var trunk = Find(kitchen, "TreeTrunk");
            var trunkRenderer = trunk.GetComponent<SpriteRenderer>();
            var crown = Find(kitchen, "TreeCrown");
            var crownRenderer = crown.GetComponent<SpriteRenderer>();
            var trees = TreeArt.Where(n => System.IO.File.Exists(Folder + n + ".png")).ToArray();
            var basePoint = Px(720f, 635f);

            if (trees.Length > 0)
            {
                foreach (var name in trees) SetTrunkBasePivot(Folder + name + ".png");
                trunk.gameObject.SetActive(false); // each drawing has its own trunk
                var keepCrown = IsHandPlaced(crown);
                var crownScale = crown.localScale;
                Sprite(crown, Load(trees[0]), "Outside", 3); // preview; KitchenDirector picks the day's tree
                if (keepCrown) crown.localScale = crownScale;
                else crown.position = new Vector3(basePoint.x, basePoint.y, 0f);
                return;
            }

            trunk.gameObject.SetActive(true);
            Layer(trunkRenderer, "Outside", 2);
            trunk.position = new Vector3(basePoint.x, basePoint.y - trunkRenderer.bounds.min.y + trunk.position.y, 0f);
            Layer(crownRenderer, "Outside", 3);
            crown.position = (Vector3)Px(700f, 400f);
        }

        /// <summary>Pivot at the bottom centre of the trunk (measured on the drawing's lowest rows).</summary>
        private static void SetTrunkBasePivot(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(System.IO.File.ReadAllBytes(path));
            var pixels = texture.GetPixels32();
            int w = texture.width, h = texture.height;
            Object.DestroyImmediate(texture);

            int bottom = -1, min = w, max = -1;
            for (var y = 0; y < h && bottom < 0; y++) // texture rows go bottom-up
                for (var x = 0; x < w; x++)
                    if (pixels[y * w + x].a > 128) { bottom = y; break; }
            for (var y = bottom; y < Mathf.Min(h, bottom + 6); y++)
                for (var x = 0; x < w; x++)
                    if (pixels[y * w + x].a > 128) { min = Mathf.Min(min, x); max = Mathf.Max(max, x); }
            if (bottom < 0 || max < min) return;

            var pivot = new Vector2((min + max + 1) * 0.5f / w, bottom / (float)h);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment == (int)SpriteAlignment.Custom && (settings.spritePivot - pivot).sqrMagnitude < 1e-6f) return;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static SpriteRenderer Sprite(Transform target, Sprite sprite, string layer, int order)
        {
            var renderer = target.GetComponent<SpriteRenderer>(); // (no ?? : Unity's missing components are fake-null)
            if (renderer == null) renderer = target.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            target.localScale = Vector3.one;
            target.localRotation = Quaternion.identity;
            Layer(renderer, layer, order);
            return renderer;
        }

        private static void Layer(SpriteRenderer renderer, string layer, int order)
        {
            renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask; // only seen through the window
        }

        // ---------------------------------------------------------------- levels

        public static void PatchLevels()
        {
            foreach (var (day, sky, window, crown) in Days)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"Assets/_Game/Data/Levels/Day_{day:00}.asset");
                if (level == null) continue;
                ColorUtility.TryParseHtmlString(sky, out var skyColour);
                var crownSprite = Load(crown);
                Edit(level, so =>
                {
                    so.FindProperty("skyColor").colorValue = skyColour;
                    so.FindProperty("miraWindow").enumValueIndex = (int)window;
                    so.FindProperty("treeCrownSprite").objectReferenceValue = crownSprite; // null until drawn

                    // Day 20: after his reply "Hold on." her window goes from the note to lit.
                    var beats = so.FindProperty("storyBeats");
                    for (var i = 0; i < beats.arraySize; i++)
                    {
                        var beat = beats.GetArrayElementAtIndex(i);
                        var isReply = day == 20 && beat.FindPropertyRelative("text").stringValue.Trim() == "Hold on.";
                        beat.FindPropertyRelative("setsMiraWindow").boolValue = isReply;
                        beat.FindPropertyRelative("miraWindowAfter").enumValueIndex = isReply ? (int)MiraWindowState.Lit : 0;
                    }
                });
            }

            AssetDatabase.SaveAssets();
        }
    }
}
