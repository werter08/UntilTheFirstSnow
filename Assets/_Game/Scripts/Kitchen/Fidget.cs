using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// A kitchen detail that reacts when clicked (no score, just life): it swings on its pivot like a spring and/or
    /// punches, plays a sound and can puff a few particles. Uses a PointerTarget, so the hand shows on hover.
    /// </summary>
    [RequireComponent(typeof(PointerTarget))]
    public sealed class Fidget : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [Tooltip("Spring swing on click (degrees); 0 = none.")]
        [SerializeField] private float swingDegrees = 8f;
        [SerializeField, Min(0.1f)] private float swingFrequency = 3f;
        [SerializeField, Min(0.1f)] private float swingDamping = 3f;
        [SerializeField, Range(0f, 1f)] private float punch = 0.5f;
        [SerializeField] private SfxId sound = SfxId.UiClick;
        [SerializeField, Min(0)] private int particles;
        [SerializeField] private Color particleColour = Color.white;

        private PointerTarget _target;
        private FeedbackFx _fx;
        private Quaternion _baseRotation;
        private float _swingTime = -1f;

        /// <summary>Raised after the fidget reacts (e.g. Mira's window waves back).</summary>
        public event System.Action Poked;

        private void Awake()
        {
            _target = GetComponent<PointerTarget>();
            if (visual == null) visual = transform;
            _baseRotation = visual.localRotation;
        }

        private void OnEnable() => _target.Clicked += OnClicked;
        private void OnDisable() => _target.Clicked -= OnClicked;

        private void OnClicked()
        {
            if (_fx == null) _fx = FindAnyObjectByType<FeedbackFx>();
            if (punch > 0f && _fx != null) _fx.Punch(visual, punch);
            if (particles > 0 && _fx != null) _fx.Burst(visual.position, particleColour, particles);
            AudioManager.Play(sound);
            if (swingDegrees > 0f) _swingTime = 0f;
            Poked?.Invoke();
        }

        private void Update()
        {
            if (_swingTime < 0f) return;
            _swingTime += Time.deltaTime;
            var envelope = Mathf.Exp(-swingDamping * _swingTime);
            var angle = Mathf.Sin(_swingTime * swingFrequency * Mathf.PI * 2f) * swingDegrees * envelope;
            visual.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, angle);
            if (envelope < 0.01f)
            {
                visual.localRotation = _baseRotation;
                _swingTime = -1f;
            }
        }
    }
}
