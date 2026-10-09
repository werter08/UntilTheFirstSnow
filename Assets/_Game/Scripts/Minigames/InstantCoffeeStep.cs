using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Drag the spoon from the jar to the mug, then hold the kettle to pour up to the line.
    /// Quality = correct spoon count + how close the fill is to the line.
    /// </summary>
    public sealed class InstantCoffeeStep : MinigameStep
    {
        [Header("Scene refs")]
        [SerializeField] private DragTarget spoon;
        [Tooltip("Coffee on the spoon; shown while the spoon is loaded.")]
        [SerializeField] private SpriteRenderer spoonLoad;
        [SerializeField] private Collider2D jar;
        [SerializeField] private Collider2D mug;
        [Tooltip("One marker per spoon in the mug; extra spoons beyond the array still count.")]
        [SerializeField] private SpriteRenderer[] spoonMarkers;
        [SerializeField] private SpriteBar mugFill;
        [SerializeField] private Transform fillLine;
        [SerializeField] private PointerTarget kettle;
        [SerializeField] private SpriteRenderer kettleBody;

        [Header("Tuning")]
        [SerializeField, Min(1)] private int targetSpoons = 2;
        [SerializeField, Range(0f, 1f)] private float wrongSpoonScore = 0.5f;
        [SerializeField, Min(0.01f)] private float pourPerSecond = 0.35f;
        [SerializeField, Range(0f, 1f)] private float targetFill = 0.85f;
        [Tooltip("Fill distance from the line at which the fill score reaches 0.")]
        [SerializeField, Min(0.01f)] private float fillTolerance = 0.3f;
        [SerializeField, Range(0f, 1f)] private float spoonWeight = 0.5f;

        [Header("Look")]
        [SerializeField] private Color waterColor = new(0.75f, 0.85f, 0.95f);
        [SerializeField] private Color coffeeColor = new(0.35f, 0.2f, 0.1f);
        [SerializeField] private Color kettleIdleColor = new(0.6f, 0.6f, 0.65f);
        [SerializeField] private Color kettleReadyColor = new(0.85f, 0.85f, 0.9f);

        private Vector3 _spoonRest;
        private Vector2 _grabOffset;
        private bool _spoonLoaded;
        private int _spoons;
        private bool _pouring;
        private bool _pourStarted;

        public override void Begin(MinigameContext ctx)
        {
            _spoonRest = spoon.transform.position;
            spoon.DragStarted += OnSpoonDragStarted;
            spoon.Dragged += OnSpoonDragged;
            spoon.DragEnded += OnSpoonDragEnded;
            kettle.Pressed += OnKettlePressed;
            kettle.Released += OnKettleReleased;

            fillLine.localPosition = new Vector3(fillLine.localPosition.x, targetFill * mugFill.MaxHeight, 0f);
            mugFill.SetValue(0f);
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

            if (kettle != null)
            {
                kettle.Pressed -= OnKettlePressed;
                kettle.Released -= OnKettleReleased;
            }
        }

        private void Update()
        {
            if (!_pouring || IsComplete) return;

            mugFill.SetValue(mugFill.Value + pourPerSecond * Time.deltaTime);
            if (mugFill.Value >= 1f) Finish();
        }

        private void OnSpoonDragStarted(Vector2 world) => _grabOffset = (Vector2)spoon.transform.position - world;

        private void OnSpoonDragged(Vector2 world)
        {
            spoon.transform.position = world + _grabOffset;
            if (!_spoonLoaded && jar.OverlapPoint(world))
            {
                _spoonLoaded = true;
                spoonLoad.enabled = true;
            }
        }

        private void OnSpoonDragEnded(Vector2 world)
        {
            if (_spoonLoaded && mug.OverlapPoint(world))
            {
                _spoonLoaded = false;
                spoonLoad.enabled = false;
                _spoons++;
                RefreshSpoonMarkers();
                RefreshKettle();
            }

            spoon.transform.position = _spoonRest;
        }

        private void OnKettlePressed()
        {
            if (_spoons == 0 || IsComplete) return;

            _pouring = true;
            _pourStarted = true;
            spoon.Interactable = false;
            mugFill.Fill.color = Color.Lerp(waterColor, coffeeColor, Mathf.Clamp01((float)_spoons / targetSpoons));
        }

        private void OnKettleReleased()
        {
            _pouring = false;
            if (_pourStarted && mugFill.Value > 0f) Finish();
        }

        private void Finish()
        {
            _pouring = false;
            kettle.Interactable = false;

            var spoonScore = _spoons == targetSpoons ? 1f : wrongSpoonScore;
            var fillScore = 1f - Mathf.Abs(mugFill.Value - targetFill) / fillTolerance;
            Complete(spoonWeight * spoonScore + (1f - spoonWeight) * Mathf.Clamp01(fillScore));
        }

        private void RefreshSpoonMarkers()
        {
            for (var i = 0; i < spoonMarkers.Length; i++)
                spoonMarkers[i].enabled = i < _spoons;
        }

        private void RefreshKettle() => kettleBody.color = _spoons > 0 ? kettleReadyColor : kettleIdleColor;
    }
}
