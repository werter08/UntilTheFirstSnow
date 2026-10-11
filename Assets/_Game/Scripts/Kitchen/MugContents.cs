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

        [Header("Spices tint the drink")]
        [Tooltip("How much each portion of spice shifts the drink's colour toward the spice.")]
        [SerializeField, Range(0f, 1f)] private float tintPerPortion = 0.3f;
        [SerializeField, Min(0.05f)] private float stirSeconds = 0.6f;

        private Coroutine _stir;

        /// <summary>
        /// A portion of spice stirs into the drink: no layer on top — the drink itself takes on the spice's colour
        /// (each portion a bit more) with a little swirl and glow as it mixes in.
        /// </summary>
        public void AddSpice(Color colour)
        {
            _portions++;
            if (spice != null) spice.enabled = false;
            var target = Color.Lerp(liquid.color, new Color(colour.r, colour.g, colour.b, 1f), tintPerPortion);
            if (_stir != null) StopCoroutine(_stir);
            _stir = isActiveAndEnabled ? StartCoroutine(Stir(target)) : null;
            if (_stir == null) liquid.color = target;
        }

        private System.Collections.IEnumerator Stir(Color target)
        {
            var from = liquid.color;
            var t = liquid.transform;
            var baseScale = t.localScale;
            var basePosition = t.localPosition;
            for (var elapsed = 0f; elapsed < stirSeconds; elapsed += Time.deltaTime)
            {
                var k = elapsed / stirSeconds;
                var swirl = Mathf.Sin(k * Mathf.PI * 4f) * (1f - k);
                liquid.color = Color.Lerp(Color.Lerp(from, target, k), Color.white, Mathf.Sin(k * Mathf.PI) * 0.25f); // a glint
                t.localPosition = basePosition + new Vector3(swirl * 0.04f, 0f, 0f);
                t.localScale = new Vector3(baseScale.x * (1f + swirl * 0.03f), baseScale.y, baseScale.z);
                yield return null;
            }

            liquid.color = target;
            t.localPosition = basePosition;
            t.localScale = baseScale;
            _stir = null;
        }

        private SpriteRenderer _milk;

        /// <summary>
        /// Milk being poured in: the latte tile fades in over the coffee (<paramref name="share"/> 0 = all coffee,
        /// 1 = latte), clipped to the same fill.
        /// </summary>
        public void SetMilk(float share, Sprite latte)
        {
            if (_milk == null)
            {
                if (share <= 0f || latte == null || liquid == null) return;
                _milk = Instantiate(liquid.gameObject, liquid.transform.parent).GetComponent<SpriteRenderer>();
                _milk.name = "Milk";
                _milk.transform.SetPositionAndRotation(liquid.transform.position, liquid.transform.rotation);
                _milk.transform.localScale = liquid.transform.localScale;
                _milk.sortingOrder = liquid.sortingOrder + 1;
                // The fill mask clips only its own sorting range: let it reach the milk layer too.
                if (mask != null && mask.isCustomRangeActive && mask.frontSortingOrder < _milk.sortingOrder)
                    mask.frontSortingOrder = _milk.sortingOrder;
            }

            if (latte != null) _milk.sprite = ForMug(latte);
            _milk.enabled = share > 0.001f && liquid.enabled;
            _milk.color = new Color(1f, 1f, 1f, Mathf.Clamp01(share));
        }

        public void ClearSpice()
        {
            // A copy of this mug (his cup on Day 20) carries the milk layer as a child but not the reference.
            if (_milk == null && liquid != null && liquid.transform.parent != null)
            {
                var copied = liquid.transform.parent.Find("Milk");
                if (copied != null) _milk = copied.GetComponent<SpriteRenderer>();
            }

            if (_milk != null) _milk.enabled = false;
            _portions = 0;
            if (spice != null) spice.enabled = false;
            if (_stir != null) StopCoroutine(_stir);
            _stir = null;
            if (liquid != null) liquid.color = Color.white;
        }
    }
}
