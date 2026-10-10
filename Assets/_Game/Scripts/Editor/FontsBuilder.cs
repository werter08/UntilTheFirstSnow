using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// The game's fonts (Art/Fonts, SIL Open Font License): Amaranth for all text, Playpen Sans (handwritten) for the
    /// fridge notes. Creates dynamic TextMeshPro font assets (Amaranth with its real italic/bold faces, LiberationSans as
    /// glyph fallback) and assigns them in every scene and prefab. Idempotent.
    /// </summary>
    internal static class FontsBuilder
    {
        private const string Folder = "Assets/_Game/Art/Fonts/";

        /// <summary>Texts written in the handwriting font: the note card's body and signature.</summary>
        private static bool IsHandwritten(TMP_Text text) =>
            text.GetComponentInParent<UI.NoteCard>(true) != null && text.name is "Body" or "Signature";

        [MenuItem("Courtyard/Setup/Apply Fonts")]
        public static void Apply()
        {
            var fallback = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault();

            var italic = FontAsset("Amaranth-Italic", fallback);
            var bold = FontAsset("Amaranth-Bold", fallback);
            var boldItalic = FontAsset("Amaranth-BoldItalic", fallback);
            var amaranth = FontAsset("Amaranth-Regular", fallback);
            var playpen = FontAsset("PlaypenSans-Medium", fallback);
            if (amaranth == null || playpen == null)
            {
                Debug.LogWarning("FontsBuilder: fonts missing in Art/Fonts — nothing changed.");
                return;
            }

            // Real italic / bold faces instead of TMP's slanted or thickened regular.
            Edit(amaranth, so =>
            {
                var table = so.FindProperty("m_FontWeightTable");
                if (table == null || table.arraySize < 8) return;
                table.GetArrayElementAtIndex(4).FindPropertyRelative("italicTypeface").objectReferenceValue = italic;
                table.GetArrayElementAtIndex(7).FindPropertyRelative("regularTypeface").objectReferenceValue = bold;
                table.GetArrayElementAtIndex(7).FindPropertyRelative("italicTypeface").objectReferenceValue = boldItalic;
            });
            AssetDatabase.SaveAssets();

            foreach (var path in BuildScript.Scenes)
                EditScene(path, scene =>
                {
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                        Assign(text, IsHandwritten(text) ? playpen : amaranth);
                });

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var texts = root.GetComponentsInChildren<TMP_Text>(true);
                    if (texts.Length == 0) continue;
                    foreach (var text in texts) Assign(text, IsHandwritten(text) ? playpen : amaranth);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void Assign(TMP_Text text, TMP_FontAsset font)
        {
            if (text.font == font) return;
            text.font = font;
            text.fontSharedMaterial = font.material;
            EditorUtility.SetDirty(text);
        }

        /// <summary>A dynamic SDF font asset next to the TTF (created once; glyphs are added as text needs them).</summary>
        private static TMP_FontAsset FontAsset(string fileName, TMP_FontAsset fallback)
        {
            var assetPath = Folder + fileName + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + fileName + ".ttf");
            if (source == null) return null;

            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            font.name = fileName + " SDF";
            AssetDatabase.CreateAsset(font, assetPath);
            font.atlasTextures[0].name = fileName + " Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            font.material.name = fileName + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            if (fallback != null) font.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }
    }
}
