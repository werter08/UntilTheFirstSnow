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

        private const string BadDrink = "Cold and bitter… that didn't help at all.";
        private const string TooCold = "Too cold… let's try that again.";

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
            (P.InstantCoffee, "Two spoons of coffee into the cup. Then pour water to the line.", false),
            (P.GrindBeans, "Turn the handle in circles. Stop in the green.", false),
            (P.PourOver, "Pour in slow circles. Keep the flow in the green. Fill to the line.", false),
            (P.WaveBack, "She's waving. Click your mug to wave back.", true),
            (P.MiraVisit, "", false),
            (P.TakeHerCup, "Take the jar from her.", true),
            (P.SteamMilk, "Hold to steam the milk. Let go in the green.", false),
            (P.GuessHerDrink, "Add two spices to her cup, one at a time. Then click the cup.", true),
            (P.ChooseMug, "First, her mug. Which one is hers?", true),
            (P.HerDrinkFromMemory, "Make her usual. You know it by now.", true),
            (P.AutoFillHisMug, "", false),
            (P.PourWater, "Pour the hot water to the line.", false),
            (P.ReadHerWindow, "Something's on her window. Tap it.", true),
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
                Number = 1, Calendar = "Day 1. Same as yesterday.", BadDrink = BadDrink, TooCold = TooCold,
                Tutorial = true, PulseRecipe = true, Recipe = RecipeInstant,
                Beats = new[]
                {
                    Start(thought, "6:40. Again."),
                    After(0, thought, "Instant coffee. Tastes like Monday."),
                    End(thought, "…Someone across the courtyard is already awake."),
                },
            };
            yield return new Day
            {
                Number = 5, Calendar = "Rain again. I bought real beans on the way home.", BadDrink = BadDrink, TooCold = TooCold,
                PulseRecipe = true, Recipe = RecipeInstant + RecipePourOver,
                Beats = new[]
                {
                    Start(thought, "Real beans. No idea why I bought them."),
                    After(1, thought, "Grind. Pour slowly. Breathe."),
                    End(thought, "She waved. I waved back with a mug. Smooth."),
                },
            };
            yield return new Day
            {
                Number = 10, Calendar = "Sunday. No rush today.", BadDrink = BadDrink, TooCold = TooCold,
                Recipe = RecipeInstant + RecipePourOver + RecipeMilk,
                Beats = new[]
                {
                    Start(thought, "Sunday. Even the light looks softer."),
                    After(3, dialogue, "Hi… I'm Mira, from across the yard. I ran out of coffee. Could I borrow some?", "Mira"),
                    After(4, dialogue, "Something sweet, please. Cinnamon… and maybe honey?", "Mira"),
                    End(dialogue, "Wow. Best cup I've had all month.", "Mira", BeatCondition.GoodResult),
                    End(dialogue, "It's… nice. Thank you, really.", "Mira", BeatCondition.PoorResult),
                    End(thought, "I think I'm smiling. Strange feeling."),
                },
            };
            yield return new Day
            {
                Number = 20, Calendar = "Freezing tonight. Her window is still lit.",
                BadDrink = "Cold and bitter… that won't warm anyone.", TooCold = TooCold,
                Recipe = RecipeInstant + RecipePourOver + RecipeMilk + RecipeTwoMugs,
                // "My heating broke :(" is read from her window (ReadHerWindow, step 0), not shown as a line.
                Beats = new[]
                {
                    After(0, note, "Hold on. Coffee's coming."),
                    After(6, thought, "Two cups tonight. Hers first."), // after his cup fills beside hers
                    End(thought, "Funny. A few weeks ago I couldn't even keep myself warm."),
                },
            };
            yield return new Day
            {
                Number = 30, Calendar = "First snow.", BadDrink = "Hmm. Not quite her usual.", TooCold = "", // Day 30 can't fail
                Recipe = RecipeInstant + RecipePourOver + RecipeMilk + RecipeTwoMugs, // never her recipe: Day 30 is from memory
                Beats = new[]
                {
                    Start(dialogue, "It's snowing! Make me the usual?", "Mira"),
                    After(2, thought, "I don't need a recipe for this one."),
                    End(thought, "Autumn's over."),
                    End(thought, "And for the first time in a long while… I'm warm."),
                },
            };
        }

        private static readonly (int afterDay, string from, string text)[] FridgeNotes =
        {
            (1, "", "Buy real coffee. Seriously."),
            (5, "", "The girl across the yard waved. I waved back. With a mug."),
            (10, "Mira", "Thank you for the coffee! My usual: cinnamon + honey ♥"),
            (20, "Mira", "Heating's fixed. Thanks for the coffee at midnight."),
            (30, "", "First snow. Two mugs on the sill."),
        };

        /// <summary>Credits as in CREDITS.md (art, music, decor, fonts) plus the sound sources named by the team.</summary>
        private static readonly (BeatStyle style, string text)[] Ending =
        {
            (BeatStyle.Note, "Until the First Snow"),
            (BeatStyle.Thought, "A cozy lofi story about coffee and love."),
            (BeatStyle.Note, "Made in 72 hours for Mini Jam 221: Autumn.\nLimitation: Staying Warm."),
            (BeatStyle.Note, "Design, code & art: Begench\nMusic: Purrple Cat (CC BY-SA 4.0)\nRain & wind: Helton Yan (CC BY 4.0)"),
            (BeatStyle.Note, "Sound effects: Polarsound Productions, MorphCore, Kenney (CC0)\nDecor: ODDBLOT\nFonts: Amaranth, Playpen Sans (OFL)"),
            (BeatStyle.Thought, "Thank you for playing. Stay warm."),
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
                    if (prefab == P.HerDrinkFromMemory) Wire(step, "wrongTasteText", "Not quite her usual…");
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
