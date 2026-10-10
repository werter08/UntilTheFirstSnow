using UnityEngine;

namespace Courtyard.Audio
{
    /// <summary>Sets the music (and optionally the ambience) when a scene starts — the title's lofi and wind, the Ending's Day 30 track.</summary>
    public sealed class MusicCue : MonoBehaviour
    {
        [SerializeField] private AudioClip music;
        [Tooltip("Optional background sound (e.g. autumn wind on the title screen).")]
        [SerializeField] private AudioClip ambience;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.5f;

        private void Start()
        {
            AudioManager.SetMusic(music);
            if (ambience != null) AudioManager.SetAmbience(ambience, ambienceVolume);
        }
    }
}
