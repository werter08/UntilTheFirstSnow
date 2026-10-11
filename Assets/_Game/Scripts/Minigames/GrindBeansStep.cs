using Courtyard.Audio;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Drag the counter grinder's handle in circles to fill the grind meter; let go inside the "medium" zone.
    /// Quality depends on how close the final level is to the target. Owns only the meter.
    /// </summary>
    public sealed class GrindBeansStep : MinigameStep
    {
        [Header("Gauge")]
        [SerializeField] private Sprite gaugeIcon;

        [Header("Tuning")]
        [SerializeField, Range(0f, 1f)] private float targetLevel = 0.55f;
        [SerializeField, Range(0.01f, 0.5f)] private float zoneHalfWidth = 0.1f;
        [Tooltip("Meter gained per full turn of the handle.")]
        [SerializeField, Min(0.01f)] private float fillPerTurn = 0.12f;
        [Tooltip("Obsolete: letting go now only finishes once the meter has reached the green zone.")]
        [SerializeField, Range(0f, 1f)] private float minLevelToFinish = 0.15f;
        [Tooltip("Distance from the target at which quality reaches 0.")]
        [SerializeField, Min(0.01f)] private float qualityFalloff = 0.35f;

        [Header("Sound follows the crank")]
        [Tooltip("Crank speed (degrees/s) that sounds like full-speed grinding.")]
        [SerializeField, Min(1f)] private float fullSpeedDegrees = 720f;
        [Tooltip("Seconds without crank movement before the grinding sound fades out.")]
        [SerializeField, Min(0f)] private float stillSeconds = 0.12f;
        [SerializeField, Min(0.1f)] private float speedSmoothing = 10f;

        [Header("Why it went wrong")]
        [SerializeField] private string tooFineText = "Ground too fine — stop in the green.";
        [SerializeField] private string tooCoarseText = "Too coarse — grind a little more.";

        private CounterStation _counter;
        private FeedbackFx _fx;
        private UI.MinigameGauge _gauge;
        private float _grind;
        private float _lastAngle;
        private float _dustDegrees;
        private float _speed;
        private float _sinceMoved = float.MaxValue;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _fx = ctx.Fx;
            var handle = _counter.GrinderHandle;
            handle.DragStarted += OnDragStarted;
            handle.Dragged += OnDragged;
            handle.DragEnded += OnDragEnded;
            handle.Interactable = true;
            NudgeTarget = handle.transform;
            _counter.SetCoffeeBagOpen(true); // beans come out of the open bag

            _grind = 0f;
            _gauge = ctx.Gauge;
            if (_gauge != null)
                _gauge.Show(_counter.GrinderRenderer, ctx.WorldCamera, new Vector2(targetLevel - zoneHalfWidth, targetLevel + zoneHalfWidth),
                    RedAbove(targetLevel, qualityFalloff), gaugeIcon);
        }

        private void OnDestroy()
        {
            if (_counter == null) return;
            var handle = _counter.GrinderHandle;
            handle.DragStarted -= OnDragStarted;
            handle.Dragged -= OnDragged;
            handle.DragEnded -= OnDragEnded;
            AudioManager.StopLoop(SfxId.Grind, 0.1f);
            _counter.SetCoffeeBagOpen(false);
            if (_gauge != null) _gauge.Hide();
        }

        private void Update()
        {
            // Grinding sound only while the crank turns; volume and pitch (±10%) follow its speed.
            _sinceMoved += Time.deltaTime;
            var moving = _sinceMoved <= stillSeconds && !IsComplete;
            if (!moving)
            {
                _speed = 0f;
                AudioManager.StopLoop(SfxId.Grind, 0.1f);
                return;
            }

            AudioManager.StartLoop(SfxId.Grind, 0.05f); // no-op while already playing / fading in
            AudioManager.SetLoopVolume(SfxId.Grind, Mathf.Lerp(0.45f, 1f, _speed));
            AudioManager.SetLoopPitch(SfxId.Grind, Mathf.Lerp(0.9f, 1.1f, _speed));
        }

        private float AngleTo(Vector2 world)
        {
            var offset = world - (Vector2)_counter.GrinderArm.position;
            return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        }

        private void OnDragStarted(Vector2 world)
        {
            _lastAngle = AngleTo(world);
        }

        private void OnDragged(Vector2 world)
        {
            if (IsComplete) return;

            var angle = AngleTo(world);
            var delta = Mathf.Abs(Mathf.DeltaAngle(_lastAngle, angle));
            _lastAngle = angle;

            _counter.GrinderArm.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (delta > 0.5f)
            {
                var instant = Mathf.Clamp01(delta / Mathf.Max(Time.deltaTime, 0.0001f) / fullSpeedDegrees);
                _speed = Mathf.Lerp(_speed, instant, 1f - Mathf.Exp(-speedSmoothing * Time.deltaTime));
                _sinceMoved = 0f;
            }
            _grind = Mathf.Clamp01(_grind + delta / 360f * fillPerTurn);
            if (_gauge != null) _gauge.SetValue(_grind);

            // Coffee dust and a little shake every so often while grinding.
            _dustDegrees += delta;
            if (_dustDegrees >= 60f)
            {
                _dustDegrees = 0f;
                if (_fx != null)
                {
                    // The grounds go somewhere: in an arc from the grinder into the V60 on the cup.
                    var grounds = new Color(0.4f, 0.26f, 0.16f);
                    var grinder = _counter.GrinderRenderer.bounds;
                    var outlet = new Vector3(grinder.max.x - 0.15f, grinder.center.y - grinder.extents.y * 0.35f, 0f);
                    if (_counter.DripperOnMug) _fx.Throw(outlet, _counter.DripperTop, grounds, 3);
                    else _fx.Burst(_counter.GrinderArm.position + Vector3.down * 1.9f, grounds, 3, 0.6f, 0.05f);
                    _fx.Punch(_counter.GrinderArm.parent, 0.08f);
                }

                // Grounds pile up in the V60 as soon as some have fallen in.
                if (_grind >= 0.25f) _counter.SetDripperGrounds(true);
            }
            if (_grind >= 1f) Finish();
        }

        private void OnDragEnded(Vector2 world)
        {
            _sinceMoved = float.MaxValue;
            AudioManager.StopLoop(SfxId.Grind, 0.1f);
            // Let go before the green zone and you simply keep grinding; inside or past it, that's the grind.
            if (!IsComplete && _grind >= Mathf.Max(minLevelToFinish, targetLevel - zoneHalfWidth)) Finish();
        }

        private void Finish()
        {
            _counter.GrinderHandle.Interactable = false;
            _counter.SetDripperGrounds(true);
            AudioManager.StopLoop(SfxId.Grind, 0.1f);
            if (_gauge != null) _gauge.Release();
            var quality = 1f - Mathf.Abs(_grind - targetLevel) / qualityFalloff;
            if (ShouldExplain(quality)) Explain(_fx, _grind > targetLevel ? tooFineText : tooCoarseText, _counter.GrinderRenderer);
            Complete(quality);
        }
    }
}
