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
