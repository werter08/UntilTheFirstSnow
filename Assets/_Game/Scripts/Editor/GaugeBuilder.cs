using System.Linq;
using Courtyard.Core;
using Courtyard.Minigames;
using Courtyard.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Courtyard.Editor.BuilderKit;
using P = Courtyard.Editor.MinigamePrefabBuilder;

namespace Courtyard.Editor
{
    /// <summary>
    /// The one reusable "stop in the green" gauge (the team's drawn frame, fill, zones, marker and star, 1:1 from their
    /// sheet): imports the sprites with 9-slice borders, builds Prefabs/UI/MinigameGauge, places one in the Kitchen HUD,
    /// gives each step its icon and removes the old world-space meters from the step prefabs. Idempotent.
    /// </summary>
    internal static class GaugeBuilder
    {
        private const string ArtFolder = "Assets/_Game/Art/UI/";
        private const string PrefabPath = "Assets/_Game/Prefabs/UI/MinigameGauge.prefab";

        // Reference size (1920×1080) and the drawings' native sizes: the 9-slice multiplier scales borders to match.
        private static readonly Vector2 Size = new(360f, 56f);
        private const float InnerInsetX = 13f;
        private const float InnerInsetY = 8f;

        /// <summary>Sprite → 9-slice border in source pixels (left, bottom, right, top); zero = simple sprite.</summary>
        private static readonly (string name, Vector4 border)[] Sprites =
        {
            ("gauge_frame", new Vector4(64f, 60f, 64f, 60f)), // 28 px L/R at the 56 px height
            ("gauge_fill", new Vector4(42f, 40f, 42f, 40f)),
            ("gauge_zone_green", new Vector4(14f, 14f, 14f, 14f)),
            ("gauge_zone_red", new Vector4(14f, 14f, 14f, 14f)),
            ("gauge_marker", Vector4.zero),
            ("gauge_star", Vector4.zero),
            ("icon_grind", Vector4.zero),
            ("icon_flow", Vector4.zero),
            ("icon_milk", Vector4.zero),
            ("icon_temp", Vector4.zero),
        };

        [MenuItem("Courtyard/Setup/Build Minigame Gauge")]
        public static void Apply()
        {
            ConfigureImports();
            if (Load("gauge_frame") == null || Load("gauge_fill") == null)
            {
                Debug.LogWarning("GaugeBuilder: gauge art missing in Art/UI — nothing built.");
                return;
            }

            var prefab = BuildPrefab();
            PatchSteps();
            EditScene(BuildScript.Scenes[1], scene =>
            {
                var canvas = scene.GetRootGameObjects().First(g => g.name == "Canvas").transform;
                var parent = canvas.Find("HUD") ?? canvas; // hidden with the HUD while a note is read
                var runner = scene.GetRootGameObjects().Select(g => g.GetComponent<LevelRunner>()).First(r => r != null);
                // Two: most steps use one; the pour-over watches the water and the flow at once.
                foreach (var (name, field) in new[] { ("MinigameGauge", "gauge"), ("MinigameGauge2", "gauge2") })
                {
                    var old = parent.Find(name);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                    var gauge = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    gauge.name = name;
                    Wire(runner, field, gauge.GetComponent<MinigameGauge>());
                }
            });
            AssetDatabase.SaveAssets();
        }

        private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + name + ".png");

        private static void ConfigureImports()
        {
            foreach (var (name, border) in Sprites)
            {
                var path = ArtFolder + name + ".png";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    if (name.StartsWith("icon_")) Debug.LogWarning($"GaugeBuilder: {name}.png missing in Art/UI — that gauge has no icon.");
                    continue;
                }

                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.spriteBorder == border && importer.mipmapEnabled == false) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static GameObject BuildPrefab()
        {
            var root = new GameObject("MinigameGauge", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = Size;
                var group = root.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                // The drawn frame is semi-transparent: a second copy underneath keeps the bar readable over the scene.
                foreach (var layer in new[] { "Backing", "Frame" })
                {
                    var frame = UiObject(layer, rect);
                    Stretch(frame);
                    Sliced(frame, "gauge_frame", Load("gauge_frame").rect.height / Size.y);
                }

                var inner = UiObject("Inner", rect);
                Stretch(inner);
                inner.offsetMin = new Vector2(InnerInsetX, InnerInsetY);
                inner.offsetMax = new Vector2(-InnerInsetX, -InnerInsetY);
                var innerSize = Size - new Vector2(InnerInsetX, InnerInsetY) * 2f;

                // Zones under the fill: the fill sweeps over them, marker and star stay on top.
                var red = Zone(inner, "RedZone", "gauge_zone_red", innerSize.y, Color.white);
                var green = Zone(inner, "GreenZone", "gauge_zone_green", innerSize.y, Color.white);

                // The fill is full width; a mask shows only up to the current value.
                var mask = UiObject("FillMask", inner);
                mask.anchorMin = Vector2.zero;
                mask.anchorMax = new Vector2(0.4f, 1f);
                mask.offsetMin = mask.offsetMax = Vector2.zero;
                mask.pivot = new Vector2(0f, 0.5f);
                mask.gameObject.AddComponent<RectMask2D>();
                var fill = UiObject("Fill", mask);
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2(0f, 1f);
                fill.pivot = new Vector2(0f, 0.5f);
                fill.sizeDelta = new Vector2(innerSize.x, 0f);
                var fillImage = Sliced(fill, "gauge_fill", Load("gauge_fill").rect.height / innerSize.y);
                fillImage.color = new Color(0.91f, 0.584f, 0.353f);

                var star = UiObject("Star", inner);
                star.anchorMin = star.anchorMax = new Vector2(0.5f, 1f);
                star.pivot = new Vector2(0.5f, 0.5f);
                star.sizeDelta = new Vector2(26f, 26f);
                star.anchoredPosition = Vector2.zero;
                Simple(star, "gauge_star");

                var marker = UiObject("Marker", inner);
                marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 1f);
                marker.pivot = new Vector2(0.5f, 0f);
                marker.sizeDelta = new Vector2(24f, 38f);
                marker.anchoredPosition = new Vector2(0f, 10f);
                Simple(marker, "gauge_marker");

                var icon = UiObject("Icon", rect);
                icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
                icon.pivot = new Vector2(1f, 0.5f);
                icon.sizeDelta = new Vector2(52f, 52f);
                icon.anchoredPosition = new Vector2(-6f, 0f);
                var iconImage = icon.gameObject.AddComponent<Image>();
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                icon.gameObject.SetActive(false);

                var gauge = root.AddComponent<MinigameGauge>();
                Wire(gauge, "group", group);
                Wire(gauge, "inner", inner);
                Wire(gauge, "fillMask", mask);
                Wire(gauge, "fill", fillImage);
                Wire(gauge, "greenZone", green);
                Wire(gauge, "redZone", red);
                Wire(gauge, "marker", marker);
                Wire(gauge, "star", star);
                Wire(gauge, "icon", iconImage);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform Zone(RectTransform inner, string name, string sprite, float height, Color colour)
        {
            var zone = UiObject(name, inner);
            zone.anchorMin = new Vector2(0.4f, 0f);
            zone.anchorMax = new Vector2(0.6f, 1f);
            zone.offsetMin = zone.offsetMax = Vector2.zero;
            Sliced(zone, sprite, Load(sprite).rect.height / height).color = colour;
            return zone;
        }

        private static Image Sliced(RectTransform rect, string sprite, float multiplier)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Load(sprite);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
            image.raycastTarget = false;
            return image;
        }

        private static void Simple(RectTransform rect, string sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Load(sprite);
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        /// <summary>Icons on the steps; the old world-space meters (grind bar, flow bar, thermometer) removed.</summary>
        private static void PatchSteps()
        {
            var steps = new (string prefab, string icon, string[] oldMeters)[]
            {
                (P.GrindBeans, "icon_grind", new[] { "GrindMeter", "GrindMeterBack" }),
                (P.PourOver, "", new[] { "FlowMeter", "FlowMeterBack" }), // flow: no icon; the water gauge has the drop
                (P.SteamMilk, "icon_milk", new[] { "Thermometer" }),
                (P.InstantCoffee, "icon_flow", System.Array.Empty<string>()), // the fill gauge while pouring
                (P.PourWater, "icon_flow", System.Array.Empty<string>()),
            };

            foreach (var (prefab, icon, oldMeters) in steps)
            {
                var path = P.Folder + prefab + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var name in oldMeters)
                    {
                        var old = root.transform.Find(name);
                        if (old != null) Object.DestroyImmediate(old.gameObject);
                    }

                    Wire(root.GetComponent<MinigameStep>(), "gaugeIcon", string.IsNullOrEmpty(icon) ? null : Load(icon));
                    if (prefab == P.PourOver) Wire(root.GetComponent<MinigameStep>(), "waterIcon", Load("icon_flow"));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }
}
