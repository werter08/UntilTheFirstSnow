using UnityEngine;

namespace Courtyard.Audio
{
    /// <summary>Sets the music when a scene starts (e.g. the Ending keeps Day 30's track playing).</summary>
    public sealed class MusicCue : MonoBehaviour
    {
        [SerializeField] private AudioClip music;

        private void Start() => AudioManager.SetMusic(music);
    }
}
