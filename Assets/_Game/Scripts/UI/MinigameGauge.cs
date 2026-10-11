using System.Collections;
using Courtyard.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Courtyard.UI
{
    /// <summary>
    /// The one "stop in the green" gauge every minigame uses (grind, pour flow, milk): a hand-drawn frame with an
    /// orange fill, a green zone (and an optional red one), a star at the perfect point, a marker above the bar and the
    /// step's icon. It hovers in screen space above the active tool and follows it; the green zone breathes while the
    /// marker is in it, the marker trembles in the red; on release the zone that was hit flashes, then it fades.
    /// </summary>
    public sealed class MinigameGauge : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("Inner area of the bar (fill and zones are placed in it by normalized ranges).")]
        [SerializeField] private RectTransform inner;
        [Tooltip("Clips the fill to the current value.")]
        [SerializeField] private RectTransform fillMask;
        [SerializeField] private Image fill;
        [SerializeField] private RectTransform greenZone;
        [SerializeField] private RectTransform redZone;
        [SerializeField] private RectTransform marker;
        [SerializeField] private RectTransform star;
        [SerializeField] private Image icon;

        [Header("Placement")]
        [Tooltip("Height above the tool's top edge (reference pixels, 1920×1080).")]
        [SerializeField] private float aboveTool = 140f;
        [SerializeField, Min(0.1f)] private float follow = 10f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.25f;
        [SerializeField, Min(0f)] private float hideDelay = 0.3f;

        [Header("Look")]
        [Tooltip("Slightly see-through, so the zones it sweeps over still show.")]
        [SerializeField] private Color fillColour = new(0.91f, 0.584f, 0.353f, 0.88f);   // #e8955a
        [SerializeField] private Color fillInGreen = new(1f, 0.68f, 0.42f, 0.88f);
        [SerializeField] private Color fillInRed = new(0.72f, 0.36f, 0.32f, 0.88f);
        [SerializeField, Min(1f)] private float greenPulse = 1.05f;
        [SerializeField, Min(0f)] private float redShake = 3f;
        [SerializeField, Min(0.05f)] private float flashSeconds = 0.45f;

        private RectTransform _rect;
        private Canvas _canvas;
        private Camera _camera;
        private Renderer _tool;
        private Vector2 _green;
        private Vector2? _red;
        private float _value;
        private bool _shown;
        private Coroutine _fade;
        private Vector2 _markerBase;

        public float Value => _value;
        public bool IsShown => _shown;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            _markerBase = marker.anchoredPosition;
            group.alpha = 0f;
        }

        /// <summary>
        /// Shows the gauge above <paramref name="tool"/>: green zone <paramref name="green"/> (0..1), optional red zone,
        /// and the step's icon (none = no icon).
        /// </summary>
        public void Show(Renderer tool, Camera worldCamera, Vector2 green, Vector2? red, Sprite stepIcon, float value = 0f)
        {
            _tool = tool;
            _camera = worldCamera;
            _green = new Vector2(Mathf.Clamp01(Mathf.Min(green.x, green.y)), Mathf.Clamp01(Mathf.Max(green.x, green.y)));
            _red = red;
            Place(greenZone, _green);
            redZone.gameObject.SetActive(red.HasValue);
            if (red.HasValue) Place(redZone, red.Value);
            Place(star, new Vector2((_green.x + _green.y) * 0.5f, (_green.x + _green.y) * 0.5f), keepSize: true);
            icon.gameObject.SetActive(stepIcon != null);
            if (stepIcon != null) icon.sprite = stepIcon;

            gameObject.SetActive(true);
            SnapToTool();
            SetValue(value);
            _shown = true;
            FadeTo(1f, 0f);
        }

        public void SetValue(float value)
        {
            _value = Mathf.Clamp01(value);
            fillMask.anchorMax = new Vector2(_value, 1f);
            var x = inner.rect.width * _value - inner.rect.width * 0.5f;
            _markerBase = new Vector2(x, _markerBase.y);
        }

        /// <summary>The player let go: the zone that was hit flashes (green = good, red = bad), then the gauge fades.</summary>
        public void Release()
        {
            if (!_shown) return;
            _shown = false;
            var zone = InGreen ? greenZone : InRed ? redZone : null;
            if (zone != null) StartCoroutine(Flash(zone));
            FadeTo(0f, hideDelay + (zone != null ? flashSeconds : 0f));
        }

        /// <summary>Fades out without a flash (the step ended some other way).</summary>
        public void Hide()
        {
            if (!_shown) return; // already fading (e.g. after Release)
            _shown = false;
            FadeTo(0f, hideDelay);
        }

        private bool InGreen => _value >= _green.x && _value <= _green.y;
        private bool InRed => _red.HasValue && _value >= _red.Value.x && _value <= _red.Value.y;

        private void LateUpdate()
        {
            if (group.alpha <= 0f && !_shown) return;

            // Follow the tool smoothly.
            if (TryToolPosition(out var target))
                _rect.anchoredPosition = Vector2.Lerp(_rect.anchoredPosition, target, 1f - Mathf.Exp(-follow * Time.deltaTime));

            fill.color = InRed ? fillInRed : InGreen ? fillInGreen : fillColour;
            var pulse = _shown && InGreen ? Mathf.Lerp(1f, greenPulse, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f)) : 1f;
            greenZone.localScale = new Vector3(pulse, pulse, 1f);
            star.localScale = Vector3.one * pulse;
            var shake = _shown && InRed ? Random.insideUnitCircle * redShake : Vector2.zero;
            marker.anchoredPosition = _markerBase + shake;
        }

        private void SnapToTool()
        {
            if (TryToolPosition(out var target)) _rect.anchoredPosition = target;
        }

        /// <summary>Where the gauge sits: centred above the tool's top edge, in the canvas's reference pixels.</summary>
        private bool TryToolPosition(out Vector2 position)
        {
            position = default;
            if (_tool == null || _camera == null || _canvas == null) return false;
            var bounds = _tool.bounds;
            var screen = _camera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            var parent = (RectTransform)_rect.parent;
            var uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out var local)) return false;
            position = local + Vector2.up * aboveTool;

            // Keep it on screen.
            var half = _rect.rect.size * 0.5f;
            var area = parent.rect;
            position.x = Mathf.Clamp(position.x, area.xMin + half.x + 60f, area.xMax - half.x - 20f);
            position.y = Mathf.Clamp(position.y, area.yMin + half.y + 20f, area.yMax - half.y - 70f);
            return true;
        }

        /// <summary>Stretches a zone over [range.x, range.y] of the inner bar (or centres a fixed-size mark at range.x).</summary>
        private static void Place(RectTransform part, Vector2 range, bool keepSize = false)
        {
            if (keepSize)
            {
                part.anchorMin = part.anchorMax = new Vector2(range.x, part.anchorMin.y);
                part.anchoredPosition = new Vector2(0f, part.anchoredPosition.y);
                return;
            }

            part.anchorMin = new Vector2(range.x, 0f);
            part.anchorMax = new Vector2(range.y, 1f);
            part.offsetMin = part.offsetMax = Vector2.zero;
            part.pivot = new Vector2(0.5f, 0.5f);
        }

        private IEnumerator Flash(RectTransform zone)
        {
            var image = zone.GetComponent<Image>();
            var colour = image.color;
            yield return Tweens.RunLinear(flashSeconds, t =>
            {
                var k = Mathf.Sin(t * Mathf.PI * 3f); // three blinks
                image.color = Color.Lerp(colour, Color.white, Mathf.Abs(k) * 0.6f);
                zone.localScale = Vector3.one * (1f + Mathf.Abs(k) * 0.12f);
            });
            image.color = colour;
            zone.localScale = Vector3.one;
        }

        private void FadeTo(float alpha, float delay)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(alpha, delay));
        }

        private IEnumerator FadeRoutine(float alpha, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var from = group.alpha;
            yield return Tweens.Run(fadeSeconds, t => group.alpha = Mathf.Lerp(from, alpha, t));
            _fade = null;
        }
    }
}
