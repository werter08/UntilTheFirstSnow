using System.Collections.Generic;
using Courtyard.Core;
using UnityEditor;
using UnityEngine;

namespace Courtyard.Editor
{
    /// <summary>
    /// Import settings for the hand-drawn hands: one size for all, and each pivot on the fingertip / grip point
    /// so the hand lands exactly on the pointer. Missing files fall back to the placeholder box.
    /// </summary>
    internal static class HandArt
    {
        private const string Folder = "Assets/_Game/Art/Sprites/Hands/";
        private const float PixelsPerUnit = 500f;

        /// <summary>Grip point per pose, normalised from the bottom-left of the image.</summary>
        private static readonly Dictionary<(HandPose, Sweater), Vector2> Pivots = new()
        {
            [(HandPose.Pinch, Sweater.Grey)] = new Vector2(0.755f, 0.71f),
            [(HandPose.Pinch, Sweater.Beige)] = new Vector2(0.755f, 0.71f),
            [(HandPose.Open, Sweater.Grey)] = new Vector2(0.56f, 0.76f),
            [(HandPose.Open, Sweater.Beige)] = new Vector2(0.56f, 0.76f),
            [(HandPose.Point, Sweater.Grey)] = new Vector2(0.41f, 0.97f),
            [(HandPose.Point, Sweater.Beige)] = new Vector2(0.41f, 0.97f),
            [(HandPose.Grip, Sweater.Grey)] = new Vector2(0.9f, 0.64f),
            [(HandPose.Grip, Sweater.Beige)] = new Vector2(0.775f, 0.77f),
            [(HandPose.RaiseMug, Sweater.Grey)] = new Vector2(0.81f, 0.86f),
            [(HandPose.RaiseMug, Sweater.Beige)] = new Vector2(0.81f, 0.86f),
        };

        public static Sprite Load(HandPose pose, Sweater sweater)
        {
            var path = $"{Folder}Hand_{pose}_{sweater}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            var pivot = Pivots[(pose, sweater)];
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            var needsImport = importer.textureType != TextureImporterType.Sprite || settings.spriteAlignment != (int)SpriteAlignment.Custom
                              || settings.spritePivot != pivot || !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit);
            if (needsImport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 1024;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.Tight;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
