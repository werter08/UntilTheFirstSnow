using Courtyard.Audio;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Hold the counter milk jug to steam: temperature and foam rise. Release inside the green zone.
    /// Holding too long makes the milk squeal (the jug flashes) and ends the step at the maximum.
    /// The steamed milk then goes into the cup by hand (<see cref="PourMilkStep"/>). Owns only the thermometer.
    /// </summary>
    public sealed class SteamMilkStep : MinigameStep
    {
        [Header("Own gauge")]
        [Tooltip("Icon on the milk gauge.")]
        [SerializeField] private Sprite gaugeIcon;

        [Header("Tuning (°C)")]
        [SerializeField] private float startTemperature = 5f;
        [SerializeField] private float maxTemperature = 80f;
        [SerializeField] private float bandMin = 60f;
        [SerializeField] private float bandMax = 68f;
        [SerializeField, Min(0.1f)] private float heatPerSecond = 13f;
        [SerializeField, Min(0.1f)] private float qualityFalloff = 18f;
        [Tooltip("Releasing before this much heating is ignored, so a quick click doesn't end the step.")]
        [SerializeField, Min(0f)] private float minHeatingToFinish = 35f;

        [Header("Look")]
        [Header("Why it went wrong")]
        [SerializeField] private string tooColdText = "Still cold — hold it a little longer.";
        [SerializeField] private string tooHotText = "Too hot — the milk scorched.";

        [SerializeField] private Color jugColor = new(0.8f, 0.82f, 0.85f);
        [SerializeField] private Color squealColor = new(1f, 0.55f, 0.5f);

        private CounterStation _counter;
        private FeedbackFx _fx;
        private UI.MinigameGauge _gauge;
        private float _temperature;
        private HandsView _hands;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _fx = ctx.Fx;
            _hands = ctx.Hands;
            if (_hands != null) _hands.UseRightHand(true); // the milk is worked with the right hand
            _temperature = startTemperature;
            _counter.MilkJug.Pressed += OnPressed;
            _counter.MilkJug.Released += OnReleased;
            _counter.MilkJug.Interactable = true;
            NudgeTarget = _counter.MilkJug.transform;
            ResultAt = _counter.MilkJugBody;

            _gauge = ctx.Gauge;
            var centre = Normalized((bandMin + bandMax) * 0.5f);
            var falloff = qualityFalloff / (maxTemperature - startTemperature);
            if (_gauge != null)
                _gauge.Show(_counter.MilkJugBody, ctx.WorldCamera, new Vector2(Normalized(bandMin), Normalized(bandMax)),
                    RedAbove(centre, falloff), gaugeIcon);
            UpdateVisuals();
        }

        private void OnDestroy()
        {
            if (_hands != null) _hands.UseRightHand(false);
            if (_counter == null) return;
            _counter.MilkJug.Pressed -= OnPressed;
            _counter.MilkJug.Released -= OnReleased;
            _counter.MilkJugBody.color = jugColor;
            StopSounds();
            if (_gauge != null) _gauge.Hide();
        }

        private void Update()
        {
            if (IsComplete || !_counter.MilkJug.IsHeld) return;

            _temperature = Mathf.Min(maxTemperature, _temperature + heatPerSecond * Time.deltaTime);
            UpdateVisuals();
            if (_temperature >= maxTemperature) Finish();
        }

        private void OnPressed()
        {
            AudioManager.StartLoop(SfxId.Steam, 0.05f);
            _counter.SetFrothing(true);
        }

        private void StopSounds()
        {
            if (_counter != null) _counter.SetFrothing(false);
            AudioManager.StopLoop(SfxId.Steam, 0.1f);
            AudioManager.StopLoop(SfxId.Squeal, 0.1f);
        }

        private void OnReleased()
        {
            StopSounds();
            if (!IsComplete && _temperature - startTemperature >= minHeatingToFinish) Finish();
        }

        private void Finish()
        {
            _counter.MilkJug.Interactable = false;
            StopSounds();
            _fx.Punch(_counter.MilkJugBody.transform);
            _fx.Burst(_counter.MilkJugBody.bounds.center + Vector3.up * 0.6f, new Color(0.98f, 0.95f, 0.88f), 8, 0.6f, 0.06f, 0.4f);
            if (_gauge != null) _gauge.Release();
            var centre = (bandMin + bandMax) * 0.5f;
            var quality = 1f - Mathf.Abs(_temperature - centre) / qualityFalloff;
            if (ShouldExplain(quality)) Explain(_fx, _temperature < centre ? tooColdText : tooHotText, _counter.MilkJugBody);
            Complete(quality);
        }

        private float Normalized(float temperature) => Mathf.InverseLerp(startTemperature, maxTemperature, temperature);

        private void UpdateVisuals()
        {
            if (_gauge != null) _gauge.SetValue(Normalized(_temperature));
            _counter.SetMilkFoam(Mathf.InverseLerp(startTemperature, maxTemperature, _temperature));
            var squealing = _temperature > bandMax;
            _counter.MilkJugBody.color = squealing ? squealColor : jugColor;
            if (squealing && _counter.MilkJug.IsHeld) AudioManager.StartLoop(SfxId.Squeal);
        }
    }
}
