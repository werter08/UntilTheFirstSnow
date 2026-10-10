using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// The inside of the side-view mug, as drawn by the team: a cap over the top of the mug (aligned to its rim and
    /// outline). Empty, it shows the dark "empty" cap; as the mug fills, the drink cap rises over it bottom-up.
    /// Spices added on top show as a thin coloured layer.
    /// </summary>
    public sealed class MugContents : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer emptyCap;
        [SerializeField] private SpriteRenderer liquid;
        [SerializeField] private SpriteMask mask;
        [SerializeField] private SpriteRenderer spice;
        [Tooltip("Height of the cap region (local units).")]
        [SerializeField, Min(0.01f)] private float regionHeight = 0.66f;
        [SerializeField, Min(0.01f)] private float regionWidth = 1.2f;
        [SerializeField, Min(0.005f)] private float spicePerPortion = 0.06f;
        [SerializeField] private float spiceWidth = 0.96f;

        private int _portions;

        public float RegionHeight => regionHeight;

        public void SetSprite(Sprite drink)
        {
            if (drink != null) liquid.sprite = drink;
        }

        public void SetLevel(float level)
        {
            level = Mathf.Clamp01(level);
            if (emptyCap != null) emptyCap.enabled = level < 0.999f;
            liquid.enabled = level > 0.001f;
            var height = regionHeight * level;
            mask.transform.localScale = new Vector3(regionWidth, Mathf.Max(0.0001f, height), 1f);
            mask.transform.localPosition = new Vector3(0f, -regionHeight * 0.5f + height * 0.5f, 0f);
        }

        /// <summary>A portion of spice settles on top of the drink.</summary>
        public void AddSpice(Color colour)
        {
            if (spice == null) return;
            _portions++;
            spice.enabled = true;
            spice.color = _portions == 1 ? colour : Color.Lerp(spice.color, colour, 0.5f);
            var height = spicePerPortion * _portions;
            spice.size = new Vector2(spiceWidth, height);
            spice.transform.localPosition = new Vector3(0f, regionHeight * 0.5f - 0.1f - height * 0.5f, 0f);
        }

        public void ClearSpice()
        {
            _portions = 0;
            if (spice != null) spice.enabled = false;
        }
    }
}
