using System;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>How to continue one arm sprite's sleeve past the screen edge.</summary>
    [Serializable]
    public sealed class SleeveEntry
    {
        public Sprite sprite;
        [Tooltip("A one-row slice across the sleeve (outline included), stretched along the arm.")]
        public Sprite strip;
        [Tooltip("Where the strip starts, in the arm sprite's local units (relative to its pivot).")]
        public Vector2 anchor;
        [Tooltip("Rotation so the strip runs away from the hand along the arm.")]
        public float angle;
    }
}
