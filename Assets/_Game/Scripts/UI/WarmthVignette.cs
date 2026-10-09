using Courtyard.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Courtyard.UI
{
    /// <summary>
    /// Frost on the screen edges when cold (below ~30 %), a warm glow when warm (above ~70 %).
    /// Follows the coldest meter.
    /// </summary>
    public sealed class WarmthVignette : MonoBehaviour
    {
        [SerializeField] private Image frost;
        [SerializeField] private Image warm;
        [SerializeField, Range(0f, 1f)] private float coldBelow = 0.3f;
        [SerializeField, Range(0f, 1f)] private float warmAbove = 0.7f;
        [SerializeField, Range(0f, 1f)] private float maxFrostAlpha = 0.85f;
        [SerializeField, Range(0f, 1f)] private float maxWarmAlpha = 0.45f;
        [SerializeField, Min(0.01f)] private float smoothing = 4f;

        private WarmthGroup _group;

        public void Bind(WarmthGroup group, Color warmTint)
        {
            _group = group;
            if (warmTint.a > 0f) warm.color = new Color(warmTint.r, warmTint.g, warmTint.b, 0f);
            SetAlpha(frost, 0f);
            SetAlpha(warm, 0f);
        }

        /// <summary>Coldest meter, 0..1.</summary>
        public float Coldest
        {
            get
            {
                var min = 1f;
                foreach (var meter in _group.Meters) min = Mathf.Min(min, meter.Normalized);
                return min;
            }
        }

        private void Update()
        {
            if (_group == null) return;

            var value = Coldest;
            var k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            SetAlpha(frost, Mathf.Lerp(frost.color.a, Mathf.InverseLerp(coldBelow, 0f, value) * maxFrostAlpha, k));
            SetAlpha(warm, Mathf.Lerp(warm.color.a, Mathf.InverseLerp(warmAbove, 1f, value) * maxWarmAlpha, k));
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var colour = graphic.color;
            graphic.color = new Color(colour.r, colour.g, colour.b, alpha);
        }
    }
}
