using System;
using UnityEngine;

namespace Courtyard.Story
{
    /// <summary>A paper note pinned to the fridge at the end of a day; it stays there and can be read any time after.</summary>
    [Serializable]
    public sealed class FridgeNote
    {
        [Tooltip("Written at the end of this day (1, 5, 10, 20, 30); on the fridge from then on.")]
        [SerializeField] private int afterDay;
        [Tooltip("Signature under the note (empty = his own note).")]
        [SerializeField] private string from;
        [SerializeField, TextArea(2, 5)] private string text;

        public int AfterDay => afterDay;
        public string From => from;
        public string Text => text;
    }
}
