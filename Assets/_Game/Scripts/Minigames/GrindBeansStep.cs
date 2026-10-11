using Courtyard.Audio;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Pick the grinder up and hold it over the V60 on the cup (it stays there, tilted, one hand holding it still); then
    /// turn the handle in circles with the other hand to fill the grind meter and let go inside the "medium" zone — the
    /// grounds fall straight into the V60. Then the grinder goes back. Quality = how close the grind is to the target.
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

        [Header("Showing how")]
        [Tooltip("Until the player starts cranking, the handle turns once by itself every this many seconds.")]
        [SerializeField, Min(0.5f)] private float demoEverySeconds = 2f;
        [SerializeField, Min(0.1f)] private float demoTurnSeconds = 1f;
        [Tooltip("Pointer travel (world units) that counts as one full turn when the player scrubs instead of circling.")]
        [SerializeField, Min(0.1f)] private float travelPerTurn = 2.2f;

        [Header("Why it went wrong")]
        [SerializeField] private string tooFineText = "Ground too fine — stop in the green.";
        [SerializeField] private string tooCoarseText = "Too coarse — grind a little more.";

        [Header("Carrying it over the cup")]
        [Tooltip("How far (world units) from the V60 the grinder may be let go and still settle over it.")]
        [SerializeField, Min(0.2f)] private float settleReach = 1.6f;
        [SerializeField, Min(0f)] private float settleSeconds = 0.25f;
        [SerializeField, Min(0f)] private float returnSeconds = 0.4f;

        private CounterStation _counter;
        private FeedbackFx _fx;
        private UI.MinigameGauge _gauge;
        private MinigameContext _ctx;
        private GameObject _holdingHand;
        private bool _overCup;
        private Vector3 _grab;
        private Coroutine _carry;
        private float _grind;
        private float _lastAngle;
        private float _dustDegrees;
        private float _speed;
        private float _sinceMoved = float.MaxValue;
        private Vector2 _lastPointer;
        private bool _cranking;
        private bool _everCranked;
        private Coroutine _demo;

        public override void Begin(MinigameContext ctx)
        {
            _ctx = ctx;
            _counter = ctx.Counter;
            _fx = ctx.Fx;
            var handle = _counter.GrinderHandle;
            handle.DragStarted += OnDragStarted;
            handle.Dragged += OnDragged;
            handle.DragEnded += OnDragEnded;
            ResultAt = _counter.GrinderRenderer;
            _counter.SetCoffeeBagOpen(true); // beans come out of the open bag
            _grind = 0f;
            _gauge = ctx.Gauge;

            // First: carry the grinder over the V60 (no V60 on the cup, or no grab handle: grind where it stands).
            var grab = _counter.GrinderGrab;
            if (grab != null && _counter.DripperOnMug)
            {
                grab.DragStarted += OnGrinderGrabbed;
                grab.Dragged += OnGrinderCarried;
                grab.DragEnded += OnGrinderDropped;
                grab.Interactable = true;
                handle.Interactable = false;
                NudgeTarget = grab.transform;
            }
            else StartCranking();
        }

        public bool OverCup => _overCup;

        private void OnGrinderGrabbed(Vector2 world)
        {
            if (_overCup) return;
            if (_carry != null) StopCoroutine(_carry);
            _grab = _counter.Grinder.position - (Vector3)world;
        }

        private void OnGrinderCarried(Vector2 world)
        {
            if (_overCup) return;
            var t = _counter.Grinder;
            t.position = new Vector3(world.x + _grab.x, world.y + _grab.y, t.position.z);
            if (NearCup()) _carry = StartCoroutine(SettleOverCup()); // over the V60: it settles there by itself
        }

        private void OnGrinderDropped(Vector2 world)
        {
            if (_overCup) return;
            _carry = StartCoroutine(NearCup() ? SettleOverCup() : _counter.ReturnGrinder(returnSeconds));
        }

        private bool NearCup()
        {
            var outlet = _counter.GrinderOutlet;
            var top = _counter.DripperTop;
            return Mathf.Abs(outlet.x - top.x) <= settleReach && outlet.y >= top.y - 0.6f;
        }

        /// <summary>Over the V60, tilted; a hand holds it still while the other turns the crank.</summary>
        private System.Collections.IEnumerator SettleOverCup()
        {
            _overCup = true;
            _counter.GrinderGrab.Interactable = false;
            yield return _counter.HoldGrinderOverCup(settleSeconds);
            AudioManager.Play(SfxId.PutDown);
            var body = _counter.GrinderRenderer.bounds;
            if (_ctx.Hands != null)
                _holdingHand = _ctx.Hands.HoldStill(_counter.Grinder, new Vector3(body.min.x + 0.15f, body.center.y - 0.2f, 0f), Core.HandPose.Grip);
            StartCranking();
        }

        private void StartCranking()
        {
            _overCup = true;
            var handle = _counter.GrinderHandle;
            handle.Interactable = true;
            NudgeTarget = handle.transform;

            // The whole grinder works as the crank once it's over the cup: grab it anywhere and move.
            var grab = _counter.GrinderGrab;
            if (grab != null && grab != handle)
            {
                grab.DragStarted += OnDragStarted;
                grab.Dragged += OnDragged;
                grab.DragEnded += OnDragEnded;
                grab.Interactable = true;
            }

            _demo = StartCoroutine(ShowHowToTurn());
            if (_gauge != null)
                _gauge.Show(_counter.GrinderRenderer, _ctx.WorldCamera, new Vector2(targetLevel - zoneHalfWidth, targetLevel + zoneHalfWidth),
                    RedAbove(targetLevel, qualityFalloff), gaugeIcon);
        }

        private void OnDestroy()
        {
            if (_counter == null) return;
            var handle = _counter.GrinderHandle;
            handle.DragStarted -= OnDragStarted;
            handle.Dragged -= OnDragged;
            handle.DragEnded -= OnDragEnded;
            var grab = _counter.GrinderGrab;
            if (grab != null)
            {
                grab.DragStarted -= OnGrinderGrabbed;
                grab.Dragged -= OnGrinderCarried;
                grab.DragEnded -= OnGrinderDropped;
                grab.DragStarted -= OnDragStarted;
                grab.Dragged -= OnDragged;
                grab.DragEnded -= OnDragEnded;
                grab.Interactable = false;
            }

            if (_holdingHand != null) Destroy(_holdingHand);
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

        /// <summary>Until the player cranks, the handle turns a full circle by itself now and then: "do this".</summary>
        private System.Collections.IEnumerator ShowHowToTurn()
        {
            var arm = _counter.GrinderArm;
            while (!_everCranked && !IsComplete)
            {
                yield return new WaitForSeconds(demoEverySeconds);
                if (_everCranked || _cranking || IsComplete) yield break;
                var from = arm.eulerAngles.z;
                yield return Core.Tweens.Run(demoTurnSeconds, t =>
                {
                    if (!_cranking) arm.rotation = Quaternion.Euler(0f, 0f, from - 360f * Mathf.SmoothStep(0f, 1f, t));
                });
            }
        }

        private void OnDragStarted(Vector2 world)
        {
            if (!_overCup || IsComplete) return;
            _cranking = true;
            _lastAngle = AngleTo(world);
            _lastPointer = world;
        }

        private void OnDragged(Vector2 world)
        {
            if (IsComplete || !_cranking) return;

            // Circling turns the handle; any other hand movement counts too (by distance), so no one gets stuck.
            var angle = AngleTo(world);
            var turned = Mathf.Abs(Mathf.DeltaAngle(_lastAngle, angle));
            var travelled = Vector2.Distance(_lastPointer, world) / travelPerTurn * 360f;
            var delta = Mathf.Max(turned, travelled);
            _lastAngle = angle;
            _lastPointer = world;
            if (delta > 0.5f) _everCranked = true;

            _counter.GrinderArm.rotation = Quaternion.Euler(0f, 0f, angle); // world angle: the grinder itself may be tilted
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
                    if (_counter.DripperOnMug) _fx.Throw(_counter.GrinderOutlet, _counter.DripperTop, grounds, 3, 0.3f);
                    else _fx.Burst(_counter.GrinderArm.position + Vector3.down * 1.9f, grounds, 3, 0.6f, 0.05f);
                    _fx.Punch(_counter.GrinderArm.parent, 0.08f);
                    CameraShake.Kick(0.012f, 0.12f); // the burr bites
                }

                // Grounds pile up in the V60 as soon as some have fallen in.
                if (_grind >= 0.25f) _counter.SetDripperGrounds(true);
            }
            if (_grind >= 1f) Finish();
        }

        private void OnDragEnded(Vector2 world)
        {
            if (!_cranking) return;
            _cranking = false;
            _sinceMoved = float.MaxValue;
            AudioManager.StopLoop(SfxId.Grind, 0.1f);
            // Let go before the green zone and you simply keep grinding; inside or past it, that's the grind.
            if (!IsComplete && _grind >= Mathf.Max(minLevelToFinish, targetLevel - zoneHalfWidth)) Finish();
        }

        private void Finish()
        {
            // Done: the grinder goes back to its place (on the counter's coroutine — this step is destroyed soon).
            if (_holdingHand != null) Destroy(_holdingHand);
            _counter.StartCoroutine(_counter.ReturnGrinder(returnSeconds));
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
