using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Click a curtain to draw it closed or tie it back open (two drawings, each with its own spot and size). Clicked
    /// too often it breaks: the curtain slips off the rod and falls away for the rest of the day.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CurtainToggle : MonoBehaviour
    {
        [SerializeField] private Sprite tiedBack;
        [SerializeField] private Sprite drawn;
        [SerializeField] private Vector3 tiedPosition;
        [SerializeField] private Vector3 tiedScale = Vector3.one;
        [SerializeField] private Vector3 drawnPosition;
        [SerializeField] private Vector3 drawnScale = Vector3.one;
        [SerializeField, Min(0f)] private float fallSeconds = 0.8f;

        private SpriteRenderer _renderer;
        private PointerTarget _target;
        private Breakable _breakable;
        private bool _drawn;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _target = GetComponent<PointerTarget>();
            _breakable = GetComponent<Breakable>();
        }

        private void OnEnable()
        {
            if (_target != null) _target.Clicked += Toggle;
            if (_breakable != null) _breakable.Broke += Fall;
        }

        private void OnDisable()
        {
            if (_target != null) _target.Clicked -= Toggle;
            if (_breakable != null) _breakable.Broke -= Fall;
        }

        private void Toggle()
        {
            if (drawn == null || tiedBack == null || (_breakable != null && _breakable.IsBroken)) return;
            _drawn = !_drawn;
            _renderer.sprite = _drawn ? drawn : tiedBack;
            var position = _drawn ? drawnPosition : tiedPosition;
            var scale = _drawn ? drawnScale : tiedScale;
            if (TryGetComponent<AmbientMotion>(out var motion)) motion.SetBase(position, scale); // it re-poses every frame
            else
            {
                transform.localPosition = position;
                transform.localScale = scale;
            }
            var collider = GetComponent<BoxCollider2D>();
            if (collider != null && _renderer.sprite != null)
            {
                collider.size = _renderer.sprite.bounds.size;
                collider.offset = _renderer.sprite.bounds.center;
            }

        }

        private void Fall() => StartCoroutine(FallRoutine());

        private System.Collections.IEnumerator FallRoutine()
        {
            if (TryGetComponent<AmbientMotion>(out var motion)) motion.enabled = false;
            if (TryGetComponent<Fidget>(out var fidget)) fidget.enabled = false;
            Audio.AudioManager.Play(Audio.SfxId.Break);
            var from = transform.localPosition;
            var colour = _renderer.color;
            var tilt = Random.Range(-12f, 12f);
            yield return Tweens.Run(fallSeconds, t =>
            {
                transform.localPosition = from + Vector3.down * (t * t * 3f);
                transform.localRotation = Quaternion.Euler(0f, 0f, tilt * t);
                _renderer.color = new Color(colour.r, colour.g, colour.b, colour.a * (1f - t));
            });
            gameObject.SetActive(false);
        }
    }
}
