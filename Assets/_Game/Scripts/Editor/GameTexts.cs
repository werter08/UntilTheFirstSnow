using System.Collections.Generic;
using System.Linq;
using Courtyard.Core;
using Courtyard.Minigames;
using Courtyard.Story;
using TMPro;
using UnityEditor;
using UnityEngine;
using static Courtyard.Editor.BuilderKit;
using P = Courtyard.Editor.MinigamePrefabBuilder;

namespace Courtyard.Editor
{
    /// <summary>
    /// Every player-facing text of the game in one table (the team's final copy), written into the level assets, the
    /// step prefabs, the fridge notes, the ending and the scenes' fixed labels. Structure (triggers, steps) stays; only
    /// the strings change — except Day 20's "Two cups tonight" beat, which follows his auto-filled cup (step 5).
    /// Run it after editing this table; it re-exports Docs/Texts.md for checking.
    /// </summary>
    internal static class GameTexts
    {
        private const string LevelsFolder = "Assets/_Game/Data/Levels/";
        private const string NotesPath = "Assets/_Game/Data/Story/FridgeNotes.asset";
        private const string EndingPath = "Assets/_Game/Data/Story/Ending.asset";

        private const string BadDrink = "Ugh. Cold and bitter.";
        private const string TooCold = "Too cold. Let's try that again.";

        private sealed class Beat
        {
            public BeatTrigger Trigger;
            public int Step;
            public BeatCondition Condition = BeatCondition.Always;
            public BeatStyle Style;
            public string Speaker = "";
            public string Text;
        }

        private sealed class Day
        {
            public int Number;
            public string Calendar;
            public string BadDrink;
            public string TooCold;
            public bool Tutorial;
            public bool PulseRecipe;
            public string Recipe;
            public Beat[] Beats;
        }

        // ---------------------------------------------------------------- hints (step prefabs)

        /// <summary>(prefab, hint, always shown — story prompts that are new every time).</summary>
        private static readonly (string prefab, string hint, bool always)[] Hints =
        {
            (P.KettleBoil, "Turn the kettle on. Turn it off when it boils.", false),
            (P.InstantCoffee, "Two spoons of coffee. Then pour water up to the line.", false),
            (P.GrindBeans, "Hold the handle and stir in circles. Stop in the green.", false),
            (P.PourOver, "Pour slowly, in circles. Keep it in the green. Fill to the line.", false),
            (P.WaveBack, "She's waving. Click your mug to wave back.", true),
            (P.MiraVisit, "", false),
            (P.TakeHerCup, "Take the jar.", true),
            (P.SteamMilk, "Hold to steam the milk. Let go in the green.", false),
            (P.GuessHerDrink, "Add two spices to her cup, one at a time. Then click the cup.", true),
            (P.ChooseMug, "Her mug first. Which one is hers?", true),
            (P.HerDrinkFromMemory, "Make her usual.", true),
            (P.AutoFillHisMug, "", false),
            (P.PourWater, "One spoon of coffee into her cup. Then pour water to the line.", false), // her coffee
            (P.ReadHerWindow, "Something's on her window. Tap it.", true),
            (P.PlaceV60, "Put the V60 on the cup.", false),
        };

        // ---------------------------------------------------------------- the recipe card (grows over the autumn)

        private const string RecipeInstant = "<b>Instant coffee</b>\n1) boil water   2) two spoons   3) pour to the line";
        private const string RecipePourOver = "\n\n<b>Pour-over</b>\n1) boil, switch off when it boils   2) grind to the green\n3) slow circles, keep the flow green, fill to the line";
        private const string RecipeMilk = "\n\n<b>Milk</b> — hold to steam, let go in the green";
        private const string RecipeTwoMugs = "\n\nTwo mugs tonight. Hers first.";

        // ---------------------------------------------------------------- the days

        private static IEnumerable<Day> Days()
        {
            const BeatStyle thought = BeatStyle.Thought, dialogue = BeatStyle.Dialogue, note = BeatStyle.Note;
            Beat Start(BeatStyle style, string text, string speaker = "") => new() { Trigger = BeatTrigger.LevelStart, Style = style, Text = text, Speaker = speaker };
            Beat After(int step, BeatStyle style, string text, string speaker = "") => new() { Trigger = BeatTrigger.AfterStep, Step = step, Style = style, Text = text, Speaker = speaker };
            Beat End(BeatStyle style, string text, string speaker = "", BeatCondition condition = BeatCondition.Always) =>
                new() { Trigger = BeatTrigger.LevelEnd, Style = style, Text = text, Speaker = speaker, Condition = condition };

            yield return new Day
            {
                Number = 1, Calendar = "Monday.", BadDrink = BadDrink, TooCold = TooCold,
                Tutorial = true, PulseRecipe = true, Recipe = RecipeInstant,
                Beats = new[]
                {
                    Start(thought, "6:40. Snoozed twice. Still tired."),
                    After(0, thought, "Instant again. Whatever. At least it's hot."),
                    End(thought, "Huh. Someone across the yard is up this early too."),
                },
            };
            yield return new Day
            {
                Number = 5, Calendar = "Raining. Again.", BadDrink = BadDrink, TooCold = TooCold,
                PulseRecipe = true, Recipe = RecipeInstant + RecipePourOver,
                Beats = new[]
                {
                    Start(thought, "Got real beans on the way home. Felt like it."),
                    After(2, thought, "Okay, that smells way better than instant."), // after grinding (step 1 puts the V60 on)
                    End(thought, "She waved at me. I waved back... with a mug. Very cool. Super smooth."),
                },
            };
            yield return new Day
            {
                Number = 10, Calendar = "Sunday. No work.", BadDrink = BadDrink, TooCold = TooCold,
                Recipe = RecipeInstant + RecipePourOver + RecipeMilk,
                Beats = new[]
                {
                    Start(thought, "Sunday. Nowhere to be."),
                    After(4, dialogue, "Hi! Sorry, um... I'm Mira, from across the yard. I ran out of coffee. Could I borrow a little?", "Mira"), // after her knock
                    After(5, dialogue, "Oh, and I like it sweet. Cinnamon and honey, if you have them?", "Mira"), // after taking her jar
                    End(dialogue, "Okay, wow. That's really good. Thank you!", "Mira", BeatCondition.GoodResult),
                    End(dialogue, "Mm. Thanks... it's nice.", "Mira", BeatCondition.PoorResult),
                    End(thought, "I think I've been smiling for ten minutes."),
                },
            };
            yield return new Day
            {
                Number = 20, Calendar = "Minus three tonight.",
                BadDrink = "Ugh. That won't warm anyone up.", TooCold = TooCold,
                Recipe = RecipeInstant + RecipePourOver + RecipeMilk + RecipeTwoMugs,
                // "My heating broke :(" is read from her window (ReadHerWindow, step 0), not shown as a line.
                Beats = new[]
                {
                    After(0, note, "hang on. making coffee."), // his reply, after reading her window
                    After(7, thought, "Two mugs. Hers first."), // after his cup fills beside hers
                    End(thought, "Three weeks ago I could barely make coffee for myself."),
                },
            };
            yield return new Day
            {
                Number = 30, Calendar = "First snow.", BadDrink = "Hmm. Not quite how she likes it.", TooCold = "", // Day 30 can't fail
                Recipe = RecipeInstant + RecipePourOver + RecipeMilk + RecipeTwoMugs, // never her recipe: Day 30 is from memory
                Beats = new[]
                {
                    Start(dialogue, "It's snowing!! Make me the usual?", "Mira"),
                    After(2, thought, "Cinnamon, honey. Don't even have to think about it."),
                    End(thought, "Autumn's over."),
                    End(thought, "It's freezing outside. Weird. I'm not cold at all."),
                },
            };
        }

        private static readonly (int afterDay, string from, string text)[] FridgeNotes =
        {
            (1, "", "BUY REAL COFFEE."),
            (5, "", "Next time maybe say hi. Not just wave."),
            (10, "M", "Thanks for saving my Sunday!\n(cinnamon + honey, don't forget)"),
            (20, "M", "Heating's back. Thank you for last night."),
            (30, "", "First snow. Two mugs on the sill."),
        };

        /// <summary>Credits as in CREDITS.md (art, music, decor, fonts) plus the sound sources named by the team.</summary>
        private static readonly (BeatStyle style, string text)[] Ending =
        {
            (BeatStyle.Note, "Until the First Snow"),
            (BeatStyle.Thought, "A cozy lofi story about coffee and love."),
            (BeatStyle.Note, "Made in 72 hours for Mini Jam 221: Autumn.\nLimitation: Staying Warm."),
            (BeatStyle.Note, "Design, code & art: Begench\nMusic: Purrple Cat (CC BY-SA 4.0)\nRain & wind: Helton Yan (CC BY 4.0)"),
            (BeatStyle.Note, "Sound effects: Polarsound Productions, MorphCore, Kenney (CC0), Freesound contributors\nDecor: ODDBLOT\nFonts: Amaranth, Playpen Sans (SIL OFL)"),
            (BeatStyle.Thought, "Thanks for playing. Stay warm."),
        };

        // ---------------------------------------------------------------- apply

        [MenuItem("Courtyard/Setup/Apply Game Texts")]
        public static void Apply()
        {
            var steps = P.EnsureAll();
            LevelDataBuilder.PatchSteps(steps);
            ApplyHints(steps);
            ApplyDays();
            ApplyNotes();
            ApplyEnding();
            ApplyFixedLabels();
            AssetDatabase.SaveAssets();
            CheckRecipe();
            TextsExporter.Export();
        }

        private static void ApplyHints(IReadOnlyDictionary<string, MinigameStep> steps)
        {
            foreach (var (prefab, hint, always) in Hints)
            {
                if (!steps.ContainsKey(prefab))
                {
                    Debug.LogWarning($"GameTexts: no step prefab '{prefab}'.");
                    continue;
                }

                Patch(P.Folder + prefab + ".prefab", root =>
                {
                    var step = root.GetComponent<MinigameStep>();
                    Wire(step, "hintText", hint);
                    Wire(step, "alwaysShowHint", always);
                    if (prefab == P.HerDrinkFromMemory) Wire(step, "wrongTasteText", "Not quite her usual...");
                    if (prefab == P.ReadHerWindow) Wire(step, "noteText", "my heating died :(");
                    NoEllipsis(step);
                });
            }
        }

        private static void ApplyDays()
        {
            foreach (var day in Days())
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{LevelsFolder}Day_{day.Number:00}.asset");
                if (level == null)
                {
                    Debug.LogWarning($"GameTexts: Day_{day.Number:00}.asset missing.");
                    continue;
                }

                Edit(level, so =>
                {
                    so.FindProperty("calendarLine").stringValue = day.Calendar;
                    so.FindProperty("coldDrinkText").stringValue = day.BadDrink;
                    so.FindProperty("failText").stringValue = day.TooCold;
                    so.FindProperty("tutorialHints").boolValue = day.Tutorial;
                    so.FindProperty("pulseRecipeCard").boolValue = day.PulseRecipe;
                    so.FindProperty("recipeCardText").stringValue = day.Recipe;
                    so.FindProperty("remakeText").stringValue = "It's bad. Let's remake it.";

                    // Beats are updated in place (same order), so per-beat extras like "sets Mira's window" stay.
                    var beats = so.FindProperty("storyBeats");
                    if (beats.arraySize != day.Beats.Length)
                        Debug.LogWarning($"GameTexts: Day {day.Number} has {beats.arraySize} beats, the table {day.Beats.Length} — resized.");
                    beats.arraySize = day.Beats.Length;
                    for (var i = 0; i < day.Beats.Length; i++) WriteBeat(beats.GetArrayElementAtIndex(i), day.Beats[i]);
                });
            }
        }

        private static void WriteBeat(SerializedProperty beat, Beat source)
        {
            beat.FindPropertyRelative("trigger").enumValueIndex = (int)source.Trigger;
            beat.FindPropertyRelative("stepIndex").intValue = source.Step;
            beat.FindPropertyRelative("condition").enumValueIndex = (int)source.Condition;
            beat.FindPropertyRelative("style").enumValueIndex = (int)source.Style;
            beat.FindPropertyRelative("speaker").stringValue = source.Speaker;
            beat.FindPropertyRelative("text").stringValue = source.Text;
        }

        private static void ApplyNotes()
        {
            var book = AssetDatabase.LoadAssetAtPath<NotesBook>(NotesPath);
            if (book == null) return;
            Edit(book, so =>
            {
                var notes = so.FindProperty("notes");
                notes.arraySize = FridgeNotes.Length;
                for (var i = 0; i < FridgeNotes.Length; i++)
                {
                    var note = notes.GetArrayElementAtIndex(i);
                    note.FindPropertyRelative("afterDay").intValue = FridgeNotes[i].afterDay;
                    note.FindPropertyRelative("from").stringValue = FridgeNotes[i].from;
                    note.FindPropertyRelative("text").stringValue = FridgeNotes[i].text;
                }
            });
        }

        private static void ApplyEnding()
        {
            var ending = AssetDatabase.LoadAssetAtPath<StorySequence>(EndingPath);
            if (ending == null) return;
            Edit(ending, so =>
            {
                var beats = so.FindProperty("beats");
                beats.arraySize = Ending.Length;
                for (var i = 0; i < Ending.Length; i++)
                {
                    var beat = beats.GetArrayElementAtIndex(i);
                    WriteBeat(beat, new Beat { Trigger = BeatTrigger.LevelEnd, Style = Ending[i].style, Text = Ending[i].text });
                    beat.FindPropertyRelative("setsMiraWindow").boolValue = false;
                }
            });
        }

        /// <summary>Labels written into scenes and UI prefabs: "click to continue", "click to close", the title prompt.</summary>
        private static void ApplyFixedLabels()
        {
            var labels = new Dictionary<string, string>
            {
                ["ContinueHint"] = "click to continue",
                ["CloseHint"] = "click to close",
                ["WakePrompt"] = "click to start",
            };

            void Relabel(GameObject root)
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (!labels.TryGetValue(text.name, out var value) || text.text == value) continue;
                    text.text = value;
                    EditorUtility.SetDirty(text);
                }

                foreach (var fx in root.GetComponentsInChildren<Kitchen.FeedbackFx>(true)) NoEllipsis(fx);
                foreach (var runner in root.GetComponentsInChildren<LevelRunner>(true))
                    Wire(runner, "newNoteHint", "New note on the fridge. Click it to read.");
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/UI" }))
                Patch(AssetDatabase.GUIDToAssetPath(guid), Relabel);
            foreach (var path in BuildScript.Scenes)
                EditScene(path, scene =>
                {
                    foreach (var root in scene.GetRootGameObjects()) Relabel(root);
                });
        }

        /// <summary>Mira's usual must be exactly cinnamon + honey wherever her drink is judged (Day 10 guess, Day 30 memory).</summary>
        private static void CheckRecipe()
        {
            foreach (var name in new[] { P.GuessHerDrink, P.HerDrinkFromMemory })
            {
                var step = AssetDatabase.LoadAssetAtPath<GameObject>(P.Folder + name + ".prefab")?.GetComponent<PickToppingsStep>();
                var recipe = step != null ? new SerializedObject(step).FindProperty("recipe").objectReferenceValue as Recipe : null;
                var toppings = recipe != null ? recipe.Toppings.Distinct().OrderBy(t => t).ToArray() : System.Array.Empty<Topping>();
                var ok = toppings.SequenceEqual(new[] { Topping.Cinnamon, Topping.Honey }.OrderBy(t => t));
                if (ok) Debug.Log($"GameTexts: {name} checks cinnamon + honey ({recipe.name}).");
                else Debug.LogError($"GameTexts: {name} does not check exactly cinnamon + honey.");
            }
        }

        /// <summary>
        /// The game font (Amaranth) has no "…" glyph — it would show in a fallback font. Every text field on the
        /// component uses three dots instead (the handwritten notes font has it, but notes don't go through here).
        /// </summary>
        private static void NoEllipsis(Object component)
        {
            var so = new SerializedObject(component);
            var property = so.GetIterator();
            var changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.String || !property.stringValue.Contains("…")) continue;
                property.stringValue = property.stringValue.Replace("…", "...");
                changed = true;
            }

            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Edits a prefab in place (keeps its file IDs).</summary>
        private static void Patch(string path, System.Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
