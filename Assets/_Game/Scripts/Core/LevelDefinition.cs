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
        [Tooltip("Step quality at or above this counts as a good result for conditional beats.")]
        [SerializeField, Range(0f, 1f)] private float goodResultThreshold = 0.7f;
        [SerializeField, TextArea] private string failText = "Too cold… let's try again.";

        [Header("Served drink")]
        [Tooltip("Icons that pop up above the mug when the drink is served.")]
        [SerializeField] private List<DrinkResult> drinkResults = new();

        [Header("Audio")]
        [SerializeField] private AudioClip ambience;

        [Header("Outro")]
        [Tooltip("Played over black after the last line (Day 20: footsteps across the courtyard).")]
        [SerializeField] private AudioClip outroClip;
        [SerializeField, Min(0f)] private float outroHoldSeconds;
        [Tooltip("Shown walking across the black outro (Day 20: his and her mugs).")]
        [SerializeField] private Sprite[] outroWalkers;

        [Header("Finale")]
        [Tooltip("Last day: after the final lines the camera drifts to the window sill before the ending.")]
        [SerializeField] private bool finale;

        public int DayNumber => dayNumber;
        public string ClockTime => clockTime;
        public TimeOfDay TimeOfDay => timeOfDay;
        public Weather Weather => weather;
        public LevelPalette Palette => palette;
        public TreeStage TreeStage => treeStage;
        public MiraWindowState MiraWindow => miraWindow;
        public KitchenProps Props => props;
        public IReadOnlyList<MinigameStep> Steps => steps;
        public IReadOnlyList<StoryBeat> StoryBeats => storyBeats;
        public bool TwoMeters => twoMeters;
        public float StartWarmth => startWarmth;
        public float EffectiveDrainPerSecond => drainPerSecond * coldMultiplier;
        public float WarmthPerStep => warmthPerStep;
        public float DrinkWarmthBonus => drinkWarmthBonus;
        public float GoodResultThreshold => goodResultThreshold;
        public string FailText => failText;
        public AudioClip Ambience => ambience;
        public IReadOnlyList<DrinkResult> DrinkResults => drinkResults;
        public AudioClip OutroClip => outroClip;
        public float OutroHoldSeconds => outroHoldSeconds;
        public Sprite[] OutroWalkers => outroWalkers;
        public bool Finale => finale;

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
