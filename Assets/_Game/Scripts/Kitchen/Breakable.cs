using System;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Click something too many times in a row and it breaks (lamp, mugs on the rack, curtains): counts quick clicks;
    /// past the limit it raises <see cref="Broke"/> once (cosmetic, for the rest of the day).
    /// </summary>
    [RequireComponent(typeof(PointerTarget))]
    public sealed class Breakable : MonoBehaviour
    {
        [Tooltip("Clicks within the window that break it.")]
        [SerializeField, Min(2)] private int clicksToBreak = 8;
        [SerializeField, Min(0.5f)] private float windowSeconds = 4f;
        [SerializeField] private Color shardColour = new(0.9f, 0.9f, 0.85f);

        private PointerTarget _target;
        private float[] _times;
        private int _next;

        public bool IsBroken { get; private set; }
        public Color ShardColour => shardColour;

        /// <summary>Raised once, when it breaks.</summary>
        public event Action Broke;

        private void Awake()
        {
            _target = GetComponent<PointerTarget>();
            _times = new float[clicksToBreak];
            for (var i = 0; i < _times.Length; i++) _times[i] = float.NegativeInfinity;
        }

        private void OnEnable() => _target.Clicked += OnClicked;
        private void OnDisable() => _target.Clicked -= OnClicked;

        private void OnClicked()
        {
            if (IsBroken) return;
            _times[_next] = Time.time;
            _next = (_next + 1) % _times.Length;
            var oldest = _times[_next]; // the click clicksToBreak ago
            if (Time.time - oldest > windowSeconds) return;
            IsBroken = true;
            Broke?.Invoke();
        }
    }
}
