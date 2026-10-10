using Courtyard.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Whatever can be clicked or dragged lights up softly under the cursor: a small warm light fades in on the hovered
    /// object (sized to it) and out when the cursor leaves. No change to the object itself (scale, colliders, punches).
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class HoverHighlight : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float intensity = 1.2f;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 10f;
        [SerializeField, Min(0.1f)] private float radiusPerSize = 1.1f;

        private Light2D _light;
        private Transform _target;
        private float _alpha;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            _light.intensity = 0f;
        }

        private void OnEnable()
        {
            PointerTarget.AnyHoverStarted += OnHoverStarted;
            PointerTarget.AnyHoverEnded += OnHoverEnded;
        }

        private void OnDisable()
        {
            PointerTarget.AnyHoverStarted -= OnHoverStarted;
            PointerTarget.AnyHoverEnded -= OnHoverEnded;
        }

        private void OnHoverStarted(Transform target)
        {
            if (target == null) return;
            _target = target;
            var bounds = Bounds(target);
            _light.pointLightOuterRadius = Mathf.Max(0.4f, Mathf.Max(bounds.extents.x, bounds.extents.y) * radiusPerSize * 2f);
            _light.pointLightInnerRadius = _light.pointLightOuterRadius * 0.35f;
        }

        private void OnHoverEnded(Transform target)
        {
            if (target == _target) _target = null;
        }

        private void LateUpdate()
        {
            var visible = _target != null && _target.gameObject.activeInHierarchy;
            if (visible) transform.position = Bounds(_target).center;
            _alpha = Mathf.MoveTowards(_alpha, visible ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            _light.intensity = intensity * _alpha;
            _light.enabled = _alpha > 0.001f;
        }

        private static Bounds Bounds(Transform target)
        {
            var renderer = target.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null) return renderer.bounds;
            var collider = target.GetComponent<Collider2D>();
            return collider != null ? collider.bounds : new Bounds(target.position, Vector3.one * 0.5f);
        }
    }
}
