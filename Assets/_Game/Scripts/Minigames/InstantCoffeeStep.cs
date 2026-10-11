using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Day 1 instant coffee, done by hand:
    /// 1) carry spoonfuls from the jar into the cup one by one — the cup visibly fills with coffee (this can start while
    ///    the kettle is still heating: the step starts together with KettleBoil);
    /// 2) once the water has boiled, pick up the kettle, carry it to the cup and pour while the spout is over it (a
    ///    gauge shows how full the cup is); let go to put it back.
    /// Without the spoon (<see cref="useSpoon"/> off) it is just "pour the hot water to the line" (Days 10 and 30).
    /// Wrong spoonfuls spoil it. Quality = how close to the line.
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

        [Tooltip("Off: no spoon or jar — only pour the hot water to the line.")]
        [SerializeField] private bool useSpoon = true;
        [SerializeField] private Sprite gaugeIcon;
        [Tooltip("The bag of beans steps aside while this runs (her coffee: the jar stands in its spot).")]
        [SerializeField] private bool hideCoffeeBag;

        [Header("Tuning")]
        [SerializeField, Min(0)] private int targetSpoons = 2;
        [SerializeField, Min(0.01f)] private float pourPerSecond = 0.35f;
        [SerializeField, Range(0f, 1f)] private float targetFill = 0.85f;
        [Tooltip("Fill distance from the line at which quality reaches 0.")]
        [SerializeField, Min(0.01f)] private float fillTolerance = 0.3f;
        [Tooltip("Putting the kettle back below this fill doesn't end the step.")]
        [SerializeField, Range(0f, 1f)] private float minFillToFinish = 0.15f;
        [Tooltip("How far (sideways) from the cup's opening the spout may be and still pour into it.")]
        [SerializeField, Min(0.1f)] private float pourReach = 0.5f;
        [SerializeField, Min(0.1f)] private float kettleFollow = 14f;
        [SerializeField, Min(0f)] private float spoonReturnSeconds = 0.2f;
        [SerializeField, Min(0f)] private float kettleReturnSeconds = 0.25f;

        [Header("Look")]
        [SerializeField] private Color waterColor = new(0.75f, 0.85f, 0.95f);
        [SerializeField] private Color coffeeColor = new(0.35f, 0.2f, 0.1f);

        [Header("Spoon counter beside the cup")]
        [Tooltip("{0} = spoons in, {1} = spoons needed.")]
        [SerializeField] private string spoonCountText = "Coffee: {0} / {1} spoons";
        [Tooltip("Dark ink: the label sits on the light counter.")]
        [SerializeField] private Color countColour = new(0.24f, 0.16f, 0.12f);
        [SerializeField] private Color countDoneColour = new(0.16f, 0.45f, 0.14f);
        [SerializeField] private Color countOverColour = new(0.7f, 0.16f, 0.1f);
        [SerializeField, Min(0.1f)] private float countScale = 0.7f;
        [Tooltip("How far (sideways) from the cup a loaded spoon tips its coffee in — as soon as it gets there.")]
        [SerializeField, Min(0.1f)] private float spoonReach = 0.9f;

        [Header("Why it went wrong")]
        [SerializeField] private string tooMuchCoffeeText = "Too much coffee — just two spoons.";
        [SerializeField] private string tooLittleCoffeeText = "Not enough coffee — two spoons.";
        [SerializeField] private string tooMuchWaterText = "Too much water — stop at the line.";
        [SerializeField] private string tooLittleWaterText = "Not enough water — fill to the line.";

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
        private bool _kettleReady = true;
        private TMPro.TMP_Text _spoonCount;
        private MinigameContext _ctx;

        public override void Begin(MinigameContext ctx)
        {
            _ctx = ctx;
            _counter = ctx.Counter;
            if (hideCoffeeBag)
            {
                if (useSpoon) StandJarWhereTheBagIs();
                _counter.SetCoffeeBagVisible(false);
            }
            _fx = ctx.Fx;
            _camera = ctx.WorldCamera;
            if (!useSpoon)
            {
                spoon.gameObject.SetActive(false);
                jar.gameObject.SetActive(false);
            }

            // The spoon lying on the counter becomes the one in the player's hand.
            var resting = _counter.RestingSpoon;
            if (useSpoon && resting != null && resting.gameObject.activeInHierarchy)
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
            _counter.SetMugDrink(useSpoon ? MugDrink.Instant : MugDrink.Black, coffeeColor);
            ShowLoad(false);
            if (useSpoon)
            {
                _counter.ShowSpoons(0);
                // How many spoons: a counter centred under the cup that ticks up (green when right, red when too many).
                var mug = _counter.MugBody.bounds;
                _spoonCount = _fx.Label(string.Empty, new Vector3(mug.center.x, mug.min.y - 0.4f, 0f), countColour, countScale);
                UpdateSpoonCount();
            }
            NudgeTarget = useSpoon ? spoon.transform : _counter.KettleTarget.transform;

            // Started with the kettle step: spoon now, pour once the water has boiled (OnPreviousDone).
            _kettleReady = !StartsWithPrevious;
            if (_kettleReady) ReadyToPour();
        }

        /// <summary>Her jar takes the bag of beans' spot on the counter (wherever the team put the bag), standing on its base.</summary>
        private void StandJarWhereTheBagIs()
        {
            var bag = _counter.CoffeeBagBounds;
            if (!bag.HasValue) return;
            var art = jar.GetComponentInChildren<SpriteRenderer>();
            var own = art != null ? art.bounds : jar.bounds;
            jar.transform.position += new Vector3(bag.Value.center.x - own.center.x, bag.Value.min.y - own.min.y, 0f);
        }

        public override void OnPreviousDone()
        {
            if (_kettleReady) return;
            _kettleReady = true;
            ReadyToPour();
        }

        /// <summary>The water is hot: the kettle can be picked up, and the gauge shows how full the cup is.</summary>
        private void ReadyToPour()
        {
            _counter.KettleTarget.Interactable = true;
            _counter.ShowFillLine(targetFill);
            if (!useSpoon || _spoons >= targetSpoons) NudgeTarget = _counter.KettleTarget.transform;
            var good = (1f - 0.6f) * fillTolerance; // the green: quality ≥ 0.6
            _ctx.Gauge?.Show(_counter.MugBody, _ctx.WorldCamera, new Vector2(targetFill - good, Mathf.Min(1f, targetFill + good)),
                RedAbove(targetFill, fillTolerance), gaugeIcon, _counter.MugLevel);
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
            if (hideCoffeeBag) _counter.SetCoffeeBagVisible(true);
            _counter.KettleTarget.Pressed -= OnKettlePressed;
            _counter.KettleTarget.Released -= OnKettleReleased;
            _counter.HideFillLine();
            _counter.KettleView.SetPouring(false);
            if (_ctx?.Gauge != null) _ctx.Gauge.Hide();
            if (_spoonCount != null) Destroy(_spoonCount.gameObject);
            _counter.KettleStream.enabled = false;
            _counter.Kettle.position = _counter.KettleRest;
            AudioManager.StopTrack(SfxId.Pour);
            if (useSpoon) _counter.SetRestingSpoon(_counter.RestingSpoon != null); // back on the counter
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
            if (_spoonLoaded)
            {
                // The coffee goes in as soon as the spoon reaches the cup — no need to aim and let go.
                if (OverCup(world)) TipSpoonful();
                return;
            }

            if (!jar.OverlapPoint(world) && !jar.OverlapPoint(spoon.transform.position)) return;

            _spoonLoaded = true;
            ShowLoad(true);
            AudioManager.Play(SfxId.Spoon);
            _fx.Punch(jar.transform, 0.6f);
            _fx.Burst(world, coffeeColor, 4, 0.7f, 0.05f);
        }

        private void OnSpoonDragEnded(Vector2 world)
        {
            if (_spoonLoaded && OverCup(world)) TipSpoonful();

            var from = spoon.transform.position;
            _spoonReturn = StartCoroutine(Tweens.Run(spoonReturnSeconds, t => spoon.transform.position = Vector3.Lerp(from, _spoonRest, t)));
        }

        private bool OverCup(Vector2 world) =>
            _counter.IsOverMug(world) || _counter.IsAboveMug(world, spoonReach) || _counter.IsOverMug(spoon.transform.position);

        private void TipSpoonful()
        {
            _spoonLoaded = false;
            ShowLoad(false);
            _spoons++;
            _counter.ShowSpoons(_spoons);
            UpdateSpoonCount();
            if (_spoonCount != null) _fx.Punch(_spoonCount.transform, 0.6f);
            AudioManager.Play(SfxId.SpoonTink);
            _fx.Punch(_counter.Mug.transform);
            _fx.Burst(_counter.MugTop, coffeeColor, 8, 0.9f, 0.06f);

            // Too many spoons spoil it (judged when the water is poured); the player carries on either way.
            if (_spoons >= targetSpoons && _kettleReady) NudgeTarget = _counter.KettleTarget.transform;
        }

        private void UpdateSpoonCount()
        {
            if (_spoonCount == null) return;
            _spoonCount.text = string.Format(spoonCountText, _spoons, targetSpoons);
            _spoonCount.color = _spoons == targetSpoons ? countDoneColour : _spoons > targetSpoons ? countOverColour : countColour;
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
            if (IsComplete || !_kettleReady) return; // still heating: the kettle stays on its base
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
            if (_ctx.Gauge != null) _ctx.Gauge.SetValue(_counter.MugLevel);
            _splashTimer -= Time.deltaTime;
            if (_splashTimer <= 0f)
            {
                _splashTimer = 0.12f;
                _fx.Burst(_counter.MugTop, waterColor, 2, 0.8f, 0.05f, 1.2f);
            }

            // At the line (the middle of the green): that's the cup — it's accepted right away.
            if (_counter.MugLevel >= targetFill || _counter.MugLevel >= 1f) Finish();
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
            if (_ctx.Gauge != null) _ctx.Gauge.Release();

            var mug = _counter.MugBody;
            if (useSpoon && _spoons != targetSpoons)
            {
                Explain(_fx, _spoons > targetSpoons ? tooMuchCoffeeText : tooLittleCoffeeText, mug);
                Ruin(); // too strong or too weak: a cold, bitter cup
                return;
            }

            var quality = 1f - Mathf.Abs(_counter.MugLevel - targetFill) / fillTolerance;
            if (ShouldExplain(quality)) Explain(_fx, _counter.MugLevel > targetFill ? tooMuchWaterText : tooLittleWaterText, mug);
            Complete(quality);
        }
    }
}
