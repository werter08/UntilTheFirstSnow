using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Day 1 instant coffee, done by hand:
    /// 1) carry spoonfuls from the jar into the cup one by one — the cup visibly fills with coffee;
    /// 2) pick up the kettle, carry it to the cup and pour while the spout is over it; let go to put it back.
    /// Too many or too few spoonfuls ruins the coffee and the day restarts. Quality = how close to the line.
    /// </summary>
    public sealed class InstantCoffeeStep : MinigameStep
    {
        [Header("Own props")]
        [SerializeField] private DragTarget spoon;
        [Tooltip("Coffee on the spoon; shown while the spoon is loaded.")]
        [SerializeField] private SpriteRenderer spoonLoad;
        [SerializeField] private Collider2D jar;
        [Tooltip("Drawn spoon, empty and with coffee (optional: without them the Load overlay shows the coffee).")]
        [SerializeField] private Sprite spoonEmpty;
        [SerializeField] private Sprite spoonFull;

        [Header("Tuning")]
        [SerializeField, Min(1)] private int targetSpoons = 2;
        [SerializeField, Min(0.01f)] private float pourPerSecond = 0.35f;
        [SerializeField, Range(0f, 1f)] private float targetFill = 0.85f;
        [Tooltip("Fill distance from the line at which quality reaches 0.")]
        [SerializeField, Min(0.01f)] private float fillTolerance = 0.3f;
        [Tooltip("Putting the kettle back below this fill doesn't end the step.")]
        [SerializeField, Range(0f, 1f)] private float minFillToFinish = 0.15f;
        [Tooltip("How far (sideways) from the cup's opening the spout may be and still pour into it.")]
        [SerializeField, Min(0.1f)] private float pourReach = 0.55f;
        [SerializeField, Min(0.1f)] private float kettleFollow = 14f;
        [SerializeField, Min(0f)] private float spoonReturnSeconds = 0.2f;
        [SerializeField, Min(0f)] private float kettleReturnSeconds = 0.25f;

        [Header("Look")]
        [SerializeField] private Color waterColor = new(0.75f, 0.85f, 0.95f);
        [SerializeField] private Color coffeeColor = new(0.35f, 0.2f, 0.1f);

        private CounterStation _counter;
        private FeedbackFx _fx;
        private Camera _camera;
        private Coroutine _spoonReturn;
        private Coroutine _kettleMove;
        private Vector3 _spoonRest;
        private Vector2 _grabOffset;
        private bool _spoonLoaded;
        private int _spoons;
        private bool _holdingKettle;
        private bool _streamOn;
        private float _splashTimer;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _fx = ctx.Fx;
            _camera = ctx.WorldCamera;
            // The spoon lying on the counter becomes the one in the player's hand.
            var resting = _counter.RestingSpoon;
            if (resting != null && resting.gameObject.activeInHierarchy)
            {
                spoon.transform.SetPositionAndRotation(resting.position, resting.rotation);
                _counter.SetRestingSpoon(false);
            }

            _spoonRest = spoon.transform.position;
            spoon.DragStarted += OnSpoonDragStarted;
            spoon.Dragged += OnSpoonDragged;
            spoon.DragEnded += OnSpoonDragEnded;
            _counter.KettleTarget.Pressed += OnKettlePressed;
            _counter.KettleTarget.Released += OnKettleReleased;
            _counter.KettleTarget.Interactable = true;

            _counter.SetMugGauge(true);
            _counter.ShowFillLine(targetFill);
            _counter.SetMugDrink(MugDrink.Instant, coffeeColor);
            ShowLoad(false);
            NudgeTarget = spoon.transform;
            _counter.ShowSpoons(0);
        }

        private void OnDestroy()
        {
            if (spoon != null)
            {
                spoon.DragStarted -= OnSpoonDragStarted;
                spoon.Dragged -= OnSpoonDragged;
                spoon.DragEnded -= OnSpoonDragEnded;
            }

            if (_counter == null) return;
            _counter.KettleTarget.Pressed -= OnKettlePressed;
            _counter.KettleTarget.Released -= OnKettleReleased;
            _counter.HideFillLine();
            _counter.SetMugGauge(false);
            _counter.KettleView.SetPouring(false);
            _counter.KettleStream.enabled = false;
            _counter.Kettle.position = _counter.KettleRest;
            AudioManager.StopTrack(SfxId.Pour);
            _counter.SetRestingSpoon(_counter.RestingSpoon != null); // back on the counter
        }

        // ---------------------------------------------------------------- spoon

        private void OnSpoonDragStarted(Vector2 world)
        {
            if (_spoonReturn != null) StopCoroutine(_spoonReturn);
            _grabOffset = (Vector2)spoon.transform.position - world;
        }

        private void OnSpoonDragged(Vector2 world)
        {
            spoon.transform.position = world + _grabOffset;
            if (_spoonLoaded || !jar.OverlapPoint(world)) return;

            _spoonLoaded = true;
            ShowLoad(true);
            AudioManager.Play(SfxId.Spoon);
            _fx.Punch(jar.transform, 0.6f);
            _fx.Burst(world, coffeeColor, 4, 0.7f, 0.05f);
        }

        private void OnSpoonDragEnded(Vector2 world)
        {
            if (_spoonLoaded && (_counter.IsOverMug(world) || _counter.IsAboveMug(world, 0.7f)))
            {
                _spoonLoaded = false;
                ShowLoad(false);
                _spoons++;
                _counter.ShowSpoons(_spoons);
                AudioManager.Play(SfxId.SpoonTink);
                _fx.Punch(_counter.Mug.transform);
                _fx.Burst(_counter.MugTop, coffeeColor, 8, 0.9f, 0.06f);

                if (_spoons > targetSpoons)
                {
                    Ruin(); // far too strong
                    return;
                }

                if (_spoons == targetSpoons) NudgeTarget = _counter.KettleTarget.transform;
            }

            var from = spoon.transform.position;
            _spoonReturn = StartCoroutine(Tweens.Run(spoonReturnSeconds, t => spoon.transform.position = Vector3.Lerp(from, _spoonRest, t)));
        }

        /// <summary>Coffee on the spoon: the drawn full spoon when there is one, otherwise the Load overlay.</summary>
        private void ShowLoad(bool loaded)
        {
            var drawn = spoonEmpty != null && spoonFull != null;
            if (drawn) spoon.GetComponent<SpriteRenderer>().sprite = loaded ? spoonFull : spoonEmpty;
            spoonLoad.enabled = loaded && !drawn;
        }

        // ---------------------------------------------------------------- kettle

        private void OnKettlePressed()
        {
            if (IsComplete) return;
            if (_kettleMove != null) StopCoroutine(_kettleMove);
            _holdingKettle = true;
            spoon.Interactable = false;
            _counter.KettleView.SetPouring(true);
        }

        private void OnKettleReleased()
        {
            if (!_holdingKettle) return;
            _holdingKettle = false;
            SetStream(false);
            ReturnKettle();
            spoon.Interactable = !IsComplete;
            if (!IsComplete && _counter.MugLevel >= minFillToFinish) Finish();
        }

        private void Update()
        {
            if (!_holdingKettle || IsComplete) return;

            var pointer = PointerWorld();
            var kettle = _counter.Kettle;
            var follow = 1f - Mathf.Exp(-kettleFollow * Time.deltaTime);
            kettle.position = Vector3.Lerp(kettle.position, new Vector3(pointer.x, pointer.y, kettle.position.z), follow);

            var pouring = _counter.IsAboveMug(kettle.position, pourReach);
            SetStream(pouring);
            if (!pouring) return;

            _counter.SetMugLevel(_counter.MugLevel + pourPerSecond * Time.deltaTime);
            _splashTimer -= Time.deltaTime;
            if (_splashTimer <= 0f)
            {
                _splashTimer = 0.12f;
                _fx.Burst(_counter.MugTop, waterColor, 2, 0.8f, 0.05f, 1.2f);
            }

            if (_counter.MugLevel >= 1f) Finish(); // overflowing
        }

        private void SetStream(bool on)
        {
            if (_streamOn == on) return;
            _streamOn = on;
            _counter.KettleStream.enabled = on;
            // The pour is a one-shot: pausing keeps its place, so tipping the kettle again continues the same pour.
            if (on) AudioManager.ResumeTrack(SfxId.Pour);
            else AudioManager.PauseTrack(SfxId.Pour);
        }

        private void ReturnKettle()
        {
            _counter.KettleView.SetPouring(false);
            var kettle = _counter.Kettle;
            var from = kettle.position;
            var rest = _counter.KettleRest;
            _kettleMove = StartCoroutine(Tweens.Run(kettleReturnSeconds, t => kettle.position = Vector3.Lerp(from, rest, t)));
        }

        private Vector2 PointerWorld()
        {
            var screen = Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
            return _camera.ScreenToWorldPoint(screen);
        }

        private void Finish()
        {
            if (IsComplete) return;
            _holdingKettle = false;
            SetStream(false);
            ReturnKettle();
            _counter.KettleTarget.Interactable = false;
            spoon.Interactable = false;

            if (_spoons != targetSpoons)
            {
                Ruin(); // too weak (or no coffee at all)
                return;
            }

            Complete(1f - Mathf.Abs(_counter.MugLevel - targetFill) / fillTolerance);
        }
    }
}
