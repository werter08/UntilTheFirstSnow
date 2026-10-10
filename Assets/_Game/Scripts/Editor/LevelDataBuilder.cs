using System.Collections.Generic;
using Courtyard.Core;
using Courtyard.Minigames;
using Courtyard.Story;
using UnityEditor;
using UnityEngine;
using static Courtyard.Editor.BuilderKit;
using P = Courtyard.Editor.MinigamePrefabBuilder;

namespace Courtyard.Editor
{
    /// <summary>
    /// Creates the recipe, the five LevelDefinition assets and the ending sequence with the story from CLAUDE.md.
    /// Existing assets are kept, so tuning done in the Inspector is never overwritten.
    /// </summary>
    internal static class LevelDataBuilder
    {
        private const string LevelsFolder = "Assets/_Game/Data/Levels/";
        private const string MiraUsualPath = "Assets/_Game/Data/Recipes/Mira_Usual.asset";
        private const string EndingPath = "Assets/_Game/Data/Story/Ending.asset";

        private sealed class Beat
        {
            public BeatTrigger Trigger;
            public int Step;
            public BeatStyle Style;
            public string Text;
            public string Speaker = "";
            public BeatCondition Condition = BeatCondition.Always;
        }

        private sealed class Spec
        {
            public int Day;
            public string Clock;
            public TimeOfDay Time;
            public Weather Weather;
            public TreeStage Tree;
            public MiraWindowState Window;
            public LevelPalette Palette;
            public KitchenProps Props;
            public string[] Steps;
            public Beat[] Beats;
            public bool TwoMeters;
            public float Drain;
            public float OutroSeconds;
            public (string icon, BeatCondition condition)[] Drinks = System.Array.Empty<(string, BeatCondition)>();
            public MugStyle[] Rack = System.Array.Empty<MugStyle>();
        }

        public static Recipe EnsureMiraUsual() =>
            LoadOrCreateAsset<Recipe>(MiraUsualPath, so =>
            {
                so.FindProperty("displayName").stringValue = "Mira's usual";
                var toppings = so.FindProperty("toppings");
                toppings.arraySize = 2;
                toppings.GetArrayElementAtIndex(0).enumValueIndex = (int)Topping.Cinnamon;
                toppings.GetArrayElementAtIndex(1).enumValueIndex = (int)Topping.Honey;
            });

        public static List<LevelDefinition> BuildLevels(IReadOnlyDictionary<string, MinigameStep> steps, bool overwrite)
        {
            var levels = new List<LevelDefinition>();
            foreach (var spec in Specs())
            {
                var path = $"{LevelsFolder}Day_{spec.Day:00}.asset";
                var level = LoadOrCreateAsset<LevelDefinition>(path, so => Fill(so, spec, steps), overwrite);
                PatchM1Defaults(level, spec);
                levels.Add(level);
            }

            return levels;
        }

        public static StorySequence BuildEnding() =>
            LoadOrCreateAsset<StorySequence>(EndingPath, so =>
                AddBeat(so.FindProperty("beats"), new Beat { Trigger = BeatTrigger.LevelEnd, Style = BeatStyle.Note, Text = "Thank you for playing." }));

        private static void Fill(SerializedObject so, Spec spec, IReadOnlyDictionary<string, MinigameStep> steps)
        {
            so.FindProperty("dayNumber").intValue = spec.Day;
            so.FindProperty("clockTime").stringValue = spec.Clock;
            so.FindProperty("timeOfDay").enumValueIndex = (int)spec.Time;
            so.FindProperty("weather").enumValueIndex = (int)spec.Weather;
            so.FindProperty("treeStage").enumValueIndex = (int)spec.Tree;
            so.FindProperty("miraWindow").enumValueIndex = (int)spec.Window;
            WritePalette(so.FindProperty("palette"), spec.Palette);
            WriteProps(so.FindProperty("props"), spec.Props);

            var stepList = so.FindProperty("steps");
            stepList.arraySize = spec.Steps.Length;
            for (var i = 0; i < spec.Steps.Length; i++)
                stepList.GetArrayElementAtIndex(i).objectReferenceValue = steps[spec.Steps[i]];

            var beats = so.FindProperty("storyBeats");
            beats.arraySize = 0;
            foreach (var beat in spec.Beats) AddBeat(beats, beat);

            so.FindProperty("twoMeters").boolValue = spec.TwoMeters;
            so.FindProperty("startWarmth").floatValue = 70f;
            so.FindProperty("drainPerSecond").floatValue = spec.Drain;
            so.FindProperty("coldMultiplier").floatValue = 1f;
            so.FindProperty("warmthPerStep").floatValue = 15f;
            so.FindProperty("drinkWarmthBonus").floatValue = 25f;
            so.FindProperty("outroHoldSeconds").floatValue = spec.OutroSeconds;
            WriteDrinks(so.FindProperty("drinkResults"), spec.Drinks);
        }

        /// <summary>
        /// Applies the art manifest's per-day mapping (mug, sill mugs, sleeve, served drinks) to existing level assets
        /// without touching tuning or story.
        /// </summary>
        public static void PatchArtManifest()
        {
            foreach (var spec in Specs())
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{LevelsFolder}Day_{spec.Day:00}.asset");
                if (level == null) continue;

                Edit(level, so =>
                {
                    var props = so.FindProperty("props");
                    props.FindPropertyRelative("brewMug").enumValueIndex = (int)spec.Props.brewMug;
                    props.FindPropertyRelative("mugsOnSill").boolValue = spec.Props.mugsOnSill;
                    props.FindPropertyRelative("sweater").enumValueIndex = (int)spec.Props.sweater;
                    props.FindPropertyRelative("lampOff").boolValue = spec.Props.lampOff;
                    var rack = props.FindPropertyRelative("rackMugs");
                    rack.arraySize = spec.Rack.Length;
                    for (var i = 0; i < spec.Rack.Length; i++) rack.GetArrayElementAtIndex(i).enumValueIndex = (int)spec.Rack[i];
                    WriteDrinks(so.FindProperty("drinkResults"), spec.Drinks);
                    var walkers = so.FindProperty("outroWalkers");
                    var names = spec.Day == 20 ? new[] { "mug_his", "mug_hers" } : System.Array.Empty<string>();
                    walkers.arraySize = names.Length;
                    for (var i = 0; i < names.Length; i++)
                        walkers.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/Props/{names[i]}.png");
                    so.FindProperty("finale").boolValue = spec.Day == 30;
                });
            }

            AssetDatabase.SaveAssets();
        }

        private static void WriteDrinks(SerializedProperty list, (string icon, BeatCondition condition)[] drinks)
        {
            list.arraySize = drinks.Length;
            for (var i = 0; i < drinks.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("icon").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/Drinks/{drinks[i].icon}.png");
                element.FindPropertyRelative("condition").enumValueIndex = (int)drinks[i].condition;
            }
        }

        /// <summary>Day_01 was created in M1 before palettes and props existed; fill those in once if still at defaults.</summary>
        private static void PatchM1Defaults(LevelDefinition level, Spec spec)
        {
            if (level.Palette.globalLightColor != Color.white || level.Palette.lampColor != Color.white) return;

            Edit(level, so =>
            {
                WritePalette(so.FindProperty("palette"), spec.Palette);
                WriteProps(so.FindProperty("props"), spec.Props);
            });
        }

        private static void WritePalette(SerializedProperty palette, LevelPalette value)
        {
            palette.FindPropertyRelative("globalLightColor").colorValue = value.globalLightColor;
            palette.FindPropertyRelative("globalLightIntensity").floatValue = value.globalLightIntensity;
            palette.FindPropertyRelative("lampColor").colorValue = value.lampColor;
            palette.FindPropertyRelative("lampIntensity").floatValue = value.lampIntensity;
            palette.FindPropertyRelative("miraWindowColor").colorValue = value.miraWindowColor;
            palette.FindPropertyRelative("vignetteTint").colorValue = value.vignetteTint;
        }

        private static void WriteProps(SerializedProperty props, KitchenProps value)
        {
            props.FindPropertyRelative("beans").boolValue = value.beans;
            props.FindPropertyRelative("grinder").boolValue = value.grinder;
            props.FindPropertyRelative("milk").boolValue = value.milk;
            props.FindPropertyRelative("cinnamon").boolValue = value.cinnamon;
            props.FindPropertyRelative("herMug").boolValue = value.herMug;
            props.FindPropertyRelative("toppings").boolValue = value.toppings;
            props.FindPropertyRelative("brewMug").enumValueIndex = (int)value.brewMug;
            props.FindPropertyRelative("mugsOnSill").boolValue = value.mugsOnSill;
            props.FindPropertyRelative("scarf").boolValue = value.scarf;
            props.FindPropertyRelative("blanket").boolValue = value.blanket;
            props.FindPropertyRelative("plantStage").enumValueIndex = (int)value.plantStage;
            props.FindPropertyRelative("fridgeNotes").intValue = value.fridgeNotes;
            props.FindPropertyRelative("sweater").enumValueIndex = (int)value.sweater;
            props.FindPropertyRelative("lampOff").boolValue = value.lampOff;
        }

        private static void AddBeat(SerializedProperty list, Beat beat)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            var element = list.GetArrayElementAtIndex(list.arraySize - 1);
            element.FindPropertyRelative("trigger").enumValueIndex = (int)beat.Trigger;
            element.FindPropertyRelative("stepIndex").intValue = beat.Step;
            element.FindPropertyRelative("condition").enumValueIndex = (int)beat.Condition;
            element.FindPropertyRelative("style").enumValueIndex = (int)beat.Style;
            element.FindPropertyRelative("speaker").stringValue = beat.Speaker;
            element.FindPropertyRelative("text").stringValue = beat.Text;
        }

        private static Beat Start(BeatStyle style, string text, string speaker = "") =>
            new() { Trigger = BeatTrigger.LevelStart, Style = style, Text = text, Speaker = speaker };

        private static Beat After(int step, BeatStyle style, string text, string speaker = "") =>
            new() { Trigger = BeatTrigger.AfterStep, Step = step, Style = style, Text = text, Speaker = speaker };

        private static Beat End(BeatStyle style, string text, string speaker = "", BeatCondition condition = BeatCondition.Always) =>
            new() { Trigger = BeatTrigger.LevelEnd, Style = style, Text = text, Speaker = speaker, Condition = condition };

        private static LevelPalette Palette(Color global, float globalIntensity, Color lamp, float lampIntensity) => new()
        {
            globalLightColor = global,
            globalLightIntensity = globalIntensity,
            lampColor = lamp,
            lampIntensity = lampIntensity,
            miraWindowColor = new Color(1f, 0.85f, 0.55f),
            vignetteTint = new Color(0f, 0f, 0f, 0f),
        };

        // Drain values are tuned so an average player (~15 s per step, quality ~0.6) finishes every day;
        // BalanceTests guards this. CLAUDE.md's first-guess numbers (1.5 … 4 /s) emptied the meter mid-level.
        private static IEnumerable<Spec> Specs()
        {
            const BeatStyle thought = BeatStyle.Thought;
            const BeatStyle note = BeatStyle.Note;
            const BeatStyle dialogue = BeatStyle.Dialogue;
            const string mira = "Mira";

            yield return new Spec
            {
                Day = 1, Clock = "06:40", Time = TimeOfDay.Morning, Weather = Weather.LightRain, Tree = TreeStage.GreenYellow,
                Window = MiraWindowState.LitWarmSilhouette,
                Palette = Palette(new Color(0.78f, 0.82f, 0.9f), 1f, new Color(0.85f, 0.92f, 1f), 0.6f),
                Props = new KitchenProps { plantStage = PlantStage.Wilted, lampOff = true },
                Steps = new[] { P.KettleBoil, P.InstantCoffee },
                Beats = new[]
                {
                    Start(thought, "6:40. Again."),
                    After(0, thought, "Instant coffee. It tastes like Monday."),
                    End(thought, "…Someone across the courtyard is already awake."),
                },
                Drain = 0.8f,
                Drinks = new[] { ("drink_black", BeatCondition.Always) }, // drink_instant is the empty-mug cap
            };

            yield return new Spec
            {
                Day = 5, Clock = "19:30", Time = TimeOfDay.Evening, Weather = Weather.HeavyRain, Tree = TreeStage.Orange,
                Window = MiraWindowState.LitWarmSilhouette,
                Palette = Palette(new Color(0.7f, 0.68f, 0.75f), 0.9f, new Color(1f, 0.85f, 0.65f), 0.6f),
                Props = new KitchenProps { beans = true, grinder = true, brewMug = MugStyle.His, plantStage = PlantStage.Wilted },
                Rack = new[] { MugStyle.Chipped },
                Steps = new[] { P.KettleBoil, P.GrindBeans, P.PourOver, P.WaveBack },
                Beats = new[]
                {
                    Start(thought, "Bought real beans today. No idea why."),
                    After(1, thought, "Grind. Pour slowly. Breathe."),
                    End(thought, "She waved. I waved back with my mug. Smooth."),
                },
                Drain = 1f,
                Drinks = new[] { ("drink_pourover", BeatCondition.Always) },
            };

            yield return new Spec
            {
                Day = 10, Clock = "09:15", Time = TimeOfDay.Morning, Weather = Weather.Leaves, Tree = TreeStage.Golden,
                Window = MiraWindowState.LitWarm,
                Palette = Palette(new Color(1f, 0.92f, 0.78f), 1.1f, new Color(1f, 0.82f, 0.55f), 0.5f),
                Props = new KitchenProps { beans = true, grinder = true, milk = true, cinnamon = true, toppings = true, brewMug = MugStyle.Hers, plantStage = PlantStage.Recovering, fridgeNotes = 1, sweater = Sweater.Grey },
                Rack = new[] { MugStyle.Chipped, MugStyle.His },
                // MiraVisit (knock at the door) sits between PourOver and SteamMilk so her lines follow the brief's order.
                Steps = new[] { P.KettleBoil, P.GrindBeans, P.PourOver, P.MiraVisit, P.SteamMilk, P.GuessHerDrink },
                Beats = new[]
                {
                    Start(thought, "Sunday. The light is different today."),
                    After(3, dialogue, "Hi… I'm Mira, from across. I ran out of coffee. Could I borrow some?", mira),
                    After(4, dialogue, "Something sweet… with cinnamon? Surprise me.", mira),
                    End(dialogue, "Okay. That's the best cup I've had this month.", mira, BeatCondition.GoodResult),
                    End(dialogue, "It's… nice. Thank you.", mira, BeatCondition.PoorResult),
                    End(thought, "I think I'm smiling. Weird feeling."),
                },
                Drain = 1.2f,
                Drinks = new[] { ("drink_latte", BeatCondition.Always), ("drink_cinnamon", BeatCondition.GoodResult) },
            };

            yield return new Spec
            {
                Day = 20, Clock = "23:40", Time = TimeOfDay.Night, Weather = Weather.Wind, Tree = TreeStage.HalfBare,
                Window = MiraWindowState.NoteOnGlass,
                Palette = Palette(new Color(0.45f, 0.5f, 0.75f), 0.7f, new Color(1f, 0.75f, 0.45f), 1f),
                Props = new KitchenProps { beans = true, grinder = true, milk = true, cinnamon = true, herMug = true, brewMug = MugStyle.Yellow, plantStage = PlantStage.Healthy, fridgeNotes = 2, sweater = Sweater.Beige },
                Rack = new[] { MugStyle.Chipped, MugStyle.His, MugStyle.Hers }, // ChooseMug picks hers from here
                Steps = new[] { P.KettleBoil, P.GrindBeans, P.PourOver, P.SteamMilk, P.ChooseMug },
                Beats = new[]
                {
                    // The brief's 🥶 emoji isn't in the default font; add a hand-drawn TMP sprite in M3.
                    Start(note, "Heating broke."),
                    After(0, note, "Hold on."),
                    After(3, thought, "Two cups. Hers first."),
                    End(thought, "Funny. A month ago I couldn't keep myself warm."),
                },
                TwoMeters = true,
                Drain = 1.4f,
                Drinks = new[] { ("drink_latte", BeatCondition.Always), ("drink_latte", BeatCondition.Always) },
                OutroSeconds = 3f, // fade to black, footsteps across the courtyard with two mugs
            };

            yield return new Spec
            {
                Day = 30, Clock = "17:00", Time = TimeOfDay.Evening, Weather = Weather.Snow, Tree = TreeStage.BareSnow,
                Window = MiraWindowState.Empty,
                Palette = Palette(new Color(1f, 0.85f, 0.7f), 0.95f, new Color(1f, 0.72f, 0.42f), 1.1f),
                Props = new KitchenProps { beans = true, grinder = true, milk = true, cinnamon = true, toppings = true, brewMug = MugStyle.Green, mugsOnSill = true, herMug = true, scarf = true, blanket = true, plantStage = PlantStage.Blooming, fridgeNotes = 3, sweater = Sweater.Beige },
                Rack = new[] { MugStyle.Chipped, MugStyle.His, MugStyle.Hers, MugStyle.Yellow },
                Steps = new[] { P.KettleBoil, P.SteamMilk, P.HerDrinkFromMemory },
                Beats = new[]
                {
                    Start(dialogue, "It's snowing! Make me the usual?", mira),
                    After(2, thought, "I don't need the recipe anymore."),
                    End(thought, "Autumn's over."),
                    End(thought, "For the first time in a long while — I'm warm."),
                },
                Drain = 0f,
                Drinks = new[] { ("drink_latte_heart", BeatCondition.Always) },
            };
        }
    }
}
