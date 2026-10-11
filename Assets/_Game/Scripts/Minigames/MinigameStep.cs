using System;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// One drink-making minigame. The level runner instantiates the prefab, calls <see cref="Begin"/>,
    /// waits for <see cref="Completed"/> and then destroys it.
    /// </summary>
    public abstract class MinigameStep : MonoBehaviour
    {
        [Tooltip("Shown at the top of the screen while this step runs.")]
        [SerializeField, TextArea] private string hintText;
        [Tooltip("Unscored steps (story events like waving back) don't restore warmth or count toward the drink.")]
        [SerializeField] private bool scored = true;
        [SerializeField] private WarmthTarget warmthTarget = WarmthTarget.Both;
        [Tooltip("Counter zone this step's own parts belong to; the step is shifted with the zone.")]
        [SerializeField] private Kitchen.CounterZone zone;
        [Tooltip("Story prompts (wave back, which mug, her drink) are new every time: their hint always shows. Other hints " +
                 "show on the tutorial day and the first time a minigame appears.")]
        [SerializeField] private bool alwaysShowHint;
        [Tooltip("Starts together with the previous step (Day 1: spoon the coffee in while the kettle heats); it is told " +
                 "when the previous one is done.")]
        [SerializeField] private bool startsWithPrevious;
        [Tooltip("Results below this explain what went wrong, above the tool (\"Too much water\"…).")]
        [SerializeField, Range(0f, 1f)] private float explainBelow = 0.6f;

        /// <summary>Raised once with the result quality in [0, 1].</summary>
        public event Action<float> Completed;

        public string HintText => hintText;
        public bool Scored => scored;
        public WarmthTarget WarmthTarget => warmthTarget;
        public Kitchen.CounterZone Zone => zone;
        public bool AlwaysShowHint => alwaysShowHint;
        public bool StartsWithPrevious => startsWithPrevious;

        /// <summary>For steps that started early: the previous step has finished (e.g. the water has boiled).</summary>
        public virtual void OnPreviousDone() { }

        /// <summary>When a bad drink is made again, steps whose result still stands (her mug is already chosen) are skipped.</summary>
        public virtual bool SkipOnRemake(Kitchen.CounterStation counter) => false;

        /// <summary>Whether the result counts toward the served drink (false for his auto-filled cup on Day 20).</summary>
        public virtual bool CountsTowardDrink => scored;

        /// <summary>A step whose result is always the same (no input), e.g. his auto-filled cup; null = played.</summary>
        public virtual float? FixedQuality => null;

        /// <summary>Where this step's result shows ("Perfect!", the warm puff): its own tool. Null = the cup.</summary>
        public Renderer ResultAt { get; protected set; }

        /// <summary>Why the last result was poor (empty when it went fine).</summary>
        public string Mistake { get; private set; } = string.Empty;
        public bool IsComplete { get; private set; }

        /// <summary>The tool to nudge when the player idles (null = no nudge, e.g. when it would give the answer away).</summary>
        public Transform NudgeTarget { get; protected set; }

        public abstract void Begin(MinigameContext ctx);

        /// <summary>
        /// The drink is spoiled (wrong amount): it finishes with quality 0, so it's a cold drink — "Brr… cold." and the
        /// meters drop — but the day goes on. Only warmth running out ends a day.
        /// </summary>
        protected void Ruin() => Complete(0f);

        protected bool ShouldExplain(float quality) => quality < explainBelow;

        /// <summary>
        /// The red part of a gauge: where quality = 1 − |v − centre| / falloff drops below the cold-drink line (0.4),
        /// past the green on the high side. Null when that never happens inside 0..1.
        /// </summary>
        protected static Vector2? RedAbove(float centre, float falloff, float coldBelow = 0.4f)
        {
            var from = centre + (1f - coldBelow) * falloff;
            return from < 1f ? new Vector2(from, 1f) : null;
        }

        /// <summary>
        /// Tells the player what went wrong, right where it happened: a short line above the tool, a puff and a wobble.
        /// Call before <see cref="Complete"/>.
        /// </summary>
        protected void Explain(Kitchen.FeedbackFx fx, string text, Renderer at)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Mistake = text;
            if (fx == null) return;
            var top = at != null ? new Vector3(at.bounds.center.x, at.bounds.max.y + 0.45f, 0f) : Vector3.zero;
            fx.Explain(text, top, at != null ? at.transform : null);
        }

        protected void Complete(float quality)
        {
            if (IsComplete) return;
            IsComplete = true;
            Completed?.Invoke(Mathf.Clamp01(quality));
        }
    }
}
