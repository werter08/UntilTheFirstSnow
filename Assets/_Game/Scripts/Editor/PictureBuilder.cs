using System.Linq;
using Courtyard.Kitchen;
using Courtyard.UI;
using UnityEditor;
using UnityEngine;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// The framed picture on the kitchen wall opens up close on a click (the team's drawing, Art/Props/wall_picture_open).
    /// </summary>
    internal static class PictureBuilder
    {
        private const string Art = "Assets/_Game/Art/Props/wall_picture_open.png";
        private const float Height = 940f; // of the 1080 reference screen

        [MenuItem("Courtyard/Setup/Build Wall Picture")]
        public static void Apply()
        {
            if (AssetImporter.GetAtPath(Art) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art);
            if (sprite == null)
            {
                Debug.LogWarning("PictureBuilder: " + Art + " missing — nothing changed.");
                return;
            }

            EditScene(BuildScript.Scenes[1], scene =>
            {
                var canvas = scene.GetRootGameObjects().First(g => g.name == "Canvas").transform;
                var old = canvas.Find("PictureCard");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var root = UiObject("PictureCard", canvas);
                Stretch(root);
                var group = root.gameObject.AddComponent<CanvasGroup>();
                var dim = UiObject("Dim", root);
                Stretch(dim);
                Img(dim, new Color(0.05f, 0.04f, 0.04f, 0.7f), true);

                var picture = UiObject("Picture", root);
                var size = new Vector2(Height * sprite.rect.width / sprite.rect.height, Height);
                Place(picture, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
                var image = Img(picture, Color.white);
                image.sprite = sprite;
                image.preserveAspect = true;

                var card = root.gameObject.AddComponent<PictureCard>();
                Wire(card, "group", group);
                Wire(card, "picture", picture);
                group.alpha = 0f;
                group.blocksRaycasts = false;

                // Over the HUD and the notes, under the screen fade.
                var notes = canvas.GetComponentInChildren<NoteCard>(true);
                if (notes != null) root.SetSiblingIndex(notes.transform.GetSiblingIndex() + 1);

                var wall = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t => t.name == "WallPicture");
                if (wall == null || wall.GetComponent<Core.PointerTarget>() == null)
                {
                    Debug.LogWarning("PictureBuilder: WallPicture (clickable) not found.");
                    return;
                }

                var viewable = wall.GetComponent<ViewablePicture>();
                if (viewable == null) viewable = wall.gameObject.AddComponent<ViewablePicture>();
                Wire(viewable, "card", card);
                Wire(viewable, "notes", notes);
            });
        }
    }
}
