using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Hold the counter milk jug to steam: temperature and foam rise. Release inside the green zone.
    /// Holding too long makes the milk squeal (the jug flashes) and ends the step at the maximum.
    /// The finished foam goes onto the mug. Owns only the thermometer.
    /// </summary>
    public sealed class SteamMilkStep : MinigameStep
    {
        [Header("Own gauge")]
        [SerializeField] private Transform needle;
        [SerializeField] private SpriteRenderer greenBand;
        [SerializeField, Min(0.1f)] private float scaleHeight = 2.4f;

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

        private CounterStation _counter;
        private float _temperature;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _temperature = startTemperature;
            _counter.MilkJug.Released += OnReleased;
            _counter.MilkJug.Interactable = true;

            var bottom = ToScaleY(bandMin);
            var top = ToScaleY(bandMax);
            greenBand.size = new Vector2(greenBand.size.x, top - bottom);
            greenBand.transform.localPosition = new Vector3(0f, (bottom + top) * 0.5f, 0f);
            UpdateVisuals();
        }

        private void OnDestroy()
        {
            if (_counter == null) return;
            _counter.MilkJug.Released -= OnReleased;
            _counter.MilkJugBody.color = jugColor;
        }

        private void Update()
        {
            if (IsComplete || !_counter.MilkJug.IsHeld) return;

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
            _counter.MilkJug.Interactable = false;
            _counter.MugFoam.SetActive(true);
            var centre = (bandMin + bandMax) * 0.5f;
            Complete(1f - Mathf.Abs(_temperature - centre) / qualityFalloff);
        }

        private float ToScaleY(float temperature) =>
            Mathf.InverseLerp(startTemperature, maxTemperature, temperature) * scaleHeight;

        private void UpdateVisuals()
        {
            needle.localPosition = new Vector3(0f, ToScaleY(_temperature), 0f);
            _counter.MilkFoam.SetValue(Mathf.InverseLerp(startTemperature, maxTemperature, _temperature));
            _counter.MilkJugBody.color = _temperature > bandMax ? squealColor : jugColor;
        }
    }
}
