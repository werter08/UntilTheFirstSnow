using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Courtyard.UI
{
    /// <summary>
    /// A hand-drawn round icon button in the corner: 70% when idle, full and a bit bigger under the hand (with a small
    /// handwritten tooltip), a squash when pressed. <see cref="Dimmed"/> fades it further (e.g. hints off).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RoundButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private TMP_Text tooltip;
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.7f;
        [SerializeField, Range(0f, 1f)] private float dimmedAlpha = 0.4f;
        [SerializeField, Min(1f)] private float hoverScale = 1.08f;
        [SerializeField, Min(0.5f)] private float pressScale = 0.9f;
        [SerializeField, Min(0.01f)] private float seconds = 0.1f;

        private CanvasGroup _group;
        private bool _hovered;
        private Coroutine _scale;

        /// <summary>Faded further (its option is off).</summary>
        public bool Dimmed { get; set; }

        public string Tooltip
        {
            set { if (tooltip != null) tooltip.text = value; }
        }

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (tooltip != null) tooltip.alpha = 0f;
        }

        private void Update()
        {
            var target = _hovered ? 1f : Dimmed ? dimmedAlpha : idleAlpha;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime * 5f);
            if (tooltip != null) tooltip.alpha = Mathf.MoveTowards(tooltip.alpha, _hovered ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            AudioManager.Play(SfxId.Hover);
            ScaleTo(hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            ScaleTo(1f);
        }

        public void OnPointerDown(PointerEventData eventData) => ScaleTo(pressScale);
        public void OnPointerUp(PointerEventData eventData) => ScaleTo(_hovered ? hoverScale : 1f);

        private void ScaleTo(float scale)
        {
            if (!isActiveAndEnabled) return;
            if (_scale != null) StopCoroutine(_scale);
            _scale = StartCoroutine(Scale(scale));
        }

        private IEnumerator Scale(float scale)
        {
            var from = transform.localScale;
            yield return Tweens.Run(seconds, k => transform.localScale = Vector3.LerpUnclamped(from, Vector3.one * scale, k));
        }
    }
}
