using System.Linq;
using Courtyard.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// The top HUD in the game's hand-drawn style (the team's sheet, sliced 1:1): warmth meters as a mug + drawn bar with a
    /// cold-blue → warm-orange fill (Mira's under his on Day 20), and a round restart button top-right (with a paper
    /// "are you sure?"; hints are always on) with handwritten tooltips. No plain rectangles, no "You"/"Mira" labels.
    /// </summary>
    internal static class HudBuilder
    {
        private const string Ui = "Assets/_Game/Art/UI/";
        private const float Margin = 32f;
        private static readonly Color Ink = new(0.24f, 0.16f, 0.12f);

        [MenuItem("Courtyard/Setup/Build HUD")]
        public static void Apply()
        {
            Import("hud_bar_cold", new Vector4(18f, 14f, 18f, 14f));
            Import("hud_bar_warm", new Vector4(18f, 14f, 18f, 14f));
            Import("hud_mug_him", Vector4.zero);
            Import("ui_button_restart", Vector4.zero);
            if (Load(Ui + "hud_bar_cold") == null || Load(Ui + "ui_button_restart") == null)
            {
                Debug.LogWarning("HudBuilder: HUD art missing in Art/UI — nothing changed.");
                return;
            }

            EditScene(BuildScript.Scenes[1], scene =>
            {
                var canvas = scene.GetRootGameObjects().First(g => g.name == "Canvas").transform;
                var hud = canvas.Find("HUD") ?? canvas;
                var hand = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Art/Fonts/PlaypenSans-Medium SDF.asset");

                var him = hud.Find("WarmthMeter_Him");
                var mira = hud.Find("WarmthMeter_Mira");
                if (him != null) Meter((RectTransform)him, Load(Ui + "hud_mug_him"), 0);
                if (mira != null) Meter((RectTransform)mira, Load("Assets/_Game/Art/Props/mug_hers"), 1);

                Buttons(canvas, hand);
            });
        }

        private static Sprite Load(string pathWithoutExtension) => AssetDatabase.LoadAssetAtPath<Sprite>(pathWithoutExtension + ".png");

        private static void Import(string name, Vector4 border)
        {
            if (AssetImporter.GetAtPath(Ui + name + ".png") is not TextureImporter importer) return;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == border) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- meters

        /// <summary>Mug on the left, the drawn bar beside it; the fill is a mask over the cold and warm drawings.</summary>
        private static void Meter(RectTransform meter, Sprite mug, int row)
        {
            for (var i = meter.childCount - 1; i >= 0; i--) Object.DestroyImmediate(meter.GetChild(i).gameObject);
            var panel = meter.GetComponent<Image>();
            if (panel != null) Object.DestroyImmediate(panel); // no plain rectangle behind the meter
            Place(meter, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -Margin - row * 72f), new Vector2(400f, 60f));

            var glow = UiObject("Glow", meter);
            Place(glow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(228f, 0f), new Vector2(400f, 100f));
            var glowImage = Img(glow, new Color(1f, 0.75f, 0.45f, 0f));
            glowImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/FX/glow_radial.png");

            var icon = UiObject("Mug", meter);
            Place(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(28f, 0f), new Vector2(56f, 56f));
            var iconImage = Img(icon, Color.white);
            iconImage.sprite = mug;
            iconImage.preserveAspect = true;

            var bar = UiObject("Bar", meter);
            Place(bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(68f, 0f), new Vector2(320f, 44f));
            var frame = Load(Ui + "gauge_frame");
            var track = Img(bar, Color.white);
            track.sprite = frame;
            track.type = Image.Type.Sliced;
            if (frame != null) track.pixelsPerUnitMultiplier = frame.rect.height / 44f;

            var mask = UiObject("Fill", bar);
            mask.anchorMin = Vector2.zero;
            mask.anchorMax = new Vector2(0.7f, 1f);
            mask.offsetMin = new Vector2(10f, 8f);
            mask.offsetMax = new Vector2(0f, -8f);
            mask.pivot = new Vector2(0f, 0.5f);
            mask.gameObject.AddComponent<RectMask2D>();
            var innerWidth = 320f - 20f;
            var cold = FillImage(mask, "Cold", Load(Ui + "hud_bar_cold"), innerWidth);
            var warm = FillImage(mask, "Warm", Load(Ui + "hud_bar_warm"), innerWidth);

            var view = meter.GetComponent<WarmthMeterView>();
            Wire(view, "fill", mask);
            Wire(view, "fillImage", cold);
            Wire(view, "warmFill", warm);
            Wire(view, "mugIcon", iconImage);
            Wire(view, "glow", glowImage);
            Wire(view, "steamPuff", glowImage.sprite);
        }

        private static Image FillImage(RectTransform mask, string name, Sprite sprite, float width)
        {
            var rect = UiObject(name, mask);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            var image = Img(rect, Color.white);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            if (sprite != null) image.pixelsPerUnitMultiplier = sprite.rect.height / 28f;
            return image;
        }

        // ---------------------------------------------------------------- corner buttons

        private static void Buttons(Transform canvas, TMP_FontAsset hand)
        {
            foreach (var old in new[] { "HintsToggle", "RestartDay", "CornerButtons", "ConfirmPopup" })
            {
                var t = canvas.Find(old);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            var corner = UiObject("CornerButtons", canvas);
            Stretch(corner);
            var cornerGroup = corner.gameObject.AddComponent<CanvasGroup>();
            cornerGroup.blocksRaycasts = true;
            // Hints are always on: the restart button alone in the corner.
            var restart = Round(corner, "Restart", Load(Ui + "ui_button_restart"), 0, "Restart day", hand);
            var restartButton = restart.gameObject.AddComponent<RestartDayButton>();
            Wire(restartButton, "confirm", Confirm(canvas, hand));

            // Under the note card and the screen fade.
            var card = canvas.GetComponentInChildren<NoteCard>(true);
            if (card != null) corner.SetSiblingIndex(card.transform.GetSiblingIndex());
        }

        /// <summary>A round drawn button with its icon, a tooltip under it; slot 0 is the rightmost.</summary>
        private static RectTransform Round(RectTransform parent, string name, Sprite sprite, int slot, string tip, TMP_FontAsset font)
        {
            const float size = 64f;
            var rect = UiObject(name, parent);
            Place(rect, Vector2.one, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(-Margin - size * 0.5f - slot * (size + 16f), -Margin - size * 0.5f), new Vector2(size, size));
            var image = Img(rect, Color.white, true);
            image.sprite = sprite;
            image.preserveAspect = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            rect.gameObject.AddComponent<CanvasGroup>();

            var label = Label(rect, "Tooltip", tip, 24f, TextAlignmentOptions.Top, Ink);
            Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(220f, 36f));
            if (font != null) label.font = font;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var round = rect.gameObject.AddComponent<RoundButton>();
            Wire(round, "tooltip", label);
            return rect;
        }

        /// <summary>"Start this day over?" on a small paper note, with [yes] and [no].</summary>
        private static ConfirmPopup Confirm(Transform canvas, TMP_FontAsset font)
        {
            var root = UiObject("ConfirmPopup", canvas);
            Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var dim = UiObject("Dim", root);
            Stretch(dim);
            Img(dim, new Color(0.08f, 0.06f, 0.05f, 0.35f), true);

            var paperArt = Load("Assets/_Game/Art/Props/note_open_yellow");
            var paper = UiObject("Paper", root);
            Place(paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                paperArt != null ? paperArt.rect.size * 1.4f : new Vector2(520f, 320f));
            paper.localRotation = Quaternion.Euler(0f, 0f, -2f);
            var paperImage = Img(paper, paperArt != null ? Color.white : new Color(1f, 0.95f, 0.78f));
            paperImage.sprite = paperArt;
            paperImage.preserveAspect = true;

            var question = Label(paper, "Question", "Start this day over?", 40f, TextAlignmentOptions.Center, Ink);
            question.rectTransform.offsetMin = new Vector2(40f, 110f);
            question.rectTransform.offsetMax = new Vector2(-60f, -40f);
            if (font != null) question.font = font;

            var yes = Choice(paper, "Yes", "yes", new Vector2(-80f, 70f), font);
            var no = Choice(paper, "No", "no", new Vector2(80f, 70f), font);

            var popup = root.gameObject.AddComponent<ConfirmPopup>();
            Wire(popup, "group", group);
            Wire(popup, "question", question);
            Wire(popup, "yes", yes);
            Wire(popup, "no", no);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            root.SetAsLastSibling();
            return popup;
        }

        private static Button Choice(RectTransform paper, string name, string text, Vector2 position, TMP_FontAsset font)
        {
            var rect = UiObject(name, paper);
            Place(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), position, new Vector2(130f, 64f));
            var hit = Img(rect, new Color(1f, 1f, 1f, 0.001f), true); // invisible hit area
            var label = Label(rect, "Label", "[ " + text + " ]", 40f, TextAlignmentOptions.Center, Ink);
            if (font != null) label.font = font;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            rect.gameObject.AddComponent<MenuButton>(); // lifts and tilts under the hand
            return button;
        }
    }
}
