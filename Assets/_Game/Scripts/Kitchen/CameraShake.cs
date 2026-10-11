using UnityEngine;
using UnityEngine.Rendering;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Small camera kicks that make actions land (a knock at the door, "Perfect!", the kettle clicking off, a spill).
    /// The offset is applied only while the camera renders and removed right after, so it never fights scripted camera
    /// moves (the finale drift, the door view) and can't leave the camera displaced.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraShake : MonoBehaviour
    {
        [Tooltip("Scales every kick (0 turns shaking off).")]
        [SerializeField, Range(0f, 2f)] private float strength = 1f;
        [Tooltip("How fast the shake wobbles (noise frequency).")]
        [SerializeField, Min(1f)] private float frequency = 28f;

        private static CameraShake _instance;
        private Camera _camera;
        private float _amount;
        private float _seconds;
        private float _left;
        private float _seed;
        private Vector3 _applied;

        /// <summary>A kick of <paramref name="amount"/> world units fading out over <paramref name="seconds"/>.</summary>
        public static void Kick(float amount, float seconds = 0.25f)
        {
            if (_instance == null || amount <= 0f) return;
            // A stronger kick replaces a weaker one in progress; a weaker one never cuts a strong one short.
            var current = _instance._seconds > 0f ? _instance._amount * (_instance._left / _instance._seconds) : 0f;
            if (amount < current) return;
            _instance._amount = amount * _instance.strength;
            _instance._seconds = Mathf.Max(0.01f, seconds);
            _instance._left = _instance._seconds;
            _instance._seed = Random.value * 100f;
        }

        private void Awake() => _camera = GetComponent<Camera>();

        private void OnEnable()
        {
            _instance = this;
            RenderPipelineManager.beginCameraRendering += OnBegin;
            RenderPipelineManager.endCameraRendering += OnEnd;
        }

        private void OnDisable()
        {
            if (_instance == this) _instance = null;
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering -= OnEnd;
        }

        private void Update()
        {
            if (_left > 0f) _left = Mathf.Max(0f, _left - Time.unscaledDeltaTime);
        }

        private void OnBegin(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _camera || _left <= 0f) return;
            var fade = _left / _seconds;
            var t = Time.unscaledTime * frequency;
            var x = (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f;
            var y = (Mathf.PerlinNoise(_seed + 37f, t) - 0.5f) * 2f;
            _applied = new Vector3(x, y, 0f) * (_amount * fade * fade);
            transform.position += _applied;
        }

        private void OnEnd(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _camera || _applied == Vector3.zero) return;
            transform.position -= _applied;
            _applied = Vector3.zero;
        }
    }
}
