using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Courtyard.UI
{
    /// <summary>A sticky-note button: lifts and tilts a little under the hand (with a soft tick), squashes when pressed.</summary>
    public sealed class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Min(1f)] private float hoverScale = 1.07f;
        [SerializeField, Min(0.5f)] private float pressScale = 0.95f;
        [SerializeField] private float hoverTilt = 2.5f;
        [SerializeField, Min(0.01f)] private float seconds = 0.12f;

        private Quaternion _restRotation;
        private bool _hovered;
        private Coroutine _tween;

        private void Awake() => _restRotation = transform.localRotation;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            AudioManager.Play(SfxId.Hover);
            To(hoverScale, hoverTilt);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            To(1f, 0f);
        }

        public void OnPointerDown(PointerEventData eventData) => To(pressScale, 0f);

        public void OnPointerUp(PointerEventData eventData) => To(_hovered ? hoverScale : 1f, _hovered ? hoverTilt : 0f);

        private void To(float scale, float tilt)
        {
            if (!isActiveAndEnabled) return;
            if (_tween != null) StopCoroutine(_tween);
            _tween = StartCoroutine(Animate(scale, tilt));
        }

        private IEnumerator Animate(float scale, float tilt)
        {
            var fromScale = transform.localScale;
            var fromRotation = transform.localRotation;
            var toRotation = _restRotation * Quaternion.Euler(0f, 0f, tilt);
            yield return Tweens.Run(seconds, k =>
            {
                transform.localScale = Vector3.LerpUnclamped(fromScale, Vector3.one * scale, k);
                transform.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, k);
            });
        }
    }
}
