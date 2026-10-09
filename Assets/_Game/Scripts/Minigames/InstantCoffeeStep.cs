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

        [Header("Look")]
        [SerializeField] private Color waterColor = new(0.75f, 0.85f, 0.95f);
        [SerializeField] private Color coffeeColor = new(0.35f, 0.2f, 0.1f);
        [SerializeField] private Color kettleIdleColor = new(0.6f, 0.6f, 0.65f);
        [SerializeField] private Color kettleReadyColor = new(0.85f, 0.85f, 0.9f);

        private CounterStation _counter;
        private FeedbackFx _fx;
        private Coroutine _spoonReturn;
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

            var fill = _counter.MugFill;
            var line = _counter.MugFillLine;
            line.gameObject.SetActive(true);
            line.localPosition = new Vector3(line.localPosition.x, targetFill * fill.MaxHeight, 0f);
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
            _counter.MugFillLine.gameObject.SetActive(false);
            AudioManager.StopLoop(SfxId.Pour);
        }

        private void Update()
        {
            if (!_pouring || IsComplete) return;

            var fill = _counter.MugFill;
            fill.SetValue(fill.Value + pourPerSecond * Time.deltaTime);
            if (fill.Value >= 1f) Finish();
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
            spoon.Interactable = false;
            _counter.MugFill.Fill.color = Color.Lerp(waterColor, coffeeColor, Mathf.Clamp01((float)_spoons / targetSpoons));
        }

        private void OnKettleReleased()
        {
            _pouring = false;
            AudioManager.StopLoop(SfxId.Pour);
            if (_pourStarted && _counter.MugFill.Value > 0f) Finish();
        }

        private void Finish()
        {
            if (IsComplete) return;
            _pouring = false;
            _counter.KettleTarget.Interactable = false;
            AudioManager.StopLoop(SfxId.Pour);

            var spoonScore = _spoons == targetSpoons ? 1f : wrongSpoonScore;
            var fillScore = 1f - Mathf.Abs(_counter.MugFill.Value - targetFill) / fillTolerance;
            Complete(spoonWeight * spoonScore + (1f - spoonWeight) * Mathf.Clamp01(fillScore));
        }

        private void RefreshSpoonMarkers()
        {
            var markers = _counter.SpoonMarkers;
            for (var i = 0; i < markers.Length; i++)
                markers[i].enabled = i < _spoons;
        }

        private void RefreshKettle() => _counter.KettleBody.color = _spoons > 0 ? kettleReadyColor : kettleIdleColor;
    }
}
