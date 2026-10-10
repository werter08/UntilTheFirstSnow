using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Wall drop shadow: a half-transparent black silhouette of the caster's own sprite, offset down-right like the
    /// window frame's drawn shadow. Follows sprite swaps (calendar, plant stages, rack mugs) and the caster's visibility.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DropShadow : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer caster;

        private SpriteRenderer _renderer;

        public void Bind(SpriteRenderer target) => caster = target;

        private void Awake() => _renderer = GetComponent<SpriteRenderer>();

        private void LateUpdate()
        {
            if (caster == null) return;
            var visible = caster.enabled && caster.gameObject.activeInHierarchy && caster.sprite != null;
            _renderer.enabled = visible;
            if (!visible) return;
            if (_renderer.sprite != caster.sprite) _renderer.sprite = caster.sprite;
            _renderer.flipX = caster.flipX;
            _renderer.drawMode = caster.drawMode;
            if (caster.drawMode != SpriteDrawMode.Simple) _renderer.size = caster.size;
        }
    }
}
