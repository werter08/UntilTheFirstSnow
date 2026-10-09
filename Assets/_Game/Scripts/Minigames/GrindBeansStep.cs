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
        [Header("Own gauge")]
        [SerializeField] private SpriteBar meter;
        [SerializeField] private SpriteRenderer targetZone;

        [Header("Tuning")]
        [SerializeField, Range(0f, 1f)] private float targetLevel = 0.55f;
        [SerializeField, Range(0.01f, 0.5f)] private float zoneHalfWidth = 0.1f;
        [Tooltip("Meter gained per full turn of the handle.")]
        [SerializeField, Min(0.01f)] private float fillPerTurn = 0.12f;
        [Tooltip("Obsolete: letting go now only finishes once the meter has reached the green zone.")]
        [SerializeField, Range(0f, 1f)] private float minLevelToFinish = 0.15f;
        [Tooltip("Distance from the target at which quality reaches 0.")]
        [SerializeField, Min(0.01f)] private float qualityFalloff = 0.35f;

        private CounterStation _counter;
        private FeedbackFx _fx;
        private float _lastAngle;
        private float _dustDegrees;

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

            meter.SetValue(0f);
            var height = meter.MaxHeight;
            targetZone.size = new Vector2(targetZone.size.x, zoneHalfWidth * 2f * height);
            targetZone.transform.localPosition = new Vector3(targetZone.transform.localPosition.x, targetLevel * height, 0f);
        }

        private void OnDestroy()
        {
            if (_counter == null) return;
            var handle = _counter.GrinderHandle;
            handle.DragStarted -= OnDragStarted;
            handle.Dragged -= OnDragged;
            handle.DragEnded -= OnDragEnded;
            AudioManager.StopLoop(SfxId.Grind);
        }

        private float AngleTo(Vector2 world)
        {
            var offset = world - (Vector2)_counter.GrinderArm.position;
            return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        }

        private void OnDragStarted(Vector2 world)
        {
            _lastAngle = AngleTo(world);
            AudioManager.StartLoop(SfxId.Grind);
        }

        private void OnDragged(Vector2 world)
        {
            if (IsComplete) return;

            var angle = AngleTo(world);
            var delta = Mathf.Abs(Mathf.DeltaAngle(_lastAngle, angle));
            _lastAngle = angle;

            _counter.GrinderArm.localRotation = Quaternion.Euler(0f, 0f, angle);
            meter.SetValue(meter.Value + delta / 360f * fillPerTurn);

            // Coffee dust and a little shake every so often while grinding.
            _dustDegrees += delta;
            if (_dustDegrees >= 60f)
            {
                _dustDegrees = 0f;
                if (_fx != null)
                {
                    _fx.Burst(_counter.GrinderArm.position + Vector3.down * 1.9f, new Color(0.4f, 0.26f, 0.16f), 3, 0.6f, 0.05f);
                    _fx.Punch(_counter.GrinderArm.parent, 0.08f);
                }
            }
            if (meter.Value >= 1f) Finish();
        }

        private void OnDragEnded(Vector2 world)
        {
            AudioManager.StopLoop(SfxId.Grind);
            // Let go before the green zone and you simply keep grinding; inside or past it, that's the grind.
            if (!IsComplete && meter.Value >= Mathf.Max(minLevelToFinish, targetLevel - zoneHalfWidth)) Finish();
        }

        private void Finish()
        {
            _counter.GrinderHandle.Interactable = false;
            _counter.SetDripperGrounds(true);
            AudioManager.StopLoop(SfxId.Grind);
            Complete(1f - Mathf.Abs(meter.Value - targetLevel) / qualityFalloff);
        }
    }
}
