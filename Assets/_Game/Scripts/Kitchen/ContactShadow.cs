using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// A drawn contact shadow under a prop that stays on the surface: it follows the prop sideways, and shrinks and
    /// fades as the prop is lifted (kettle, jars, mugs). Hidden whenever the prop is hidden.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ContactShadow : MonoBehaviour
    {
        [SerializeField] private Transform caster;
        [SerializeField] private SpriteRenderer casterRenderer;
        [Tooltip("Lift (world units) at which the shadow has faded out completely.")]
        [SerializeField, Min(0.05f)] private float fadeHeight = 1.2f;
        [SerializeField, Range(0.1f, 1f)] private float liftedScale = 0.6f;

        private SpriteRenderer _renderer;
        private Vector3 _casterRest;
        private Vector3 _rest;
        private Vector3 _scale;
        private float _alpha;

        public void Bind(Transform target, SpriteRenderer targetRenderer)
        {
            caster = target;
            casterRenderer = targetRenderer;
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _rest = transform.position;
            _scale = transform.localScale;
            _alpha = _renderer.color.a;
            if (caster != null) _casterRest = caster.position;
        }

        private void LateUpdate()
        {
            if (caster == null) return;

            var visible = caster.gameObject.activeInHierarchy && (casterRenderer == null || casterRenderer.enabled);
            _renderer.enabled = visible;
            if (!visible) return;

            var offset = caster.position - _casterRest;
            var lift = Mathf.Clamp01(Mathf.Max(0f, offset.y) / fadeHeight);
            transform.position = new Vector3(_rest.x + offset.x, _rest.y, _rest.z);
            transform.localScale = _scale * Mathf.Lerp(1f, liftedScale, lift);
            var colour = _renderer.color;
            colour.a = _alpha * (1f - lift);
            _renderer.color = colour;
        }
    }
}
