using System.Collections;
using Courtyard.Core;
using Courtyard.UI;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Day 20 opens on a paper note stuck to her window. The hint asks the player to tap her window; the note opens
    /// (her handwriting, on pink paper), and once it's put back he decides to make her a coffee. Unscored.
    /// </summary>
    public sealed class ReadHerWindowStep : MinigameStep
    {
        [Tooltip("Her note, as read up close.")]
        [SerializeField, TextArea] private string noteText = "My heating broke :(";
        [SerializeField] private string from = "Mira";

        private MinigameContext _ctx;
        private bool _opened;

        public override void Begin(MinigameContext ctx)
        {
            _ctx = ctx;
            NudgeTarget = ctx.Kitchen.MiraWindowTarget;
            ctx.Kitchen.MiraWindowPoked += OnWindowTapped;
        }

        private void OnDestroy()
        {
            if (_ctx != null) _ctx.Kitchen.MiraWindowPoked -= OnWindowTapped;
        }

        private void OnWindowTapped()
        {
            var card = _ctx.Kitchen.NoteCard;
            if (_opened || IsComplete || card == null || card.IsOpen) return;
            _opened = true;
            StartCoroutine(Read(card));
        }

        private IEnumerator Read(NoteCard card)
        {
            card.Open(noteText, from, NotePaper.Pink);
            while (card.IsOpen) yield return null;
            Complete(1f);
        }
    }
}
