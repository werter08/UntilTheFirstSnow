using Courtyard.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Courtyard.UI
{
    /// <summary>Horizontal warmth bar. The fill rect's right anchor follows the meter value.</summary>
    public sealed class WarmthMeterView : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [SerializeField] private Gradient coldToWarm;
        [Tooltip("Pulses when the meter is locked full (Day 30: warm together).")]
        [SerializeField] private Image glow;
        [SerializeField, Min(0.01f)] private float glowSpeed = 1.6f;
        [SerializeField, Range(0f, 1f)] private float glowMaxAlpha = 0.6f;

        private WarmthMeter _meter;

        public void Bind(WarmthMeter meter)
        {
            Unbind();
            _meter = meter;
            gameObject.SetActive(meter != null);
            if (meter == null) return;

            meter.Changed += OnChanged;
            OnChanged(meter.Value);
        }

        private void OnDestroy() => Unbind();

        private void Update()
        {
            var locked = _meter != null && _meter.IsLocked;
            var colour = glow.color;
            var alpha = locked ? (0.5f + 0.5f * Mathf.Sin(Time.time * glowSpeed * Mathf.PI)) * glowMaxAlpha : 0f;
            glow.color = new Color(colour.r, colour.g, colour.b, alpha);
        }

        private void Unbind()
        {
            if (_meter != null) _meter.Changed -= OnChanged;
            _meter = null;
        }

        private void OnChanged(float _)
        {
            var normalized = _meter.Normalized;
            fill.anchorMax = new Vector2(normalized, fill.anchorMax.y);
            fillImage.color = coldToWarm.Evaluate(normalized);
        }
    }
}
