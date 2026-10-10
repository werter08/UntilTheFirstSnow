using System;
using UnityEngine;

namespace Courtyard.Core
{
    /// <summary>
    /// One warmth value in [floor, Max]. Pure C# so it can be unit-tested; the level runner ticks it.
    /// Warmth now moves with how well the drinks are made (drain is 0 in every level); a locked meter (Day 30)
    /// stays full, and a floor above 0 (Day 1) means the meter can never empty.
    /// </summary>
    public sealed class WarmthMeter
    {
        public const float Max = 100f;

        private readonly float _drainPerSecond;
        private readonly float _floor;

        public WarmthMeter(float startValue, float drainPerSecond, bool locked = false, float floor = 0f)
        {
            if (drainPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(drainPerSecond));
            if (floor < 0f || floor > Max) throw new ArgumentOutOfRangeException(nameof(floor));

            _drainPerSecond = drainPerSecond;
            _floor = floor;
            IsLocked = locked;
            // A locked meter is shown full (Day 30: warm together).
            Value = IsLocked ? Max : Mathf.Clamp(startValue, floor, Max);
        }

        /// <summary>Raised with the new value whenever it changes.</summary>
        public event Action<float> Changed;

        /// <summary>Raised once, the first time the value reaches zero.</summary>
        public event Action Depleted;

        public float Value { get; private set; }
        public float Normalized => Value / Max;
        public bool IsLocked { get; }
        public bool IsEmpty => Value <= 0f;

        public void Tick(float deltaTime)
        {
            if (IsLocked || IsEmpty || deltaTime <= 0f) return;
            Set(Value - _drainPerSecond * deltaTime);
        }

        public void Restore(float amount)
        {
            if (IsLocked || IsEmpty || amount <= 0f) return;
            Set(Value + amount);
        }

        /// <summary>Takes warmth away at once (a cold, badly made drink). Can empty the meter.</summary>
        public void Chill(float amount)
        {
            if (IsLocked || IsEmpty || amount <= 0f) return;
            Set(Value - amount);
        }

        private void Set(float value)
        {
            var clamped = Mathf.Clamp(value, _floor, Max);
            if (Mathf.Approximately(clamped, Value)) return;

            Value = clamped;
            Changed?.Invoke(Value);
            if (IsEmpty) Depleted?.Invoke();
        }
    }
}
