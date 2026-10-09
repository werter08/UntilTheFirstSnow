using System;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>Which particle system plays for a weather type, and how strongly.</summary>
    [Serializable]
    public sealed class WeatherPreset
    {
        public Weather weather;
        public ParticleSystem system;
        [Min(0f)] public float emissionRate = 30f;
        [Tooltip("Sideways drift in world units per second (wind).")]
        public float wind;
    }
}
