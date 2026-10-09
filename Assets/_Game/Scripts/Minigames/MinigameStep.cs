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

        /// <summary>Raised once with the result quality in [0, 1].</summary>
        public event Action<float> Completed;

        public string HintText => hintText;
        public bool Scored => scored;
        public WarmthTarget WarmthTarget => warmthTarget;
        public bool IsComplete { get; private set; }

        public abstract void Begin(MinigameContext ctx);

        protected void Complete(float quality)
        {
            if (IsComplete) return;
            IsComplete = true;
            Completed?.Invoke(Mathf.Clamp01(quality));
        }
    }
}
