using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Hold the jug to steam: temperature and foam rise. Release inside the green zone.
    /// Holding too long makes the milk squeal (the jug flashes) and ends the step at the maximum.
    /// </summary>
    public sealed class SteamMilkStep : MinigameStep
    {
        [Header("Scene refs")]
        [SerializeField] private PointerTarget jug;
        [SerializeField] private SpriteRenderer jugBody;
        [SerializeField] private SpriteBar foam;
        [Tooltip("Local origin is the bottom of the thermometer scale.")]
        [SerializeField] private Transform needle;
        [SerializeField] private SpriteRenderer greenBand;
        [SerializeField, Min(0.1f)] private float scaleHeight = 3f;

        [Header("Tuning (°C)")]
        [SerializeField] private float startTemperature = 5f;
        [SerializeField] private float maxTemperature = 80f;
        [SerializeField] private float bandMin = 60f;
        [SerializeField] private float bandMax = 68f;
        [SerializeField, Min(0.1f)] private float heatPerSecond = 13f;
        [SerializeField, Min(0.1f)] private float qualityFalloff = 18f;
        [Tooltip("Releasing before this much heating is ignored, so a quick click doesn't end the step.")]
        [SerializeField, Min(0f)] private float minHeatingToFinish = 5f;

        [Header("Look")]
        [SerializeField] private Color jugColor = new(0.8f, 0.82f, 0.85f);
        [SerializeField] private Color squealColor = new(1f, 0.55f, 0.5f);

        private float _temperature;

        public override void Begin(MinigameContext ctx)
        {
            _temperature = startTemperature;
            jug.Released += OnReleased;

            var bottom = ToScaleY(bandMin);
            var top = ToScaleY(bandMax);
            greenBand.size = new Vector2(greenBand.size.x, top - bottom);
            greenBand.transform.localPosition = new Vector3(0f, (bottom + top) * 0.5f, 0f);
            UpdateVisuals();
        }

        private void OnDestroy()
        {
            if (jug != null) jug.Released -= OnReleased;
        }

        private void Update()
        {
            if (IsComplete || !jug.IsHeld) return;

            _temperature = Mathf.Min(maxTemperature, _temperature + heatPerSecond * Time.deltaTime);
            UpdateVisuals();
            if (_temperature >= maxTemperature) Finish();
        }

        private void OnReleased()
        {
            if (!IsComplete && _temperature - startTemperature >= minHeatingToFinish) Finish();
        }

        private void Finish()
        {
            jug.Interactable = false;
            var centre = (bandMin + bandMax) * 0.5f;
            Complete(1f - Mathf.Abs(_temperature - centre) / qualityFalloff);
        }

        private float ToScaleY(float temperature) =>
            Mathf.InverseLerp(startTemperature, maxTemperature, temperature) * scaleHeight;

        private void UpdateVisuals()
        {
            needle.localPosition = new Vector3(0f, ToScaleY(_temperature), 0f);
            foam.SetValue(Mathf.InverseLerp(startTemperature, maxTemperature, _temperature));
            jugBody.color = _temperature > bandMax ? squealColor : jugColor;
        }
    }
}
