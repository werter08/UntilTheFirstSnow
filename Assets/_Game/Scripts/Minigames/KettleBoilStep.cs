using Courtyard.Audio;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Click the kettle switch to start heating, click again to stop. Quality depends on how close the
    /// temperature is to the centre of the green band. Uses the counter kettle; owns only the thermometer.
    /// </summary>
    public sealed class KettleBoilStep : MinigameStep
    {
        private enum State { Idle, Heating, Done }

        [Header("Own gauge")]
        [SerializeField] private Transform needle;
        [SerializeField] private SpriteRenderer greenBand;
        [SerializeField, Min(0.1f)] private float scaleHeight = 2.4f;

        [Header("Tuning (°C)")]
        [SerializeField] private float startTemperature = 20f;
        [SerializeField] private float maxTemperature = 100f;
        [SerializeField] private float bandMin = 90f;
        [SerializeField] private float bandMax = 96f;
        [SerializeField, Min(0.1f)] private float heatPerSecond = 12f;
        [Tooltip("Degrees away from the band centre at which quality reaches 0.")]
        [SerializeField, Min(0.1f)] private float qualityFalloff = 15f;

        [Header("Look")]
        [SerializeField] private Color switchOffColor = new(0.35f, 0.35f, 0.35f);
        [SerializeField] private Color switchOnColor = new(1f, 0.45f, 0.3f);

        private CounterStation _counter;
        private FeedbackFx _fx;
        private State _state = State.Idle;
        private float _temperature;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _fx = ctx.Fx;
            _temperature = startTemperature;
            _counter.KettleSwitch.Clicked += OnSwitchClicked;
            _counter.KettleSwitch.Interactable = true;

            var bottom = ToScaleY(bandMin);
            var top = ToScaleY(bandMax);
            greenBand.size = new Vector2(greenBand.size.x, top - bottom);
            greenBand.transform.localPosition = new Vector3(0f, (bottom + top) * 0.5f, 0f);
            UpdateVisuals();
        }

        private void OnDestroy()
        {
            if (_counter == null) return;
            _counter.KettleSwitch.Clicked -= OnSwitchClicked;
            _counter.SwitchLight.color = switchOffColor;
            AudioManager.StopLoop(SfxId.KettleBoil);
        }

        private void Update()
        {
            if (_state != State.Heating) return;

            _temperature = Mathf.Min(maxTemperature, _temperature + heatPerSecond * Time.deltaTime);
            UpdateVisuals();
            if (_temperature >= maxTemperature) Stop();
        }

        private void OnSwitchClicked()
        {
            switch (_state)
            {
                case State.Idle:
                    _state = State.Heating;
                    AudioManager.Play(SfxId.KettleSwitch);
                    AudioManager.StartLoop(SfxId.KettleBoil);
                    _fx.Punch(_counter.SwitchLight.transform);
                    UpdateVisuals();
                    break;
                case State.Heating:
                    Stop();
                    break;
            }
        }

        private void Stop()
        {
            _state = State.Done;
            _counter.KettleSwitch.Interactable = false;
            AudioManager.StopLoop(SfxId.KettleBoil);
            AudioManager.Play(SfxId.KettleSwitch);
            _fx.Punch(_counter.KettleBody.transform);
            UpdateVisuals();

            var bandCentre = (bandMin + bandMax) * 0.5f;
            Complete(1f - Mathf.Abs(_temperature - bandCentre) / qualityFalloff);
        }

        private float ToScaleY(float temperature) =>
            Mathf.InverseLerp(startTemperature, maxTemperature, temperature) * scaleHeight;

        private void UpdateVisuals()
        {
            needle.localPosition = new Vector3(0f, ToScaleY(_temperature), 0f);
            _counter.SwitchLight.color = _state == State.Heating ? switchOnColor : switchOffColor;
        }
    }
}
