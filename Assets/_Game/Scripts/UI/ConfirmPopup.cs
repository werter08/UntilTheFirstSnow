using System;
using Courtyard.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Courtyard.UI
{
    /// <summary>A small paper note asking a yes/no question ("Start this day over?").</summary>
    public sealed class ConfirmPopup : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text question;
        [SerializeField] private Button yes;
        [SerializeField] private Button no;

        private Action _onYes;

        public bool IsOpen => group != null && group.blocksRaycasts;

        private void Awake()
        {
            yes.onClick.AddListener(() => Close(true));
            no.onClick.AddListener(() => Close(false));
            SetOpen(false);
        }

        public void Ask(string text, Action onYes)
        {
            question.text = text;
            _onYes = onYes;
            AudioManager.Play(SfxId.PaperNote);
            SetOpen(true);
        }

        private void Close(bool confirmed)
        {
            AudioManager.Play(SfxId.UiClick);
            SetOpen(false);
            if (confirmed) _onYes?.Invoke();
            _onYes = null;
        }

        private void SetOpen(bool open)
        {
            group.alpha = open ? 1f : 0f;
            group.blocksRaycasts = open;
            group.interactable = open;
        }
    }
}
