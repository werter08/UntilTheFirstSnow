using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.UI
{
    /// <summary>The wall picture held up close: it pops up in the middle of the screen over a dim; any click puts it back.</summary>
    public sealed class PictureCard : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform picture;
        [SerializeField, Min(0f)] private float popSeconds = 0.25f;

        private Coroutine _anim;
        private int _openedFrame;

        public bool IsOpen { get; private set; }

        private void Awake() => SetVisible(0f, false);

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            AudioManager.Play(SfxId.UiClick);
            Animate(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            AudioManager.Play(SfxId.UiClick);
            Animate(false);
        }

        private void Update()
        {
            // Any click after the opening one puts the picture back.
            if (!IsOpen || Time.frameCount == _openedFrame) return;
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame) Close();
        }

        private void Animate(bool open)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(AnimateRoutine(open));
        }

        private IEnumerator AnimateRoutine(bool open)
        {
            var from = group.alpha;
            var to = open ? 1f : 0f;
            yield return Tweens.RunLinear(popSeconds, t =>
            {
                var k = Mathf.Lerp(from, to, t);
                SetVisible(k, open);
                var overshoot = open ? 1f + Mathf.Sin(t * Mathf.PI) * 0.05f : 1f;
                picture.localScale = Vector3.one * (Mathf.Lerp(0.8f, 1f, k) * overshoot);
            });
            SetVisible(to, open);
            picture.localScale = Vector3.one;
        }

        private void SetVisible(float alpha, bool blocks)
        {
            group.alpha = alpha;
            group.blocksRaycasts = blocks;
            group.interactable = blocks;
        }
    }
}
