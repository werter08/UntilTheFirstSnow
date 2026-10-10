using System;
using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Story;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.UI
{
    /// <summary>A fridge note held up to read: the paper pops up in the middle of the screen; any click puts it back.</summary>
    public sealed class NoteCard : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform paper;
        [SerializeField] private TMP_Text body;
        [SerializeField] private TMP_Text signature;
        [SerializeField, Min(0f)] private float popSeconds = 0.25f;

        private Coroutine _anim;
        private int _openedFrame;

        public bool IsOpen { get; private set; }

        /// <summary>Raised when a note is opened (the end-of-day moment waits for the player to read it).</summary>
        public event Action Opened;

        private void Awake() => SetVisible(0f, false);

        public void Open(FridgeNote note)
        {
            if (note == null) return;
            body.text = note.Text;
            signature.text = string.IsNullOrEmpty(note.From) ? string.Empty : "— " + note.From;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            AudioManager.Play(SfxId.PaperNote);
            Animate(true);
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            AudioManager.Play(SfxId.PaperNote);
            Animate(false);
        }

        private void Update()
        {
            // Any click after the opening one puts the note back.
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
            group.blocksRaycasts = open;
            var from = group.alpha;
            var to = open ? 1f : 0f;
            yield return Tweens.RunLinear(popSeconds, t =>
            {
                var k = Mathf.Lerp(from, to, t);
                SetVisible(k, open);
                var overshoot = open ? 1f + Mathf.Sin(t * Mathf.PI) * 0.06f : 1f;
                paper.localScale = Vector3.one * (Mathf.Lerp(0.8f, 1f, k) * overshoot);
            });
            SetVisible(to, open);
            paper.localScale = Vector3.one;
        }

        private void SetVisible(float alpha, bool blocks)
        {
            group.alpha = alpha;
            group.blocksRaycasts = blocks;
            group.interactable = blocks;
        }
    }
}
