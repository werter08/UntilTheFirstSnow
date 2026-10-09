using System;
using Courtyard.Story;
using UnityEngine;

namespace Courtyard.Core
{
    /// <summary>A drink icon that pops up above the mug when the day's drink is served.</summary>
    [Serializable]
    public sealed class DrinkResult
    {
        public Sprite icon;
        [Tooltip("e.g. Day 10: her cinnamon drink only shows if it was guessed right.")]
        public BeatCondition condition;

        public bool Shows(bool goodResult) => condition switch
        {
            BeatCondition.GoodResult => goodResult,
            BeatCondition.PoorResult => !goodResult,
            _ => true,
        };
    }
}
