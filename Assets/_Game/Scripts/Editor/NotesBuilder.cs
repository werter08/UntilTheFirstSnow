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

            ApplyNoteArt(slots);
            var card = EnsureNoteCard(scene);
            ApplyOpenNoteArt(card);
            var director = kitchen.GetComponent<KitchenDirector>();
            WireArray(director, "fridgeNotes", slots.Cast<Object>().ToArray());
            Wire(director, "notesBook", book);
            Wire(director, "noteCard", card);
        }

        private const string PropsFolder = "Assets/_Game/Art/Props/";
        private const string NoteArt = PropsFolder + "note.png";

        /// <summary>
        /// The team's sticky notes on the fridge (1:1): yellow and pink alternate, each tilted by a fixed random angle
        /// (−8°…8°, the same every build), magnets on top. Warns if two notes overlap by more than half.
        /// Falls back to note.png, then to the placeholders.
        /// </summary>
        private static void ApplyNoteArt(GameObject[] slots)
        {
            Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/Props/{n}.png");
            var yellow = new[] { Load("note_yellow_1"), Load("note_yellow_2"), Load("note_yellow_3") }.Where(x => x != null).ToArray();
            var pink = new[] { Load("note_pink_1"), Load("note_pink_2") }.Where(x => x != null).ToArray();
            var single = AssetDatabase.LoadAssetAtPath<Sprite>(NoteArt);
            if (yellow.Length == 0 && single == null)
            {
                Debug.LogWarning("NotesBuilder: no note drawings — the fridge notes stay sticky-note placeholders.");
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var art = yellow.Length == 0 ? single
                    : i % 2 == 1 && pink.Length > 0 ? pink[(i / 2) % pink.Length]
                    : yellow[(i / 2) % yellow.Length];
                var slot = slots[i].transform;
                var renderer = slot.GetComponent<SpriteRenderer>();
                renderer.sprite = art;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.color = Color.white;
                slot.localScale = Vector3.one;
                var angle = (float)(new System.Random(7919 * (i + 1)).NextDouble() * 16.0 - 8.0);
                slot.localRotation = Quaternion.Euler(0f, 0f, angle);
                var collider = slot.GetComponent<BoxCollider2D>();
                if (collider != null)
                {
                    collider.size = art.bounds.size;
                    collider.offset = art.bounds.center;
                }

                var magnet = slot.Find("Magnet");
                if (magnet != null) magnet.localPosition = new Vector3(0f, art.bounds.max.y - 0.04f, 0f);
            }

            // Overlap check (axis-aligned, generous): at most half of a note may hide another.
            for (var i = 0; i < slots.Length; i++)
            for (var j = i + 1; j < slots.Length; j++)
            {
                var a = slots[i].GetComponent<SpriteRenderer>().bounds;
                var b = slots[j].GetComponent<SpriteRenderer>().bounds;
                var w = Mathf.Max(0f, Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x));
                var h = Mathf.Max(0f, Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y));
                var share = w * h / Mathf.Min(a.size.x * a.size.y, b.size.x * b.size.y);
                if (share > 0.5f) Debug.LogWarning($"NotesBuilder: FridgeNote{i} and FridgeNote{j} overlap by {share:P0} — move one.");
            }
        }

        /// <summary>
        /// The open note on screen: the team's paper drawings (1:1, sliced from their sheet) — a yellow or pink sticky
        /// note matching the note on the fridge, and the torn calendar page for the calendar. Sizes are the drawings
        /// scaled up to read at 1080p; padding keeps the writing off the curl, the red header and the torn edge.
        /// </summary>
        private static void ApplyOpenNoteArt(NoteCard card)
        {
            PaperLook Look(NotePaper kind, string file, float scale, Vector4 padding)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + file + ".png");
                if (sprite == null)
                {
                    Debug.LogWarning($"NotesBuilder: {file}.png missing in Art/Props — that page keeps plain paper.");
                    return null;
                }

                return new PaperLook { paper = kind, sprite = sprite, size = sprite.rect.size * scale, padding = padding };
            }

            var looks = new[]
            {
                Look(NotePaper.Yellow, "note_open_yellow", 2.2f, new Vector4(70f, 80f, 90f, 60f)),
                Look(NotePaper.Pink, "note_open_pink", 2.2f, new Vector4(70f, 80f, 90f, 60f)),
                Look(NotePaper.Calendar, "calendar_page", 1.6f, new Vector4(50f, 150f, 50f, 120f)),
            }.Where(l => l != null).ToArray();

            Edit(card, so =>
            {
                var list = so.FindProperty("papers");
                list.arraySize = looks.Length;
                for (var i = 0; i < looks.Length; i++)
                {
                    var item = list.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("paper").enumValueIndex = (int)looks[i].paper;
                    item.FindPropertyRelative("sprite").objectReferenceValue = looks[i].sprite;
                    item.FindPropertyRelative("size").vector2Value = looks[i].size;
                    item.FindPropertyRelative("padding").vector4Value = looks[i].padding;
                    item.FindPropertyRelative("ink").colorValue = looks[i].ink;
                }
            });

            // Signature sits left of the curled corner.
            var signature = card.transform.Find("Paper/Signature") as RectTransform;
            if (signature != null)
            {
                signature.offsetMin = new Vector2(60f, 50f);
                signature.offsetMax = new Vector2(-150f, -330f);
            }

            var paper = card.transform.Find("Paper")?.GetComponent<Image>();
            if (paper != null && looks.Length > 0)
            {
                paper.sprite = looks[0].sprite;
                paper.color = Color.white;
                paper.preserveAspect = true;
                ((RectTransform)paper.transform).sizeDelta = looks[0].size;
            }
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
