using Courtyard.Core;
using Courtyard.Story;
using Courtyard.UI;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>A note on the fridge you can click to read (opens the <see cref="NoteCard"/>).</summary>
    [RequireComponent(typeof(PointerTarget))]
    public sealed class ReadableNote : MonoBehaviour
    {
        private PointerTarget _target;
        private FridgeNote _note;
        private NoteCard _card;

        public FridgeNote Note => _note;

        public void Set(FridgeNote note, NoteCard card)
        {
            _note = note;
            _card = card;
        }

        private void Awake() => _target = GetComponent<PointerTarget>();

        private void OnEnable()
        {
            if (_target == null) _target = GetComponent<PointerTarget>();
            _target.Clicked += Read;
        }

        private void OnDisable() => _target.Clicked -= Read;

        private void Read()
        {
            if (_card == null || _note == null || _card.IsOpen) return;
            // The note opens on paper of its own colour.
            var sprite = TryGetComponent<SpriteRenderer>(out var renderer) ? renderer.sprite : null;
            var pink = sprite != null && sprite.name.Contains("pink", System.StringComparison.OrdinalIgnoreCase);
            _card.Open(_note, pink ? NotePaper.Pink : NotePaper.Yellow);
        }
    }
}
