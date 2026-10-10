using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Hold over the V60 to pour from the counter kettle and move in slow circles. Flow follows how fast the
    /// pointer moves and must stay in a band. Quality = share of pouring time in the band + how close the mug
    /// fill ends to the line. Owns the pour zone and the flow meter.
    /// </summary>
    public sealed class PourOverStep : MinigameStep
    {
        [Header("Own parts")]
        [SerializeField] private PointerTarget pourZone;
        [SerializeField] private SpriteBar flowMeter;
        [SerializeField] private SpriteRenderer flowBand;

        [Header("Tuning")]
        [SerializeField, Min(0.01f)] private float fillPerSecond = 0.12f;
        [SerializeField, Range(0f, 1f)] private float targetFill = 0.8f;
        [SerializeField, Min(0.01f)] private float fillTolerance = 0.25f;
        [Tooltip("Pointer speed (world units/s) that reads as full flow.")]
        [SerializeField, Min(0.1f)] private float speedForFullFlow = 8f;
        [SerializeField, Min(0.1f)] private float flowSmoothing = 4f;
        [SerializeField, Range(0f, 1f)] private float bandMin = 0.12f;
        [SerializeField, Range(0f, 1f)] private float bandMax = 0.55f;
        [Tooltip("Only pours while the pointer is this close to the dripper centre.")]
        [SerializeField, Min(0.1f)] private float pourRadius = 1.4f;
        [SerializeField, Range(0f, 1f)] private float bandWeight = 0.6f;
        [SerializeField, Range(0f, 1f)] private float minFillToFinish = 0.1f;
        [Tooltip("Seconds of pouring the kettle holds (matches the ~8 s pour sound); then the stream fades and the pour ends.")]
        [SerializeField, Min(1f)] private float waterSeconds = 8f;
        [SerializeField, Min(0f)] private float streamFadeSeconds = 0.35f;
        [Tooltip("How quickly the kettle catches up with the pointer (higher = snappier).")]
        [SerializeField, Min(0.1f)] private float kettleFollow = 14f;

        [Header("Look")]
        [SerializeField] private Color coffeeColor = new(0.35f, 0.2f, 0.1f);

        private CounterStation _counter;
        private FeedbackFx _fx;
        private float _splashTimer;
        private Camera _camera;
        private Vector2 _lastPointer;
        private float _flow;
        private float _pourTime;
        private float _bandTime;
        private float _pouredSeconds;
        private bool _runningDry;
        private Color _streamColour;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _fx = ctx.Fx;
            _camera = ctx.WorldCamera;
            _streamColour = _counter.KettleStream.color;
            NudgeTarget = _counter.Dripper;
            pourZone.Pressed += OnPressed;
            pourZone.Released += OnReleased;

            _counter.SetMugDrink(MugDrink.Black, coffeeColor);
            _counter.SetMugGauge(true);
            _counter.ShowFillLine(targetFill);

            flowMeter.SetValue(0f);
            var height = flowMeter.MaxHeight;
            flowBand.size = new Vector2(flowBand.size.x, (bandMax - bandMin) * height);
            flowBand.transform.localPosition = new Vector3(0f, (bandMin + bandMax) * 0.5f * height, 0f);
        }

        private void OnDestroy()
        {
            if (pourZone != null)
            {
                pourZone.Pressed -= OnPressed;
                pourZone.Released -= OnReleased;
            }

            if (_counter == null) return;
            ReturnKettle();
            _counter.HideFillLine();
            _counter.SetMugGauge(false);
            _counter.KettleStream.color = _streamColour;
            AudioManager.StopTrack(SfxId.Pour);
        }

        private void OnPressed()
        {
            if (_kettleReturn != null) StopCoroutine(_kettleReturn);
            _counter.KettleView.SetPouring(true);
            _lastPointer = PointerWorld();
        }

        private bool _streamOn;

        private void SetStream(bool on)
        {
            if (_streamOn == on) return;
            _streamOn = on;
            _counter.KettleStream.enabled = on;
            // One-shot pour: pausing keeps its place, so tipping the kettle again continues the same pour.
            if (on) AudioManager.ResumeTrack(SfxId.Pour);
            else AudioManager.PauseTrack(SfxId.Pour);
        }

        private void OnReleased()
        {
            ReturnKettle();
            if (!IsComplete && _counter.MugLevel >= minFillToFinish) Finish();
        }

        private void Update()
        {
            if (IsComplete || _runningDry || !pourZone.IsHeld) return;

            var pointer = PointerWorld();
            var speed = (pointer - _lastPointer).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastPointer = pointer;
            _flow = Mathf.Lerp(_flow, Mathf.Clamp01(speed / speedForFullFlow), 1f - Mathf.Exp(-flowSmoothing * Time.deltaTime));
            flowMeter.SetValue(_flow);

            var kettle = _counter.Kettle;
            var follow = 1f - Mathf.Exp(-kettleFollow * Time.deltaTime);
            kettle.position = Vector3.Lerp(kettle.position, new Vector3(pointer.x, pointer.y, kettle.position.z), follow);
            var overDripper = Vector2.Distance(kettle.position, _counter.Dripper.position) <= pourRadius;
            SetStream(overDripper);
            if (!overDripper) return;

            _pourTime += Time.deltaTime;
            _pouredSeconds += Time.deltaTime;
            AudioManager.SetTrackVolume(SfxId.Pour, Mathf.Lerp(0.35f, 1f, _flow)); // louder with more flow
            if (_pouredSeconds >= waterSeconds)
            {
                StartCoroutine(RunDry());
                return;
            }

            if (_flow >= bandMin && _flow <= bandMax) _bandTime += Time.deltaTime;

            _counter.SetMugLevel(_counter.MugLevel + fillPerSecond * Time.deltaTime);
            _splashTimer -= Time.deltaTime;
            if (_splashTimer <= 0f && _fx != null)
            {
                _splashTimer = 0.15f;
                _fx.Burst(_counter.Mug.transform.position + Vector3.up * 0.6f, coffeeColor, 2, 0.7f, 0.05f, 1.2f);
            }

            if (_counter.MugLevel >= 1f) Finish();
        }

        private Vector2 PointerWorld()
        {
            var screen = Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
            return _camera.ScreenToWorldPoint(screen);
        }

        private void ReturnKettle()
        {
            SetStream(false);
            _counter.KettleView.SetPouring(false);
            if (_kettleReturn != null) StopCoroutine(_kettleReturn);
            var kettle = _counter.Kettle;
            var from = kettle.position;
            var rest = _counter.KettleRest;
            if (!isActiveAndEnabled)
            {
                kettle.position = rest;
                return;
            }

            _kettleReturn = StartCoroutine(Tweens.Run(0.25f, t => kettle.position = Vector3.Lerp(from, rest, t)));
        }

        private Coroutine _kettleReturn;

        /// <summary>The kettle is empty: the stream thins out and fades, then the pour ends.</summary>
        private System.Collections.IEnumerator RunDry()
        {
            _runningDry = true;
            var stream = _counter.KettleStream;
            var from = _streamColour;
            yield return Tweens.Run(streamFadeSeconds, t => stream.color = new Color(from.r, from.g, from.b, from.a * (1f - t)));
            Finish();
            stream.color = from;
        }

        private void Finish()
        {
            pourZone.Interactable = false;
            ReturnKettle();
            _counter.StartCoroutine(_counter.LiftDripper()); // runs on the counter: this step is destroyed right after

            var bandScore = _pourTime > 0f ? _bandTime / _pourTime : 0f;
            var fillScore = Mathf.Clamp01(1f - Mathf.Abs(_counter.MugLevel - targetFill) / fillTolerance);
            Complete(bandWeight * bandScore + (1f - bandWeight) * fillScore);
        }
    }
}
