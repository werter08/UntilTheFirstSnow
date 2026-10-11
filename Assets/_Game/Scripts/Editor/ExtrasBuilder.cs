using System.Linq;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEditor;
using UnityEngine;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// Small extras on top of the built kitchen: fog on the window glass (Days 20 and 30), the perfect-day star sticker on
    /// the calendar, and what the post-credits morning needs (its light, the empty sill mugs, the UI it hides).
    /// Idempotent; runs at the end of the decor pass.
    /// </summary>
    internal static class ExtrasBuilder
    {
        private const string LevelsFolder = "Assets/_Game/Data/Levels/";
        private static readonly int[] FoggyDays = { 20, 30 };
        private const float StarScale = 2.5f; // the gauge's star, as a sticker

        [MenuItem("Courtyard/Setup/Build Extras (fog, star, epilogue)")]
        public static void Apply()
        {
            PatchLevels();
            ApplyCursor();
            EditScene(BuildScript.Scenes[1], scene =>
            {
                var kitchen = scene.GetRootGameObjects().First(g => g.name == "Kitchen").transform;
                var director = kitchen.GetComponent<KitchenDirector>();
                Fog(kitchen, director);
                Star(kitchen, director);
                SillContents(kitchen, director);
                MorningNoteOrder(kitchen);
                var hands = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<HandsView>(true)).FirstOrDefault(h => h != null);
                if (hands != null)
                    Edit(hands, so =>
                    {
                        so.FindProperty("cursorTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/UI/cursor.png");
                        so.FindProperty("cursorHotspot").vector2Value = CursorHotspot;
                    });

                var runner = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<LevelRunner>(true)).First(r => r != null);
                var canvas = scene.GetRootGameObjects().First(g => g.name == "Canvas").transform;
                var hidden = new[] { "CornerButtons", "WarmthVignette" }.Select(n => canvas.Find(n)).Where(t => t != null)
                    .Select(t => (Object)t.gameObject).ToArray();
                WireArray(runner, "hiddenInEpilogue", hidden);
            });
        }

        /// <summary>The team's hand-drawn cursor (Art/UI/cursor.png), clicking at the top-left of the shape.</summary>
        private static void ApplyCursor()
        {
            const string path = "Assets/_Game/Art/UI/cursor.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning("ExtrasBuilder: " + path + " missing — the system cursor stays.");
                return;
            }

            if (importer.textureType != TextureImporterType.Cursor)
            {
                importer.textureType = TextureImporterType.Cursor;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            PlayerSettings.defaultCursor = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            PlayerSettings.cursorHotspot = CursorHotspot;
        }

        /// <summary>Pixel of the cursor drawing that clicks (from its top-left).</summary>
        private static readonly Vector2 CursorHotspot = new(6f, 3f);

        private static void PatchLevels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { LevelsFolder }))
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (level == null) continue;
                Edit(level, so =>
                {
                    so.FindProperty("foggyWindow").boolValue = FoggyDays.Contains(level.DayNumber);
                    if (!level.Finale) return;
                    // The morning after: soft daylight, the lamp off, a pale winter sky.
                    var palette = so.FindProperty("epiloguePalette");
                    palette.FindPropertyRelative("globalLightColor").colorValue = new Color(1f, 0.96f, 0.9f);
                    palette.FindPropertyRelative("globalLightIntensity").floatValue = 1.05f;
                    palette.FindPropertyRelative("lampColor").colorValue = new Color(1f, 0.9f, 0.75f);
                    palette.FindPropertyRelative("lampIntensity").floatValue = 0f;
                    palette.FindPropertyRelative("miraWindowColor").colorValue = new Color(0.85f, 0.88f, 0.95f);
                    palette.FindPropertyRelative("vignetteTint").colorValue = new Color(0f, 0f, 0f, 0f);
                    so.FindProperty("epilogueSky").colorValue = new Color(0.86f, 0.9f, 0.96f);
                });
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>White fog inside the window mask, above the courtyard and its weather, under the frame.</summary>
        private static void Fog(Transform kitchen, KitchenDirector director)
        {
            var mask = kitchen.GetComponentsInChildren<SpriteMask>(true).FirstOrDefault(m => m.name == "WindowMask");
            if (mask == null)
            {
                Debug.LogWarning("ExtrasBuilder: no WindowMask — no window fog.");
                return;
            }

            var fog = kitchen.Find("WindowFog");
            if (fog == null) fog = new GameObject("WindowFog", typeof(SpriteRenderer), typeof(WindowFog)).transform;
            fog.SetParent(kitchen, false);
            var bounds = mask.bounds;
            fog.position = new Vector3(bounds.center.x, bounds.center.y, 0f);
            fog.localRotation = Quaternion.identity;
            fog.localScale = Vector3.one;
            var renderer = fog.GetComponent<SpriteRenderer>();
            renderer.sortingLayerName = "Weather";
            renderer.sortingOrder = 20;
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            var view = fog.GetComponent<WindowFog>();
            Edit(view, so =>
            {
                so.FindProperty("size").vector2Value = new Vector2(bounds.size.x, bounds.size.y);
                so.FindProperty("worldCamera").objectReferenceValue = Camera.main != null
                    ? Camera.main
                    : Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            });
            fog.gameObject.SetActive(false); // the level turns it on
            Wire(director, "windowFog", view);
        }

        /// <summary>The gold star sticker, hidden, at the lower right of the calendar page (swings with the calendar).</summary>
        private static void Star(Transform kitchen, KitchenDirector director)
        {
            var calendar = Find(kitchen, "Calendar", false);
            var art = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/UI/gauge_star.png");
            if (calendar == null || art == null) return;
            var page = calendar.GetComponent<SpriteRenderer>();
            var parent = calendar.parent != null && calendar.parent.name == "CalendarGroup" ? calendar.parent : calendar;
            var star = parent.Find("CalendarStar");
            if (star == null) star = new GameObject("CalendarStar", typeof(SpriteRenderer)).transform;
            star.SetParent(parent, false);
            var b = page.bounds;
            star.position = new Vector3(b.max.x - b.size.x * 0.22f, b.min.y + b.size.y * 0.24f, 0f);
            star.localScale = Vector3.one * StarScale / Mathf.Max(0.01f, parent.lossyScale.x);
            var renderer = star.GetComponent<SpriteRenderer>();
            renderer.sprite = art;
            renderer.sortingLayerID = page.sortingLayerID;
            renderer.sortingOrder = page.sortingOrder + 10;
            star.gameObject.SetActive(false);
            Wire(director, "calendarStar", renderer);
        }

        /// <summary>The morning-after note (sixth slot) and its magnet sit one step above the other notes (the team's choice).</summary>
        private static void MorningNoteOrder(Transform kitchen)
        {
            var note = kitchen.Find("FridgeNote5");
            if (note == null) return;
            var renderer = note.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.sortingOrder = 22;
            var magnet = note.Find("Magnet")?.GetComponent<SpriteRenderer>();
            if (magnet != null) magnet.sortingOrder = 23;
        }

        /// <summary>The drink and steam in the two sill mugs: hidden when the mugs stand empty, the morning after.</summary>
        private static void SillContents(Transform kitchen, KitchenDirector director)
        {
            var sill = Find(kitchen, "SillMugs", false);
            if (sill == null) return;
            var parts = new[] { "SillMugHis", "SillMugHers" }
                .Select(n => sill.Find(n)).Where(m => m != null)
                .SelectMany(m => new[] { m.Find("Drink"), m.Find("Steam") })
                .Where(t => t != null).Select(t => (Object)t.gameObject).ToArray();
            WireArray(director, "sillMugContents", parts);
        }
    }
}
