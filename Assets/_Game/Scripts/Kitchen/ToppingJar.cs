using Courtyard.Core;
using Courtyard.Minigames;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// A topping jar on the counter. Each portion shaken into the cup shows as a little round dot of that spice under
    /// the jar (one dot, two dots…), so the player sees what's already in the drink.
    /// </summary>
    [RequireComponent(typeof(PointerTarget))]
    public sealed class ToppingJar : MonoBehaviour
    {
        [SerializeField] private Topping topping;
        [SerializeField] private PointerTarget target;
        [Tooltip("Lets the player carry the jar to the cup.")]
        [SerializeField] private DragTarget drag;
        [Tooltip("Holds the portion dots (placed under the jar).")]
        [SerializeField] private GameObject pickedMarker;
        [Tooltip("Colour of the little sprinkle when this topping is picked.")]
        [SerializeField] private Color sprinkleColour = new(0.6f, 0.4f, 0.25f);
        [SerializeField] private Color dotOutline = new(0.16f, 0.13f, 0.12f);
        [Tooltip("Dot diameter and spacing (world units, before the jar's scale).")]
        [SerializeField, Min(0.02f)] private float dotSize = 0.2f;
        [SerializeField, Min(0.02f)] private float dotSpacing = 0.26f;

        private const int MaxDots = 3;
        private static Sprite _dot;
        private SpriteRenderer[] _dots;
        private int _portions;

        public Topping Topping => topping;
        public PointerTarget Target => target;
        public DragTarget Drag => drag;
        public bool IsPicked => _portions > 0;
        public int Portions => _portions;
        public Color SprinkleColour => sprinkleColour;

        public void SetPicked(bool picked) => SetPortions(picked ? Mathf.Max(1, _portions) : 0);

        /// <summary>Shows one dot per portion; a new dot pops in.</summary>
        public void SetPortions(int count)
        {
            count = Mathf.Clamp(count, 0, MaxDots);
            EnsureDots();
            var grew = count > _portions;
            _portions = count;
            pickedMarker.SetActive(count > 0);
            for (var i = 0; i < MaxDots; i++)
            {
                var dot = _dots[i].transform.parent.gameObject;
                dot.SetActive(i < count);
                dot.transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * dotSpacing, 0f, 0f);
            }

            if (grew && isActiveAndEnabled) StartCoroutine(Pop(_dots[count - 1].transform.parent));
        }

        private System.Collections.IEnumerator Pop(Transform dot)
        {
            yield return Tweens.RunLinear(0.25f, t =>
                dot.localScale = Vector3.one * (Mathf.Sin(t * Mathf.PI * 0.5f) + Mathf.Sin(t * Mathf.PI) * 0.35f));
            dot.localScale = Vector3.one;
        }

        /// <summary>Round dots built once at runtime: a dark rim (like the drawings' outlines) around the spice colour.</summary>
        private void EnsureDots()
        {
            if (_dots != null) return;
            if (pickedMarker.TryGetComponent<SpriteRenderer>(out var bar)) bar.enabled = false; // the old placeholder bar
            _dot ??= MakeDotSprite();
            var order = TryGetComponent<SpriteRenderer>(out var jar) ? jar.sortingOrder + 2 : 3;
            var layer = jar != null ? jar.sortingLayerName : "Props";
            _dots = new SpriteRenderer[MaxDots];
            for (var i = 0; i < MaxDots; i++)
            {
                var root = new GameObject($"Dot{i}").transform;
                root.SetParent(pickedMarker.transform, false);
                Disc(root, "Rim", dotSize, dotOutline, layer, order);
                _dots[i] = Disc(root, "Spice", dotSize * 0.72f, sprinkleColour, layer, order + 1);
                root.gameObject.SetActive(false);
            }
        }

        private static SpriteRenderer Disc(Transform parent, string name, float size, Color colour, string layer, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.transform.localScale = Vector3.one * size;
            renderer.sprite = _dot;
            renderer.color = colour;
            renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite MakeDotSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                var a = Mathf.Clamp01(size * 0.5f - d); // 1 px soft edge
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
