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

        [SerializeField, Min(0.1f)] private float fillSmoothing = 6f;
        [SerializeField, Range(0f, 0.5f)] private float gainPunch = 0.12f;

        private WarmthMeter _meter;
        private float _shown;
        private float _punchTime = -1f;

        public void Bind(WarmthMeter meter)
        {
            Unbind();
            _meter = meter;
            gameObject.SetActive(meter != null);
            if (meter == null) return;

            meter.Changed += OnChanged;
            _shown = meter.Normalized;
            OnChanged(meter.Value);
        }

        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (_meter != null)
            {
                _shown = Mathf.Lerp(_shown, _meter.Normalized, 1f - Mathf.Exp(-fillSmoothing * Time.deltaTime));
                ApplyFill();
            }

            if (_punchTime >= 0f)
            {
                _punchTime += Time.deltaTime;
                var s = 1f + Mathf.Sin(_punchTime * Mathf.PI * 4f) * Mathf.Exp(-_punchTime * 6f) * gainPunch;
                transform.localScale = new Vector3(s, s, 1f);
                if (_punchTime > 1f) { transform.localScale = Vector3.one; _punchTime = -1f; }
            }

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
            if (_meter.Normalized > _shown + 0.01f) _punchTime = 0f; // warmth gained: little bounce
        }

        private void ApplyFill()
        {
            fill.anchorMax = new Vector2(_shown, fill.anchorMax.y);
            fillImage.color = coldToWarm.Evaluate(_shown);
        }
    }
}
