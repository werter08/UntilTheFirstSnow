using System.Collections.Generic;
using System.Linq;
using Courtyard.Minigames;
using Courtyard.Story;
using UnityEngine;

namespace Courtyard.Core
{
    /// <summary>All content and tuning for one day. Tuning a level means editing this asset, not code.</summary>
    [CreateAssetMenu(menuName = "Courtyard/Level", fileName = "Day_XX")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public static readonly int[] ValidDays = { 1, 5, 10, 20, 30 };

        [Header("Calendar")]
        [SerializeField] private int dayNumber = 1;
        [SerializeField] private string clockTime = "06:40";
        [SerializeField] private TimeOfDay timeOfDay;
        [SerializeField] private Weather weather;

        [Header("Kitchen")]
        [SerializeField] private LevelPalette palette = LevelPalette.Default;
        [SerializeField] private TreeStage treeStage;
        [Tooltip("Drawn chestnut crown for this day (tree_1_green … tree_5_snow); empty = coloured placeholder.")]
        [SerializeField] private Sprite treeCrownSprite;
        [Tooltip("Solid sky colour behind the opposite building (alpha 0 = time-of-day fallback).")]
        [SerializeField] private Color skyColor = Color.clear;
        [SerializeField] private MiraWindowState miraWindow;
        [SerializeField] private KitchenProps props;

        [Header("Gameplay")]
        [Tooltip("Minigame step prefabs, played in order.")]
        [SerializeField] private List<MinigameStep> steps = new();
        [SerializeField] private List<StoryBeat> storyBeats = new();
        [SerializeField] private bool twoMeters;

        [Header("Warmth")]
        [SerializeField, Range(0f, WarmthMeter.Max)] private float startWarmth = 70f;
        [Tooltip("0 = meter never drains and is shown full.")]
        [SerializeField, Min(0f)] private float drainPerSecond = 1.5f;
        [SerializeField, Min(0f)] private float coldMultiplier = 1f;
        [SerializeField, Min(0f)] private float warmthPerStep = 12f;
        [SerializeField, Min(0f)] private float drinkWarmthBonus = 25f;
        [Tooltip("Warm together (Day 30): the meter is full and never changes.")]
        [SerializeField] private bool warmthLocked;
        [Tooltip("Warmth never drops below this (Day 1, the tutorial, can't be lost: 15).")]
        [SerializeField, Range(0f, WarmthMeter.Max)] private float minWarmth;
        [Tooltip("Step quality at or above this counts as a good result for conditional beats.")]
        [SerializeField, Range(0f, 1f)] private float goodResultThreshold = 0.7f;
        [Header("Bad drinks: the cold wins")]
        [Tooltip("A scored step below this quality takes warmth away instead of giving it.")]
        [SerializeField, Range(0f, 1f)] private float badResultThreshold = 0.4f;
        [Tooltip("Warmth lost by a step at quality 0 (half of it just below the threshold).")]
        [SerializeField, Min(0f)] private float badStepChill = 20f;
        [Tooltip("Warmth lost when the served drink (average quality) is below the bad threshold.")]
        [SerializeField, Min(0f)] private float badDrinkChill = 35f;
        [Tooltip("The calendar page's hopeful line for this day (click the calendar).")]
        [SerializeField, TextArea] private string calendarLine;
        [Header("Hints and the recipe card")]
        [Tooltip("Tutorial day: every step's hint stays on screen for the whole step.")]
        [SerializeField] private bool tutorialHints;
        [Tooltip("The recipe card on the wall pulses once when the day starts.")]
        [SerializeField] private bool pulseRecipeCard;
        [Tooltip("What the recipe card says today (it grows over the autumn). Never Mira's recipe.")]
        [SerializeField, TextArea(3, 12)] private string recipeCardText;
        [SerializeField, TextArea] private string coldDrinkText = "Cold, bitter coffee… the cold got in. Let's try again.";
        [SerializeField, TextArea] private string failText = "Too cold… let's try again.";

        [Header("Served drink")]
        [Tooltip("Icons that pop up above the mug when the drink is served.")]
        [SerializeField] private List<DrinkResult> drinkResults = new();

        [Header("Audio")]
        [Tooltip("Level music (none = silence; Day 1 is rain and the clock). Crossfades between days.")]
        [SerializeField] private AudioClip music;
        [SerializeField] private AudioClip ambience;
        [Tooltip("Ambience loudness 0..1 (the room tone always plays underneath).")]
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 1f;

        [Header("Outro")]
        [Tooltip("Played over black after the last line (Day 20: footsteps across the courtyard).")]
        [SerializeField] private AudioClip outroClip;
        [SerializeField, Min(0f)] private float outroHoldSeconds;
        [Tooltip("Shown walking across the black outro (Day 20: his and her mugs).")]
        [SerializeField] private Sprite[] outroWalkers;

        [Header("Finale")]
        [Tooltip("Last day: after the final lines the camera drifts to the window sill before the ending.")]
        [SerializeField] private bool finale;
        [Tooltip("A badly made drink: this line shows, then the drink is made again from its first step.")]
        [SerializeField, TextArea] private string remakeText = "It's bad. Let's remake it.";
        [Tooltip("Day 10: the finished drink is handed to her at the front door (the door scene with her and the cup).")]
        [SerializeField] private bool serveAtDoor;

        [Header("Extras")]
        [Tooltip("The window glass is fogged; between steps the player can wipe it with the mouse (Days 20 and 30).")]
        [SerializeField] private bool foggyWindow;
        [Tooltip("A step at or above this counts toward a perfect day (gold star on the calendar).")]
        [SerializeField, Range(0f, 1f)] private float perfectStepQuality = 0.9f;
        [Tooltip("Last day only: the morning after the credits — light and sky of the post-credits kitchen.")]
        [SerializeField] private LevelPalette epiloguePalette = LevelPalette.Default;
        [SerializeField] private Color epilogueSky = new(0.86f, 0.9f, 0.96f);

        public int DayNumber => dayNumber;
        public string ClockTime => clockTime;
        public TimeOfDay TimeOfDay => timeOfDay;
        public Weather Weather => weather;
        public LevelPalette Palette => palette;
        public TreeStage TreeStage => treeStage;
        public Sprite TreeCrownSprite => treeCrownSprite;
        public Color SkyColor => skyColor;
        public MiraWindowState MiraWindow => miraWindow;
        public KitchenProps Props => props;
        public IReadOnlyList<MinigameStep> Steps => steps;
        public IReadOnlyList<StoryBeat> StoryBeats => storyBeats;
        public bool TwoMeters => twoMeters;
        public float StartWarmth => startWarmth;
        public float EffectiveDrainPerSecond => drainPerSecond * coldMultiplier;
        public float WarmthPerStep => warmthPerStep;
        public float DrinkWarmthBonus => drinkWarmthBonus;
        public bool WarmthLocked => warmthLocked;
        public float MinWarmth => minWarmth;
        public float GoodResultThreshold => goodResultThreshold;
        public float BadResultThreshold => badResultThreshold;
        public string ColdDrinkText => coldDrinkText;
        public string CalendarLine => calendarLine;
        public bool TutorialHints => tutorialHints;
        public bool PulseRecipeCard => pulseRecipeCard;
        public string RecipeCardText => recipeCardText;

        /// <summary>
        /// Whose meter a step warms: on two-meter days the drink being made is hers, so steps for "both" warm Mira; a
        /// step aimed at him (his auto-filled cup) warms him.
        /// </summary>
        public WarmthTarget TargetFor(Minigames.MinigameStep step) =>
            twoMeters && step.WarmthTarget == WarmthTarget.Both ? WarmthTarget.Mira : step.WarmthTarget;

        /// <summary>The served drink warms whoever it is for (hers on two-meter days).</summary>
        public WarmthTarget DrinkTarget => twoMeters ? WarmthTarget.Mira : WarmthTarget.Both;

        /// <summary>Warmth change after a scored step: a good step warms, a bad one (below the threshold) chills.</summary>
        public float StepWarmthDelta(float quality)
        {
            if (quality >= badResultThreshold) return quality * warmthPerStep;
            var badness = badResultThreshold > 0f ? 1f - quality / badResultThreshold : 1f;
            return -badStepChill * Mathf.Lerp(0.5f, 1f, badness);
        }

        /// <summary>Warmth change when the drink is served (average quality of the scored steps).</summary>
        public float DrinkWarmthDelta(float quality) =>
            quality >= badResultThreshold ? quality * drinkWarmthBonus : -badDrinkChill;
        public string FailText => failText;
        public AudioClip Ambience => ambience;
        public AudioClip Music => music;
        public float AmbienceVolume => ambienceVolume;
        public IReadOnlyList<DrinkResult> DrinkResults => drinkResults;
        public AudioClip OutroClip => outroClip;
        public float OutroHoldSeconds => outroHoldSeconds;
        public Sprite[] OutroWalkers => outroWalkers;
        public bool Finale => finale;
        public string RemakeText => remakeText;
        public bool ServeAtDoor => serveAtDoor;
        public bool FoggyWindow => foggyWindow;
        public float PerfectStepQuality => perfectStepQuality;
        public LevelPalette EpiloguePalette => epiloguePalette;
        public Color EpilogueSky => epilogueSky;

        /// <summary>A result this poor is a bad drink: it chills, then the drink is made again.</summary>
        public bool IsBad(float quality) => quality < badResultThreshold;

        public List<StoryBeat> GetBeats(BeatTrigger trigger, int stepIndex = 0, bool goodResult = true) =>
            storyBeats.Where(b => b != null && b.Matches(trigger, stepIndex, goodResult)).ToList();

        /// <summary>Appends human-readable problems to <paramref name="errors"/>. Returns true when valid.</summary>
        public bool Validate(List<string> errors)
        {
            var before = errors.Count;

            if (!ValidDays.Contains(dayNumber))
                errors.Add($"{name}: day {dayNumber} is not one of {string.Join(", ", ValidDays)}.");
            if (steps.Count == 0)
                errors.Add($"{name}: has no minigame steps.");
            if (steps.Any(s => s == null))
                errors.Add($"{name}: has an empty step slot.");
            if (storyBeats.Count == 0)
                errors.Add($"{name}: has no story beats.");

            for (var i = 0; i < storyBeats.Count; i++)
            {
                var beat = storyBeats[i];
                if (beat == null || string.IsNullOrWhiteSpace(beat.Text))
                    errors.Add($"{name}: story beat {i} has no text.");
                else if (beat.Trigger == BeatTrigger.AfterStep && beat.StepIndex >= steps.Count)
                    errors.Add($"{name}: story beat {i} points at step {beat.StepIndex}, but there are only {steps.Count} steps.");
            }

            if (drainPerSecond > 0f && startWarmth <= 0f)
                errors.Add($"{name}: start warmth must be above zero.");

            return errors.Count == before;
        }
    }
}
