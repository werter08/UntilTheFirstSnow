using System;
using UnityEngine;

namespace Courtyard.Core
{
    /// <summary>Per-level lighting and tint values applied by the kitchen director.</summary>
    [Serializable]
    public struct LevelPalette
    {
        public Color globalLightColor;
        [Min(0f)] public float globalLightIntensity;
        public Color lampColor;
        [Min(0f)] public float lampIntensity;
        public Color miraWindowColor;
        public Color vignetteTint;

        public static LevelPalette Default => new LevelPalette
        {
            globalLightColor = Color.white,
            globalLightIntensity = 1f,
            lampColor = Color.white,
            lampIntensity = 1f,
            miraWindowColor = new Color(1f, 0.85f, 0.55f),
            vignetteTint = new Color(0f, 0f, 0f, 0f),
        };
    }
}
