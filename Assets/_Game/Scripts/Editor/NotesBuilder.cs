using System.Linq;
using Courtyard.Core;
using Courtyard.Kitchen;
using Courtyard.Story;
using Courtyard.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// Fridge notes you can read: the notes asset (draft texts, created once — edit it freely), five note slots on the
    /// fridge (one per day; the two new ones are copies of the team's third note), and the reading card on the HUD.
    /// </summary>
    internal static class NotesBuilder
    {
        private const string NotesPath = "Assets/_Game/Data/Story/FridgeNotes.asset";
        private const int Slots = 5;

        /// <summary>Draft notes (≤ 12 words a line). Day 10's note quietly records her usual for Day 30.</summary>
        private static readonly (int day, string from, string text)[] Drafts =
        {
            (1, "", "Buy real coffee.\nInstant tastes like Monday."),
            (5, "", "She waved first.\nNext time, wave back sooner."),
            (10, "Mira", "Thank you for the coffee!\nMy usual: cinnamon and honey."),
            (20, "Mira", "The heating's fixed.\nThank you for staying up with me."),
            (30, "", "First snow.\nTwo mugs on the sill.\nWarm."),
        };

        /// <summary>Extra slots next to the team's three notes (world units, on the fridge door).</summary>
        private static readonly Vector2[] NewSlotPositions = { new(8.6f, 0.55f), new(8.15f, -2.55f) };

        public static NotesBook EnsureNotes()
        {
            var book = AssetDatabase.LoadAssetAtPath<NotesBook>(NotesPath);
            if (book != null) return book; // never overwrite the team's edits

            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Story")) AssetDatabase.CreateFolder("Assets/_Game/Data", "Story");
            book = ScriptableObject.CreateInstance<NotesBook>();
            AssetDatabase.CreateAsset(book, NotesPath);
            Edit(book, so =>
            {
                var list = so.FindProperty("notes");
                list.arraySize = Drafts.Length;
                for (var i = 0; i < Drafts.Length; i++)
                {
                    var note = list.GetArrayElementAtIndex(i);
                    note.FindPropertyRelative("afterDay").intValue = Drafts[i].day;
                    note.FindPropertyRelative("from").stringValue = Drafts[i].from;
                    note.FindPropertyRelative("text").stringValue = Drafts[i].text;
                }
            });
            AssetDatabase.SaveAssets();
            return book;
        }

        public static void ApplyToKitchen(UnityEngine.SceneManagement.Scene scene, Transform kitchen)
        {
            var book = EnsureNotes();

            // Five slots: the team's three notes, plus copies of the third for days 20 and 30.
            var slots = new GameObject[Slots];
            for (var i = 0; i < Slots; i++)
            {
                var slot = kitchen.Find($"FridgeNote{i}");
                if (slot == null)
                {
                    var source = kitchen.Find("FridgeNote2");
                    if (source == null)
                    {
                        Debug.LogWarning("NotesBuilder: no FridgeNote2 to copy for more note slots.");
                        return;
                    }

                    slot = Object.Instantiate(source.gameObject, kitchen).transform;
                    slot.name = $"FridgeNote{i}";
                    var p = NewSlotPositions[Mathf.Min(i - 3, NewSlotPositions.Length - 1)];
                    slot.localPosition = new Vector3(p.x, p.y, source.localPosition.z);
                }

                if (slot.GetComponent<ReadableNote>() == null) slot.gameObject.AddComponent<ReadableNote>();
                slots[i] = slot.gameObject;
            }

            var card = EnsureNoteCard(scene);
            var director = kitchen.GetComponent<KitchenDirector>();
            WireArray(director, "fridgeNotes", slots.Cast<Object>().ToArray());
            Wire(director, "notesBook", book);
            Wire(director, "noteCard", card);
        }

        /// <summary>The reading card: a dimmed screen with the paper note in the middle (above the story box).</summary>
        private static NoteCard EnsureNoteCard(UnityEngine.SceneManagement.Scene scene)
        {
            var canvas = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<Canvas>(true)).First(c => c != null);
            var existing = canvas.GetComponentInChildren<NoteCard>(true);
            if (existing != null)
            {
                OnTop(canvas, existing.transform);
                return existing;
            }

            var font = canvas.GetComponentsInChildren<TMP_Text>(true).Select(t => t.font).FirstOrDefault(f => f != null);
            var root = UiObject("NoteCard", canvas.transform);
            Stretch(root);
            OnTop(canvas, root);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var dim = UiObject("Dim", root);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.06f, 0.05f, 0.45f);

            var paper = UiObject("Paper", root);
            paper.sizeDelta = new Vector2(760f, 470f);
            paper.localRotation = Quaternion.Euler(0f, 0f, -2f);
            paper.gameObject.AddComponent<Image>().color = new Color(1f, 0.95f, 0.78f);

            var body = Text(paper, "Body", font, 46, FontStyles.Normal, new Color(0.22f, 0.17f, 0.14f), TextAlignmentOptions.Center);
            body.rectTransform.offsetMin = new Vector2(60f, 90f);
            body.rectTransform.offsetMax = new Vector2(-60f, -50f);

            var signature = Text(paper, "Signature", font, 36, FontStyles.Italic, new Color(0.45f, 0.3f, 0.25f), TextAlignmentOptions.BottomRight);
            signature.rectTransform.offsetMin = new Vector2(60f, 40f);
            signature.rectTransform.offsetMax = new Vector2(-60f, -330f);

            var hint = Text(root, "CloseHint", font, 26, FontStyles.Italic, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Bottom);
            hint.text = "click to put it back";
            hint.rectTransform.offsetMin = new Vector2(0f, 60f);
            hint.rectTransform.offsetMax = new Vector2(0f, -900f);

            var card = root.gameObject.AddComponent<NoteCard>();
            Wire(card, "group", group);
            Wire(card, "paper", paper);
            Wire(card, "body", body);
            Wire(card, "signature", signature);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            return card;
        }

        /// <summary>Above every HUD element (story box, hint, meters) — only the screen fade stays in front.</summary>
        private static void OnTop(Canvas canvas, Transform card)
        {
            card.SetAsLastSibling();
            foreach (var fader in canvas.GetComponentsInChildren<ScreenFader>(true)) fader.transform.SetAsLastSibling();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI Text(RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style,
            Color colour, TextAlignmentOptions alignment)
        {
            var rect = UiObject(name, parent);
            Stretch(rect);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }
    }
}
