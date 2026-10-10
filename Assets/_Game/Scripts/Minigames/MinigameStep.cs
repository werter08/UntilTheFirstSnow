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

        /// <summary>Raised once with the result quality in [0, 1].</summary>
        public event Action<float> Completed;

        /// <summary>Raised once when the drink is ruined (too much / too little): the day restarts.</summary>
        public event Action Ruined;

        public string HintText => hintText;
        public bool Scored => scored;
        public WarmthTarget WarmthTarget => warmthTarget;
        public Kitchen.CounterZone Zone => zone;
        public bool IsComplete { get; private set; }

        /// <summary>The tool to nudge when the player idles (null = no nudge, e.g. when it would give the answer away).</summary>
        public Transform NudgeTarget { get; protected set; }

        public abstract void Begin(MinigameContext ctx);

        /// <summary>The drink can't be saved (wrong amount): the level runner restarts the day.</summary>
        protected void Ruin()
        {
            if (IsComplete) return;
            IsComplete = true;
            Ruined?.Invoke();
        }

        protected void Complete(float quality)
        {
            if (IsComplete) return;
            IsComplete = true;
            Completed?.Invoke(Mathf.Clamp01(quality));
        }
    }
}
