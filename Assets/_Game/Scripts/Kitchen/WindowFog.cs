using System;
using Courtyard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Fog on the window glass (cold days). Dragging the mouse over the glass wipes it — any time the hand isn't busy
    /// with something it picked up or pressed (a tool, her window) — painted into a
    /// small alpha texture with a soft round brush — and the fog slowly comes back. No score, no text: only the pointing
    /// hand while over the glass.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WindowFog : MonoBehaviour
    {
        [Tooltip("World size of the glass (the window mask) the fog covers.")]
        [SerializeField] private Vector2 size = new(5.8f, 4.4f);
        [SerializeField, Range(0f, 1f)] private float opacity = 0.45f;
        [Tooltip("Brush radius in screen pixels at the 1080p reference height.")]
        [SerializeField, Min(2f)] private float brushPixels = 28f;
        [Tooltip("Seconds for a wiped spot to fog over completely again.")]
        [SerializeField, Min(1f)] private float regrowSeconds = 20f;
        [Tooltip("Texture width; the height follows the glass's proportions. Bilinear filtering keeps it soft.")]
        [SerializeField, Range(32, 512)] private int textureWidth = 160;
        [Tooltip("Share of each side that fades out, so the fog has soft edges.")]
        [SerializeField, Range(0f, 0.3f)] private float edgeSoftness = 0.08f;
        [SerializeField] private Camera worldCamera;

        /// <summary>Wiping is allowed right now (set by the level runner: e.g. not while a note is open).</summary>
        public Func<bool> CanWipe { get; set; }

        private SpriteRenderer _renderer;
        private Texture2D _texture;
        private Color32[] _pixels;
        private float[] _fog;   // 1 = fully fogged, 0 = wiped clean
        private float[] _edge;  // soft-edge multiplier per texel
        private int _w;
        private int _h;
        private bool _clean;    // some texel is below full fog: keep regrowing
        private bool _dirty;
        private bool _hovering;
        private Vector2? _lastTexel;
        private int _busyHands; // something is pressed or dragged: that press isn't a wipe

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (worldCamera == null) worldCamera = Camera.main;
            _w = textureWidth;
            _h = Mathf.Max(8, Mathf.RoundToInt(textureWidth * size.y / Mathf.Max(0.01f, size.x)));
            _texture = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "WindowFog",
            };
            _pixels = new Color32[_w * _h];
            _fog = new float[_w * _h];
            _edge = new float[_w * _h];
            for (var y = 0; y < _h; y++)
            for (var x = 0; x < _w; x++)
            {
                var u = Mathf.Min(x, _w - 1 - x) / (float)_w;
                var v = Mathf.Min(y, _h - 1 - y) / (float)_h;
                var edge = edgeSoftness <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Min(u, v) / edgeSoftness);
                _edge[y * _w + x] = edge;
                _fog[y * _w + x] = 1f;
            }

            Upload();
            _renderer.sprite = Sprite.Create(_texture, new Rect(0, 0, _w, _h), new Vector2(0.5f, 0.5f), _w / size.x);
        }

        private void OnEnable()
        {
            PointerTarget.AnyPressed += OnTargetPressed;
            PointerTarget.AnyReleased += OnTargetReleased;
            DragTarget.AnyDragStarted += OnTargetPressed;
            DragTarget.AnyDragEnded += OnTargetReleased;
        }

        private void OnDisable()
        {
            PointerTarget.AnyPressed -= OnTargetPressed;
            PointerTarget.AnyReleased -= OnTargetReleased;
            DragTarget.AnyDragStarted -= OnTargetPressed;
            DragTarget.AnyDragEnded -= OnTargetReleased;
            _busyHands = 0;
            SetHover(false);
        }

        // Decor that just wiggles when poked (curtains, plant, her window) doesn't stop a wipe; tools do.
        private static bool IsTool(Transform target) => target != null && target.GetComponent<Fidget>() == null;

        private void OnTargetPressed(Transform target, HandPose pose)
        {
            if (IsTool(target)) _busyHands++;
        }

        private void OnTargetReleased(Transform target)
        {
            if (IsTool(target)) _busyHands = Mathf.Max(0, _busyHands - 1);
        }

        private void OnDestroy()
        {
            if (_renderer != null && _renderer.sprite != null) Destroy(_renderer.sprite);
            if (_texture != null) Destroy(_texture);
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer != null && !pointer.press.isPressed) _busyHands = 0; // nothing can still be held once the button is up
            var allowed = (CanWipe == null || CanWipe()) && _busyHands == 0;
            var overGlass = false;
            if (allowed && pointer != null && worldCamera != null)
            {
                var world = (Vector2)worldCamera.ScreenToWorldPoint(pointer.position.ReadValue());
                var local = (Vector2)transform.InverseTransformPoint(world);
                overGlass = Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f;
                if (overGlass && pointer.press.isPressed) Wipe(local);
                else _lastTexel = null;
            }
            else _lastTexel = null;

            SetHover(overGlass);
            Regrow(Time.deltaTime);
            if (_dirty) Upload();
        }

        /// <summary>Stamps the brush along the stroke from the last point, so fast strokes leave no gaps.</summary>
        private void Wipe(Vector2 local)
        {
            var texel = new Vector2((local.x / size.x + 0.5f) * _w, (local.y / size.y + 0.5f) * _h);
            var unitsPerScreenPixel = 2f * worldCamera.orthographicSize / 1080f;
            var radius = brushPixels * unitsPerScreenPixel * (_w / size.x);
            var from = _lastTexel ?? texel;
            var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, texel) / Mathf.Max(0.5f, radius * 0.4f)));
            for (var i = 1; i <= steps; i++) Stamp(Vector2.Lerp(from, texel, i / (float)steps), radius);
            _lastTexel = texel;
        }

        private void Stamp(Vector2 centre, float radius)
        {
            var minX = Mathf.Max(0, Mathf.FloorToInt(centre.x - radius));
            var maxX = Mathf.Min(_w - 1, Mathf.CeilToInt(centre.x + radius));
            var minY = Mathf.Max(0, Mathf.FloorToInt(centre.y - radius));
            var maxY = Mathf.Min(_h - 1, Mathf.CeilToInt(centre.y + radius));
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) / radius;
                if (d >= 1f) continue;
                var keep = Mathf.SmoothStep(0f, 1f, (d - 0.5f) / 0.5f); // clean core, soft rim
                var i = y * _w + x;
                if (keep >= _fog[i]) continue;
                _fog[i] = keep;
                _clean = true;
                _dirty = true;
            }
        }

        private void Regrow(float dt)
        {
            if (!_clean) return;
            var step = dt / regrowSeconds;
            var anyClean = false;
            for (var i = 0; i < _fog.Length; i++)
            {
                if (_fog[i] >= 1f) continue;
                _fog[i] = Mathf.Min(1f, _fog[i] + step);
                anyClean |= _fog[i] < 1f;
            }

            _clean = anyClean;
            _dirty = true;
        }

        private void Upload()
        {
            for (var i = 0; i < _fog.Length; i++)
                _pixels[i] = new Color32(255, 255, 255, (byte)(255f * opacity * _fog[i] * _edge[i]));
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
            _dirty = false;
        }

        /// <summary>The pointing hand while over the glass (the hands view listens for hovers).</summary>
        private void SetHover(bool hovering)
        {
            if (_hovering == hovering) return;
            _hovering = hovering;
            if (hovering) PointerTarget.RaiseHoverStarted(transform);
            else PointerTarget.RaiseHoverEnded(transform);
        }
    }
}
