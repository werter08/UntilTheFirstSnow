using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Small procedural idle motion so the kitchen never looks frozen (curtains, lamp, plant, Mira's silhouette).
    /// Rotation sways around the object's pivot; position bobs. Wind (set by the KitchenDirector per weather) scales it.
    /// </summary>
    public sealed class AmbientMotion : MonoBehaviour
    {
        [SerializeField] private float swayDegrees = 1f;
        [SerializeField] private float bob = 0f;
        [SerializeField] private float stretch = 0f;
        [SerializeField, Min(0.01f)] private float speed = 0.6f;
        [Tooltip("How much windy weather strengthens this motion.")]
        [SerializeField, Min(0f)] private float windResponse = 1f;

        public static float Wind { get; set; } = 1f;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private Vector3 _baseScale;
        private float _phase;

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _baseScale = transform.localScale;
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void OnDisable()
        {
            transform.localPosition = _basePosition;
            transform.localRotation = _baseRotation;
            transform.localScale = _baseScale;
        }

        private void Update()
        {
            var strength = Mathf.Lerp(1f, Wind, windResponse);
            var t = Time.time * speed + _phase;
            // Two sines at unrelated rates read as organic rather than mechanical.
            var wave = Mathf.Sin(t) * 0.7f + Mathf.Sin(t * 2.3f + 1.3f) * 0.3f;
            transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, wave * swayDegrees * strength);
            transform.localPosition = _basePosition + Vector3.up * (Mathf.Sin(t * 0.8f) * bob * strength);
            transform.localScale = new Vector3(_baseScale.x * (1f + wave * stretch * strength), _baseScale.y, _baseScale.z);
        }
    }
}
