using System;
using UnityEngine;

namespace Courtyard.Core
{
    /// <summary>
    /// One warmth value in [0, Max]. Pure C# so it can be unit-tested; the level runner ticks it.
    /// </summary>
    public sealed class WarmthMeter
    {
        public const float Max = 100f;

        private readonly float _drainPerSecond;

        public WarmthMeter(float startValue, float drainPerSecond)
        {
            if (drainPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(drainPerSecond));

            _drainPerSecond = drainPerSecond;
            IsLocked = drainPerSecond <= 0f;
            // A meter that never drains is shown full (Day 30: warm together).
            Value = IsLocked ? Max : Mathf.Clamp(startValue, 0f, Max);
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

        private void Set(float value)
        {
            var clamped = Mathf.Clamp(value, 0f, Max);
            if (Mathf.Approximately(clamped, Value)) return;

            Value = clamped;
            Changed?.Invoke(Value);
            if (IsEmpty) Depleted?.Invoke();
        }
    }
}
