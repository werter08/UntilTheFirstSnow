using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.UI;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// The recipe pinned on the wall: click to read it on the note card (handwritten). It grows over the autumn; it
    /// never holds Mira's recipe. Pulses gently to draw the eye (start of the early days, first time the player is stuck).
    /// </summary>
    [RequireComponent(typeof(PointerTarget))]
    public sealed class RecipeCard : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float pulseScale = 1.15f;
        [SerializeField, Min(0.1f)] private float pulseSeconds = 0.9f;
        [SerializeField] private Color glow = new(1f, 0.93f, 0.7f);

        private PointerTarget _target;
        private SpriteRenderer _renderer;
        private NoteCard _card;
        private string _text;
        private Vector3 _baseScale;
        private Coroutine _pulse;

        private void Awake()
        {
            _target = GetComponent<PointerTarget>();
            _renderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        private void OnEnable() => _target.Clicked += Read;
        private void OnDisable() => _target.Clicked -= Read;

        public void Set(string text, NoteCard card)
        {
            _text = text;
            _card = card;
            gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
        }

        private void Read()
        {
            if (_card == null || _card.IsOpen || string.IsNullOrWhiteSpace(_text)) return;
            _card.Open(_text, string.Empty, NotePaper.Yellow);
        }

        /// <summary>Two soft swells with a warm tint and a paper rustle: "look here".</summary>
        public void Pulse()
        {
            if (!isActiveAndEnabled) return;
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            AudioManager.Play(SfxId.PaperNote);
            var colour = _renderer != null ? _renderer.color : Color.white;
            yield return Tweens.RunLinear(pulseSeconds, t =>
            {
                var wave = Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Sin(t * Mathf.PI); // two swells, easing in and out
                var k = Mathf.Abs(wave);
                transform.localScale = _baseScale * Mathf.Lerp(1f, pulseScale, k);
                if (_renderer != null) _renderer.color = Color.Lerp(colour, glow * colour, k);
            });
            transform.localScale = _baseScale;
            if (_renderer != null) _renderer.color = colour;
            _pulse = null;
        }
    }
}
