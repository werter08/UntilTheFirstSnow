using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Keeps an arm's sleeve going past the screen edge, so the end of the sleeve is never seen (art manifest).
    /// Follows whatever sprite the arm renderer shows, and its colour / visibility.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SleeveExtender : MonoBehaviour
    {
        [SerializeField] private SleeveLibrary library;
        [SerializeField] private SpriteRenderer strip;
        [Tooltip("World length of the continued sleeve; long enough to leave the screen from anywhere.")]
        [SerializeField, Min(1f)] private float length = 25f;

        private SpriteRenderer _arm;
        private Sprite _shown;

        private void Awake() => _arm = GetComponent<SpriteRenderer>();

        private void LateUpdate()
        {
            if (_arm.sprite != _shown)
            {
                _shown = _arm.sprite;
                Configure();
            }

            strip.color = _arm.color;
            strip.enabled = _arm.enabled && strip.sprite != null;
        }

        private void Configure()
        {
            if (library == null || !library.TryGet(_shown, out var entry))
            {
                strip.sprite = null;
                return;
            }

            strip.sprite = entry.strip;
            strip.sortingLayerID = _arm.sortingLayerID;
            strip.sortingOrder = _arm.sortingOrder + 1; // covers the cuff end beyond the slice
            var t = strip.transform;
            t.localPosition = entry.anchor;
            t.localRotation = Quaternion.Euler(0f, 0f, entry.angle);
            var stripHeight = entry.strip.bounds.size.y;
            var parentScale = Mathf.Max(0.0001f, transform.lossyScale.y);
            t.localScale = new Vector3(1f, length / parentScale / stripHeight, 1f);
        }
    }
}
