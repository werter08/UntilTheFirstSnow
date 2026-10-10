using System.Collections.Generic;
using System.Linq;
using Courtyard.Kitchen;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// Applies the art manifest to the Kitchen scene: counter zones and heights from <see cref="KitchenArtLayout"/>,
    /// the manifest's sorting layers, and file slots that switch to the artist's PNG as soon as it exists.
    /// Runs at the end of the decor pass; idempotent.
    /// </summary>
    internal static class ArtManifestBuilder
    {
        public const string LayoutPath = "Assets/_Game/Data/Art/KitchenArtLayout.asset";
        private const string BackgroundsFolder = "Assets/_Game/Art/Backgrounds/";

        /// <summary>
        /// Manifest sorting layers, back → front ("Default" stays first). Wall is moved to the very back: the wall
        /// art has no hole, and the manifest also says the outside layers cover the window area, which only works if
        /// Sky / Outside / Weather draw over the wall, clipped by the window mask.
        /// </summary>
        public static readonly string[] SortingLayerOrder =
        {
            "Default", "Wall", "Sky", "Outside", "Weather", "Glass", "WindowFrame", "WallProps",
            "Counter", "Fridge", "Props", "PropsFront", "Hands", "FX", "UI",
        };

        private static readonly HashSet<string> MaskedLayers = new() { "Sky", "Outside", "Weather" };

        // Earlier layers are renamed (IDs kept, so existing renderers follow).
        private static readonly Dictionary<string, string> LayerRenames = new() { ["Courtyard"] = "Outside", ["Foreground"] = "PropsFront" };

        private static readonly Dictionary<string, string[]> LayerByObject = new()
        {
            ["Outside"] = new[] { "Courtyard", "OppositeWall", "GalleryRailing", "TreeTrunk", "TreeCrown", "MiraWindow", "Silhouette", "Arm", "Note" },
            ["Weather"] = new[] { "Rain", "Leaves", "Snow" },
            ["Glass"] = new[] { "WindowGlass" },
            ["Wall"] = new[] { "Wall" },
            ["WindowFrame"] = new[]
            {
                "WindowFrame", "WindowFrameShadow", "WindowSill", "WindowSillShadow", "CurtainLeft", "CurtainRight",
                "CurtainRod", "CurtainRodShadow", "Plant", "PlantPot", "SillMugHis", "SillMugHers", "WindowFrameFull",
            },
            ["WallProps"] = new[]
            {
                "Shelf", "InstantCoffeeJar", "Clock", "ClockTime", "Calendar", "CalendarHeader", "CalendarDay",
                "WallPicture", "MugRack", "ChippedMug", "HerMug", "CeilingLamp", "RackMug0", "RackMug1", "RackMug2", "RackMug3",
            },
            ["Counter"] = new[] { "Counter", "CounterEdge" },
            ["Fridge"] = new[] { "Fridge", "FridgeNote0", "FridgeNote1", "FridgeNote2", "Magnet", "ShoppingList" },
            ["PropsFront"] = new[] { "Blanket", "MiraAtDoor", "Scarf" },
            ["Hands"] = new[] { "Hand", "PourGrey", "PourBeige" },
            ["FX"] = new[] { "PopupTemplate" },
        };

        // Where the step prefabs and counter tools were authored (CounterLayout), so zones can be shifted from there.
        private static readonly Dictionary<CounterZone, Vector2> AuthoredZone = new()
        {
            [CounterZone.Kettle] = new Vector2(CounterLayout.KettleX, CounterLayout.Top),
            [CounterZone.Grinder] = new Vector2(CounterLayout.GrinderX, CounterLayout.Top),
            [CounterZone.Brew] = new Vector2(CounterLayout.MugX, CounterLayout.Top),
            [CounterZone.Milk] = new Vector2(CounterLayout.MilkX, CounterLayout.Top),
            [CounterZone.Rack] = new Vector2(CounterLayout.ServeX - 0.35f, CounterLayout.Top),
            [CounterZone.Toppings] = new Vector2(CounterLayout.ServeX + 0.025f, CounterLayout.Top),
        };

        public static KitchenArtLayout LoadOrCreateLayout()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Art")) AssetDatabase.CreateFolder("Assets/_Game/Data", "Art");
            return LoadOrCreateAsset<KitchenArtLayout>(LayoutPath, _ => { });
        }

        public static void EnsureSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");

            string NameAt(int i) => layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            int IndexOf(string name)
            {
                for (var i = 0; i < layers.arraySize; i++)
                    if (NameAt(i) == name) return i;
                return -1;
            }

            foreach (var (from, to) in LayerRenames)
            {
                var index = IndexOf(from);
                if (index >= 0 && IndexOf(to) < 0) layers.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue = to;
            }

            foreach (var name in SortingLayerOrder)
            {
                if (IndexOf(name) >= 0) continue;
                layers.InsertArrayElementAtIndex(layers.arraySize);
                var layer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                layer.FindPropertyRelative("name").stringValue = name;
                layer.FindPropertyRelative("uniqueID").uintValue = (uint)name.GetHashCode() | 1u;
            }

            for (var target = 0; target < SortingLayerOrder.Length; target++)
            {
                var current = IndexOf(SortingLayerOrder[target]);
                if (current != target) layers.MoveArrayElement(current, target);
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Apply(Scene scene, Transform kitchen, KitchenArtLayout layout)
        {
            ApplyZones(scene, kitchen, layout);
            ApplyBackgroundSlots(kitchen, layout);
            ApplySortingLayers(scene);
            ApplyLightTargets(scene);
        }

        // ---------------------------------------------------------------- zones

        private static void ApplyZones(Scene scene, Transform kitchen, KitchenArtLayout layout)
        {
            var baseline = KitchenArtLayout.ToWorldY(layout.propsBaselineY);
            var mugBottom = KitchenArtLayout.ToWorldY(layout.mugBottomY);
            var tools = Find(kitchen, "Counter Tools");
            var counter = tools.GetComponent<CounterStation>();
            tools.localPosition = Vector3.zero;
            var stepRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "StepRoot");
            if (stepRoot != null) stepRoot.transform.position = Vector3.zero;

            Vector2 Shift(CounterZone zone, float floor) =>
                new(layout.ZoneX(zone) - AuthoredZone[zone].x, floor - AuthoredZone[zone].y);

            var shifts = new Vector2[System.Enum.GetValues(typeof(CounterZone)).Length];
            foreach (CounterZone zone in System.Enum.GetValues(typeof(CounterZone)))
                if (zone != CounterZone.None) shifts[(int)zone] = Shift(zone, zone == CounterZone.Brew ? mugBottom : baseline);
            Edit(counter, so =>
            {
                var array = Property(so, "zoneShift");
                array.arraySize = shifts.Length;
                for (var i = 0; i < shifts.Length; i++) array.GetArrayElementAtIndex(i).vector2Value = shifts[i];
            });

            // Groups authored at the origin move by their zone shift.
            Find(tools, "Grinder").localPosition = shifts[(int)CounterZone.Grinder];
            Find(tools, "MilkStation").localPosition = shifts[(int)CounterZone.Milk];
            Find(tools, "Toppings").localPosition = shifts[(int)CounterZone.Toppings];

            // Kettle: base on the baseline, switch on the base, kettle on the base.
            var kettleX = layout.ZoneX(CounterZone.Kettle);
            var kettleBase = Find(tools, "KettleBase");
            var baseHalf = kettleBase.GetComponent<SpriteRenderer>().bounds.extents.y;
            var baseY = baseline + baseHalf;
            kettleBase.localPosition = new Vector3(kettleX, baseY, 0f);
            Find(tools, "KettleSwitch").localPosition = new Vector3(kettleX + 0.85f, baseY - 0.02f, 0f);
            Find(tools, "Kettle").localPosition = new Vector3(kettleX, baseY + 0.98f, 0f);

            // Mug (main focus) and the V60 on top of it.
            var mug = Find(tools, "Mug");
            var mugSprite = mug.GetComponent<SpriteRenderer>().sprite;
            var mugHalf = mugSprite.bounds.extents.y;
            mug.localPosition = new Vector3(layout.ZoneX(CounterZone.Brew), mugBottom + mugHalf - mugSprite.bounds.center.y, 0f);
            var v60 = Find(tools, "V60");
            var v60Sprite = v60.GetComponent<SpriteRenderer>().sprite;
            v60.localPosition = v60Sprite != null && v60Sprite.name.StartsWith("v60")
                ? mug.localPosition + (Vector3)(layout.dripperOnMugPx / KitchenArtLayout.PixelsPerUnit)
                : new Vector3(layout.ZoneX(CounterZone.Brew), mugBottom + mugHalf * 2f + 0.35f, 0f);

            // Loose props on the left end of the counter.
            var beans = Find(kitchen, "BeansJar");
            var beansRenderer = beans.GetComponent<SpriteRenderer>();
            beans.localPosition += new Vector3(0f, baseline - beansRenderer.bounds.min.y, 0f);
            var cinnamon = Find(kitchen, "CinnamonJar");
            cinnamon.localPosition += new Vector3(0f, baseline - cinnamon.GetComponent<SpriteRenderer>().bounds.min.y, 0f);

            // Day 30 sill mugs from the layout.
            PlaceBottom(Find(kitchen, "SillMugHis", false), layout.sillMugHis);
            PlaceBottom(Find(kitchen, "SillMugHers", false), layout.sillMugHers);
        }

        private static void PlaceBottom(Transform target, Vector2 figmaCentreBottom)
        {
            if (target == null) return;
            var renderer = target.GetComponent<SpriteRenderer>();
            var world = KitchenArtLayout.ToWorld(figmaCentreBottom);
            var bounds = renderer.bounds;
            target.position += new Vector3(world.x - bounds.center.x, world.y - bounds.min.y, 0f);
        }

        // ---------------------------------------------------------------- full-screen background slots

        /// <summary>Full-screen 1920×1080 PNGs from the manifest replace the current pieces as soon as they exist.</summary>
        private static void ApplyBackgroundSlots(Transform kitchen, KitchenArtLayout layout)
        {
            ConfigureFolder(BackgroundsFolder);

            TryFullScreen(kitchen, "Wall", "bg_wall", "Wall", 0);
            TryFullScreen(kitchen, "Counter", "counter", "Counter", 0);
            TryFullScreen(kitchen, "Fridge", "fridge", "Fridge", 20);

            // Mira's notes in the manifest's fridge-door area, top to bottom.
            var area = layout.fridgeNotesArea;
            for (var i = 0; i < 3; i++)
            {
                var figma = new Vector2(area.center.x, area.y + area.height * (i + 0.5f) / 3f);
                Find(kitchen, $"FridgeNote{i}").localPosition = KitchenArtLayout.ToWorld(figma);
            }

            var frame = Load("window_frame");
            if (frame != null)
            {
                var full = Find(kitchen, "WindowFrameFull", false) ?? Empty(kitchen, "WindowFrameFull", Vector2.zero);
                if (full.GetComponent<SpriteRenderer>() == null) full.gameObject.AddComponent<SpriteRenderer>();
                FullScreen(full, frame, "WindowFrame", 0);
                foreach (var piece in new[] { "WindowFrame", "WindowFrameShadow", "WindowSill", "WindowSillShadow", "CurtainLeft", "CurtainRight", "CurtainRod", "CurtainRodShadow" })
                {
                    var t = Find(kitchen, piece, false);
                    if (t != null) t.gameObject.SetActive(false);
                }

                // Window opening from the manifest drives the courtyard and the weather mask.
                var opening = layout.windowOpening;
                var centre = KitchenArtLayout.ToWorld(opening.center);
                var size = opening.size / KitchenArtLayout.PixelsPerUnit;
                var courtyard = Find(kitchen, "Courtyard").GetComponent<SpriteRenderer>();
                courtyard.transform.localPosition = centre;
                courtyard.size = size;
                var mask = Find(kitchen, "WindowMask");
                mask.localPosition = centre;
                mask.localScale = new Vector3(size.x, size.y, 1f);
            }

            var glass = Load("window_glass");
            if (glass != null)
            {
                var pane = Find(kitchen, "WindowGlass", false) ?? Empty(kitchen, "WindowGlass", Vector2.zero);
                if (pane.GetComponent<SpriteRenderer>() == null) pane.gameObject.AddComponent<SpriteRenderer>();
                FullScreen(pane, glass, "Glass", 0);
            }
        }

        private static bool TryFullScreen(Transform kitchen, string objectName, string file, string layer, int order)
        {
            var sprite = Load(file);
            if (sprite == null) return false;
            FullScreen(Find(kitchen, objectName), sprite, layer, order);
            return true;
        }

        private static void FullScreen(Transform target, Sprite sprite, string layer, int order)
        {
            var renderer = target.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;
            target.localScale = Vector3.one;
            target.localRotation = Quaternion.identity;
            target.localPosition = -(Vector2)sprite.bounds.center; // canvas centre on the camera centre
        }

        private static Sprite Load(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundsFolder + file + ".png");

        private static void ConfigureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder.TrimEnd('/'))) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder.TrimEnd('/') }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (importer.textureType == TextureImporterType.Sprite && importer.maxTextureSize == 2048) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = KitchenArtLayout.PixelsPerUnit;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        // ---------------------------------------------------------------- layers and lights

        private static void ApplySortingLayers(Scene scene)
        {
            var byName = new Dictionary<string, string>();
            foreach (var (layer, names) in LayerByObject)
                foreach (var name in names) byName[name] = layer;

            foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!byName.TryGetValue(renderer.name, out var layer)) continue;
                renderer.sortingLayerName = layer;
                if (MaskedLayers.Contains(layer) && renderer is SpriteRenderer sprite)
                    sprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                if (renderer.TryGetComponent<TMP_Text>(out _)) EditorUtility.SetDirty(renderer);
            }

        }

        /// <summary>2D lights must target every sorting layer, or sprites on new layers render black.</summary>
        private static void ApplyLightTargets(Scene scene)
        {
            var all = SortingLayer.layers.Select(l => l.id).ToArray();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var light in root.GetComponentsInChildren<Light2D>(true))
            {
                light.targetSortingLayers = all;
                EditorUtility.SetDirty(light);
            }
        }
    }
}
