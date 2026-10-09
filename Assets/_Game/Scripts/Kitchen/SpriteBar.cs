using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Vertical fill bar that grows up from this transform's origin. The fill renderer must use Sliced draw mode.
    /// </summary>
    public sealed class SpriteBar : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer fill;
        [SerializeField, Min(0f)] private float width = 1f;
        [SerializeField, Min(0f)] private float maxHeight = 1f;
        [SerializeField, Range(0f, 1f)] private float value;

        public float Value => value;
        public SpriteRenderer Fill => fill;
        public float MaxHeight => maxHeight;

        public void SetValue(float newValue)
        {
            value = Mathf.Clamp01(newValue);
            Apply();
        }

        private void Awake() => Apply();

        private void Apply()
        {
            var height = maxHeight * value;
            fill.enabled = height > 0.0001f;
            fill.size = new Vector2(width, height);
            fill.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        }
    }
}
