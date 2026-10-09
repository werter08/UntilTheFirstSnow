using Courtyard.Audio;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Switch the kettle on, switch it off when it boils. No gauge: the temperature is hidden and only read from the
    /// kettle itself — the badge pulses while it heats, the lid starts to tremble, then rattles with a boiling sound
    /// when the water is ready. It keeps heating: leave it too long and the water boils away (lower quality), and
    /// eventually the kettle clicks off by itself.
    /// </summary>
    public sealed class KettleBoilStep : MinigameStep
    {
        private enum State { Idle, Heating, Done }

        [Header("Tuning (°C, hidden from the player)")]
        [SerializeField] private float startTemperature = 20f;
        [Tooltip("The lid starts to tremble from here.")]
        [SerializeField] private float trembleFrom = 75f;
        [SerializeField] private float bandMin = 90f;
        [SerializeField] private float bandMax = 97f;
        [SerializeField, Min(0.1f)] private float heatPerSecond = 12f;
        [Tooltip("Degrees outside the band at which quality reaches 0. Past the band, 'degrees' keep counting as water boils away.")]
        [SerializeField, Min(0.1f)] private float qualityFalloff = 25f;

        private CounterStation _counter;
        private FeedbackFx _fx;
        private State _state = State.Idle;
        private float _heat;
        private bool _boiling;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _fx = ctx.Fx;
            _heat = startTemperature;
            _counter.KettleSwitch.Clicked += OnSwitchClicked;
            _counter.KettleSwitch.Interactable = true;
            _counter.KettleView.SetHeating(false);
            NudgeTarget = _counter.SwitchLight.transform;
        }

        private void OnDestroy()
        {
            if (_counter == null) return;
            _counter.KettleSwitch.Clicked -= OnSwitchClicked;
            _counter.KettleView.SetHeating(false);
            _counter.KettleView.SetBoil(0f);
            StopSounds();
        }

        private void Update()
        {
            if (_state != State.Heating) return;

            _heat += heatPerSecond * Time.deltaTime;
            var boil = _heat >= bandMin ? 1f : Mathf.InverseLerp(trembleFrom, bandMin, _heat) * 0.6f;
            _counter.KettleView.SetBoil(boil);

            if (!_boiling && _heat >= bandMin)
            {
                _boiling = true;
                AudioManager.StartLoop(SfxId.KettleBubble);
            }

            // Boiled dry: the kettle switches itself off.
            if (_heat >= bandMax + qualityFalloff) Stop();
        }

        private void OnSwitchClicked()
        {
            switch (_state)
            {
                case State.Idle:
                    _state = State.Heating;
                    _counter.KettleView.SetHeating(true);
                    AudioManager.Play(SfxId.KettleSwitch);
                    AudioManager.StartLoop(SfxId.KettleBoil);
                    _fx.Punch(_counter.SwitchLight.transform);
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
            _counter.KettleView.SetHeating(false);
            _counter.KettleView.SetBoil(0f);
            StopSounds();
            AudioManager.Play(SfxId.KettleSwitch);
            _fx.Punch(_counter.KettleBody.transform);

            var distance = _heat < bandMin ? bandMin - _heat : _heat > bandMax ? _heat - bandMax : 0f;
            Complete(1f - distance / qualityFalloff);
        }

        private static void StopSounds()
        {
            AudioManager.StopLoop(SfxId.KettleBoil);
            AudioManager.StopLoop(SfxId.KettleBubble);
        }
    }
}
