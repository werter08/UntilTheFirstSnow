using Courtyard.Audio;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>The wall clock ticks once a second — heard only in the silence, when no music plays (Day 1).</summary>
    public sealed class ClockTicker : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float secondsPerTick = 1f;

        private float _timer;

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < secondsPerTick) return;
            _timer -= secondsPerTick;
            if (!AudioManager.HasMusic) AudioManager.Play(SfxId.ClockTick);
        }
    }
}
