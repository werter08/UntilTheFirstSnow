using Courtyard.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Courtyard.UI
{
    /// <summary>
    /// A warmth meter drawn like the rest of the game: the person's mug, a hand-drawn bar, and a fill that is cold blue
    /// when empty and warm orange when full (the two drawn fills cross-fade). Steam rises from the mug as warmth grows;
    /// below 30% the mug frosts over. The fill rect (a mask) follows the value; a small bounce on gains.
    /// </summary>
    public sealed class WarmthMeterView : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [SerializeField] private Gradient coldToWarm;
        [Tooltip("Pulses when the meter is locked full (Day 30: warm together).")]
        [SerializeField] private Image glow;
        [SerializeField, Min(0.01f)] private float glowSpeed = 1.6f;
        [SerializeField, Range(0f, 1f)] private float glowMaxAlpha = 0.6f;

        [Header("Hand-drawn look (optional: without them the gradient colours the fill)")]
        [Tooltip("The warm fill drawn over the cold one; its alpha follows warmth.")]
        [SerializeField] private Image warmFill;
        [SerializeField] private Image mugIcon;
        [SerializeField] private Color frostTint = new(0.72f, 0.84f, 1f);
        [SerializeField, Range(0f, 1f)] private float frostBelow = 0.3f;
        [Tooltip("A soft puff that rises from the mug (steam).")]
        [SerializeField] private Sprite steamPuff;
        [SerializeField, Min(0f)] private float steamPerSecondAtFull = 2.5f;

        [SerializeField, Min(0.1f)] private float fillSmoothing = 6f;
        [SerializeField, Range(0f, 0.5f)] private float gainPunch = 0.12f;

        private WarmthMeter _meter;
        private float _shown;
        private float _punchTime = -1f;
        private float _steamTimer;

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

            UpdateMugAndSteam();
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
            if (warmFill == null)
            {
                fillImage.color = coldToWarm.Evaluate(_shown);
                return;
            }

            var c = warmFill.color;
            warmFill.color = new Color(c.r, c.g, c.b, Mathf.SmoothStep(0f, 1f, _shown)); // cold blue → warm orange
        }

        /// <summary>The mug frosts when it's cold and steams in proportion to warmth.</summary>
        private void UpdateMugAndSteam()
        {
            if (mugIcon == null || _meter == null) return;
            var cold = _shown < frostBelow;
            mugIcon.color = Color.Lerp(mugIcon.color, cold ? frostTint : Color.white, 1f - Mathf.Exp(-4f * Time.deltaTime));
            if (cold || steamPuff == null) return;

            _steamTimer += Time.deltaTime * steamPerSecondAtFull * Mathf.InverseLerp(frostBelow, 1f, _shown);
            if (_steamTimer < 1f) return;
            _steamTimer = 0f;
            StartCoroutine(Puff());
        }

        private System.Collections.IEnumerator Puff()
        {
            var go = new GameObject("Steam", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(mugIcon.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.45f, 1f);
            rect.sizeDelta = new Vector2(18f, 18f);
            var image = go.AddComponent<Image>();
            image.sprite = steamPuff;
            image.raycastTarget = false;
            var drift = Random.Range(-8f, 8f);
            yield return Tweens.RunLinear(1.4f, t =>
            {
                rect.anchoredPosition = new Vector2(drift * t + Mathf.Sin(t * 6f) * 3f, 4f + t * 34f);
                rect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.4f, t);
                image.color = new Color(1f, 1f, 1f, 0.7f * Mathf.Sin(t * Mathf.PI));
            });
            Destroy(go);
        }
    }
}
