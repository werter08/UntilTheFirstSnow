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

        [Tooltip("Drink tiles clipped to the round mugs, and the same tiles clipped to the chipped mug's notch.")]
        [SerializeField] private Sprite[] roundTiles;
        [SerializeField] private Sprite[] chippedTiles;

        private int _portions;
        private bool _chipped;
        private Sprite _drink;
        private Sprite _empty;

        public float RegionHeight => regionHeight;

        public void SetSprite(Sprite drink)
        {
            if (drink == null) return;
            _drink = drink;
            liquid.sprite = ForMug(drink);
        }

        /// <summary>The chipped mug (Day 1) has a notch in its rim: its tiles are cut to match.</summary>
        public void SetChipped(bool chipped)
        {
            _chipped = chipped;
            if (emptyCap != null)
            {
                if (_empty == null) _empty = emptyCap.sprite;
                emptyCap.sprite = ForMug(_empty);
            }

            if (_drink != null) liquid.sprite = ForMug(_drink);
        }

        private Sprite ForMug(Sprite tile)
        {
            if (roundTiles == null || chippedTiles == null) return tile;
            for (var i = 0; i < roundTiles.Length && i < chippedTiles.Length; i++)
            {
                if (roundTiles[i] == tile || chippedTiles[i] == tile)
                    return _chipped && chippedTiles[i] != null ? chippedTiles[i] : roundTiles[i];
            }

            return tile;
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
