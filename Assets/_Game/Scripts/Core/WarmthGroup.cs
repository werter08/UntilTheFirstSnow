using System;
using System.Collections.Generic;

namespace Courtyard.Core
{
    /// <summary>
    /// One or two warmth meters (index 0 = him, 1 = Mira). The level fails when any meter empties.
    /// </summary>
    public sealed class WarmthGroup
    {
        public const int HimIndex = 0;
        public const int MiraIndex = 1;

        private readonly List<WarmthMeter> _meters = new();
        private bool _depleted;

        public WarmthGroup(int count, float startValue, float drainPerSecond)
        {
            if (count < 1 || count > 2) throw new ArgumentOutOfRangeException(nameof(count), "Supports one or two meters.");

            for (var i = 0; i < count; i++)
            {
                var meter = new WarmthMeter(startValue, drainPerSecond);
                meter.Depleted += OnMeterDepleted;
                _meters.Add(meter);
            }
        }

        /// <summary>Raised once, when the first meter reaches zero.</summary>
        public event Action Depleted;

        public IReadOnlyList<WarmthMeter> Meters => _meters;
        public bool IsDepleted => _depleted;

        public void Tick(float deltaTime)
        {
            foreach (var meter in _meters) meter.Tick(deltaTime);
        }

        /// <summary>
        /// Restores the targeted meter. On a one-meter day every target maps to the single meter.
        /// </summary>
        public void Restore(WarmthTarget target, float amount)
        {
            switch (target)
            {
                case WarmthTarget.Him:
                    _meters[HimIndex].Restore(amount);
                    break;
                case WarmthTarget.Mira:
                    _meters[_meters.Count > MiraIndex ? MiraIndex : HimIndex].Restore(amount);
                    break;
                default:
                    foreach (var meter in _meters) meter.Restore(amount);
                    break;
            }
        }

        private void OnMeterDepleted()
        {
            if (_depleted) return;
            _depleted = true;
            Depleted?.Invoke();
        }
    }
}
