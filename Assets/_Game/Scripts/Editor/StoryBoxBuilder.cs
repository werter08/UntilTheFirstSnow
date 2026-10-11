using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// The story box drawn by the team (talking_bg.png, 1:1, 9-sliced so it stretches to the box without bending its
    /// rounded corners). The drawing is grey, so the styles tint it lightly and keep light text on it.
    /// </summary>
    internal static class StoryBoxBuilder
    {
        private const string ArtPath = "Assets/_Game/Art/UI/talking_bg.png";
        private const string PrefabPath = "Assets/_Game/Prefabs/UI/StoryBox.prefab";

        [MenuItem("Courtyard/Setup/Apply Story Box Art")]
        public static void Apply()
        {
            if (AssetImporter.GetAtPath(ArtPath) is not TextureImporter importer)
            {
                Debug.LogWarning("StoryBoxBuilder: talking_bg.png missing in Art/UI.");
                return;
            }

            var border = new Vector4(48f, 48f, 48f, 48f); // the rounded corners and the outline
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var image = root.GetComponent<Image>();
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
                var player = root.GetComponent<Story.StoryPlayer>();
                Edit(player, so =>
                {
                    // Tints over the grey drawing (white = as drawn); text stays light on all of them.
                    so.FindProperty("defaultBackground").colorValue = new Color(1f, 1f, 1f, 0.95f);
                    so.FindProperty("defaultText").colorValue = new Color(0.98f, 0.96f, 0.92f);
                    so.FindProperty("noteBackground").colorValue = new Color(1f, 0.9f, 0.74f, 0.97f);
                    so.FindProperty("noteText").colorValue = new Color(1f, 0.98f, 0.93f);
                    so.FindProperty("phoneBackground").colorValue = new Color(0.78f, 0.88f, 1f, 0.95f);
                    so.FindProperty("phoneText").colorValue = Color.white;
                });
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
