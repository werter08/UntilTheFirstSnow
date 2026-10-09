using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Drag the spoon from the jar to the counter mug, then hold the kettle to pour up to the line.
    /// Quality = correct spoon count + how close the fill is to the line. Owns the jar and spoon.
    /// </summary>
    public sealed class InstantCoffeeStep : MinigameStep
    {
        [Header("Own props")]
        [SerializeField] private DragTarget spoon;
        [Tooltip("Coffee on the spoon; shown while the spoon is loaded.")]
        [SerializeField] private SpriteRenderer spoonLoad;
        [SerializeField] private Collider2D jar;

        [Header("Tuning")]
        [SerializeField, Min(1)] private int targetSpoons = 2;
        [SerializeField, Range(0f, 1f)] private float wrongSpoonScore = 0.5f;
        [SerializeField, Min(0.01f)] private float pourPerSecond = 0.35f;
        [SerializeField, Range(0f, 1f)] private float targetFill = 0.85f;
        [Tooltip("Fill distance from the line at which the fill score reaches 0.")]
        [SerializeField, Min(0.01f)] private float fillTolerance = 0.3f;
        [SerializeField, Range(0f, 1f)] private float spoonWeight = 0.5f;

        [SerializeField, Min(0f)] private float spoonReturnSeconds = 0.2f;
        [Tooltip("Where the kettle's spout goes while pouring, relative to the mug.")]
        [SerializeField] private Vector2 pourOffsetFromMug = new(0.15f, 1.05f);
        [SerializeField, Min(0f)] private float kettleMoveSeconds = 0.25f;

        [Header("Look")]
        [SerializeField] private Color waterColor = new(0.75f, 0.85f, 0.95f);
        [SerializeField] private Color coffeeColor = new(0.35f, 0.2f, 0.1f);
        [Tooltip("Kettle tint before / after the first spoon (white = untinted art).")]
        [SerializeField] private Color kettleIdleColor = Color.white;
        [SerializeField] private Color kettleReadyColor = Color.white;

        private CounterStation _counter;
        private FeedbackFx _fx;
        private Coroutine _spoonReturn;
        private Coroutine _kettleMove;
        private Vector3 _spoonRest;
        private Vector2 _grabOffset;
        private bool _spoonLoaded;
        private int _spoons;
        private bool _pouring;
        private bool _pourStarted;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _fx = ctx.Fx;
            _spoonRest = spoon.transform.position;
            spoon.DragStarted += OnSpoonDragStarted;
            spoon.Dragged += OnSpoonDragged;
            spoon.DragEnded += OnSpoonDragEnded;
            _counter.KettleTarget.Pressed += OnKettlePressed;
            _counter.KettleTarget.Released += OnKettleReleased;
            _counter.KettleTarget.Interactable = true;

            _counter.SetMugGauge(true);
            _counter.ShowFillLine(targetFill);
            spoonLoad.enabled = false;
            RefreshSpoonMarkers();
            RefreshKettle();
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
            _counter.KettleBody.color = kettleIdleColor;
            _counter.HideFillLine();
            _counter.SetMugGauge(false);
            _counter.KettleView.SetPouring(false);
            _counter.Kettle.position = _counter.KettleRest;
            AudioManager.StopLoop(SfxId.Pour);
        }

        private void Update()
        {
            if (!_pouring || IsComplete) return;

            _counter.SetMugLevel(_counter.MugLevel + pourPerSecond * Time.deltaTime);
            if (_counter.MugLevel >= 1f) Finish();
        }

        private void OnSpoonDragStarted(Vector2 world)
        {
            if (_spoonReturn != null) StopCoroutine(_spoonReturn);
            _grabOffset = (Vector2)spoon.transform.position - world;
        }

        private void OnSpoonDragged(Vector2 world)
        {
            spoon.transform.position = world + _grabOffset;
            if (!_spoonLoaded && jar.OverlapPoint(world))
            {
                _spoonLoaded = true;
                spoonLoad.enabled = true;
                AudioManager.Play(SfxId.Spoon);
                _fx.Punch(jar.transform, 0.6f);
            }
        }

        private void OnSpoonDragEnded(Vector2 world)
        {
            if (_spoonLoaded && _counter.IsOverMug(world))
            {
                _spoonLoaded = false;
                spoonLoad.enabled = false;
                _spoons++;
                RefreshSpoonMarkers();
                RefreshKettle();
                AudioManager.Play(SfxId.Spoon);
                _fx.Punch(_counter.Mug.transform);
            }

            var from = spoon.transform.position;
            _spoonReturn = StartCoroutine(Tweens.Run(spoonReturnSeconds, t => spoon.transform.position = Vector3.Lerp(from, _spoonRest, t)));
        }

        private void OnKettlePressed()
        {
            if (_spoons == 0 || IsComplete) return;

            _pouring = true;
            _pourStarted = true;
            AudioManager.StartLoop(SfxId.Pour);
            _counter.KettleView.SetPouring(true);
            MoveKettle((Vector2)_counter.Mug.transform.position + pourOffsetFromMug);
            spoon.Interactable = false;
            _counter.SetMugDrink(MugDrink.Instant, Color.Lerp(waterColor, coffeeColor, Mathf.Clamp01((float)_spoons / targetSpoons)));
        }

        private void OnKettleReleased()
        {
            _pouring = false;
            AudioManager.StopLoop(SfxId.Pour);
            ReturnKettle();
            if (_pourStarted && _counter.MugLevel > 0f) Finish();
        }

        private void Finish()
        {
            if (IsComplete) return;
            _pouring = false;
            _counter.KettleTarget.Interactable = false;
            AudioManager.StopLoop(SfxId.Pour);
            ReturnKettle();

            var spoonScore = _spoons == targetSpoons ? 1f : wrongSpoonScore;
            var fillScore = 1f - Mathf.Abs(_counter.MugLevel - targetFill) / fillTolerance;
            Complete(spoonWeight * spoonScore + (1f - spoonWeight) * Mathf.Clamp01(fillScore));
        }

        private void ReturnKettle()
        {
            _counter.KettleView.SetPouring(false);
            MoveKettle(_counter.KettleRest);
        }

        private void MoveKettle(Vector3 target)
        {
            if (_kettleMove != null) StopCoroutine(_kettleMove);
            var kettle = _counter.Kettle;
            var from = kettle.position;
            target.z = from.z;
            _kettleMove = StartCoroutine(Tweens.Run(kettleMoveSeconds, t => kettle.position = Vector3.Lerp(from, target, t)));
        }

        private void RefreshSpoonMarkers() => _counter.ShowSpoons(_spoons);

        private void RefreshKettle() => _counter.KettleBody.color = _spoons > 0 ? kettleReadyColor : kettleIdleColor;
    }
}
