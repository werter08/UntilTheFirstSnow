using System;
using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Story;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        [Tooltip("The paper drawings (yellow / pink sticky note, calendar page). Missing ones keep the current paper.")]
        [SerializeField] private PaperLook[] papers = Array.Empty<PaperLook>();
        [Tooltip("\"click to close\", placed just under the paper.")]
        [SerializeField] private RectTransform closeHint;
        [Tooltip("HUD parts hidden while a note is read, so no other text sits on top of it (hint, story box).")]
        [SerializeField] private Canvas[] hideWhileOpen = Array.Empty<Canvas>();

        private Coroutine _anim;
        private int _openedFrame;

        public bool IsOpen { get; private set; }

        /// <summary>The fridge note on the card right now (null for other pages: the calendar, the recipe).</summary>
        public FridgeNote CurrentNote { get; private set; }

        private FridgeNote _pendingNote;

        /// <summary>Raised when a note is opened (the end-of-day moment waits for the player to read it).</summary>
        public event Action Opened;

        private void Awake() => SetVisible(0f, false);

        public void Open(FridgeNote note, NotePaper paper = NotePaper.Yellow)
        {
            if (note == null) return;
            _pendingNote = note;
            Open(note.Text, note.From, paper);
        }

        /// <summary>Opens a page with any text (fridge note, calendar page); <paramref name="from"/> signs it.</summary>
        public void Open(string text, string from, NotePaper paper = NotePaper.Yellow)
        {
            UsePaper(paper);
            CurrentNote = _pendingNote; // the fridge note being opened, or null for any other page
            _pendingNote = null;
            body.text = text;
            signature.text = string.IsNullOrEmpty(from) ? string.Empty : "— " + from;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            SetHudHidden(true);
            AudioManager.Play(SfxId.PaperNote);
            Animate(true);
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SetHudHidden(false);
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

        private void UsePaper(NotePaper kind)
        {
            var look = Array.Find(papers, p => p != null && p.paper == kind && p.sprite != null);
            if (look == null) return;
            if (paper.TryGetComponent<Image>(out var image))
            {
                image.sprite = look.sprite;
                image.color = Color.white;
                image.preserveAspect = true;
            }

            paper.sizeDelta = look.size;
            body.rectTransform.offsetMin = new Vector2(look.padding.x, look.padding.y);
            body.rectTransform.offsetMax = new Vector2(-look.padding.z, -look.padding.w);
            body.color = look.ink;
            if (closeHint != null) closeHint.anchoredPosition = new Vector2(0f, -look.size.y * 0.5f - 45f);
        }

        private void SetHudHidden(bool hidden)
        {
            foreach (var canvas in hideWhileOpen)
                if (canvas != null) canvas.enabled = !hidden;
        }

        private void SetVisible(float alpha, bool blocks)
        {
            group.alpha = alpha;
            group.blocksRaycasts = blocks;
            group.interactable = blocks;
        }
    }
}
