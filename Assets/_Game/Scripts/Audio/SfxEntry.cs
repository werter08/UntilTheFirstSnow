using System;
using UnityEngine;

namespace Courtyard.Audio
{
    [Serializable]
    public sealed class SfxEntry
    {
        public SfxId id;
        [Tooltip("One is picked at random each time (variation).")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch range (min, max).")]
        public Vector2 pitch = new(0.95f, 1.05f);
    }
}
