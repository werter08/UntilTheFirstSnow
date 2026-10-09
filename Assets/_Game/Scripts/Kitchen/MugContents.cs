using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// The drink inside the side-view mug: a drink tile over the top of the mug body, revealed from the bottom up
    /// by a mask as the mug fills.
    /// </summary>
    public sealed class MugContents : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer liquid;
        [SerializeField] private SpriteMask mask;
        [Tooltip("Height of the visible drink region (local units).")]
        [SerializeField, Min(0.01f)] private float regionHeight = 0.66f;
        [SerializeField, Min(0.01f)] private float regionWidth = 1.2f;

        public float RegionHeight => regionHeight;

        public void SetSprite(Sprite drink)
        {
            if (drink != null) liquid.sprite = drink;
        }

        public void SetLevel(float level)
        {
            level = Mathf.Clamp01(level);
            liquid.enabled = level > 0.001f;
            var height = regionHeight * level;
            mask.transform.localScale = new Vector3(regionWidth, Mathf.Max(0.0001f, height), 1f);
            mask.transform.localPosition = new Vector3(0f, -regionHeight * 0.5f + height * 0.5f, 0f);
        }
    }
}
