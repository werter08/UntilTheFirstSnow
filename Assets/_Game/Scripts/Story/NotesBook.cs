using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Courtyard.Story
{
    /// <summary>All the fridge notes, in story order. One is pinned at the end of each day.</summary>
    [CreateAssetMenu(menuName = "Courtyard/Notes Book", fileName = "FridgeNotes")]
    public sealed class NotesBook : ScriptableObject
    {
        [SerializeField] private List<FridgeNote> notes = new();
        [Tooltip("After the credits: the note that pops onto the fridge the morning after (its day is ignored).")]
        [SerializeField] private FridgeNote epilogue;

        public FridgeNote Epilogue => epilogue;

        /// <summary>Every day's note, in story order.</summary>
        public IReadOnlyList<FridgeNote> All => PinnedBefore(int.MaxValue);

        /// <summary>Notes already on the fridge when <paramref name="day"/> starts (written after earlier days).</summary>
        public IReadOnlyList<FridgeNote> PinnedBefore(int day) =>
            notes.Where(n => n != null && n.AfterDay < day).OrderBy(n => n.AfterDay).ToList();

        /// <summary>The note written at the end of <paramref name="day"/> (null if none).</summary>
        public FridgeNote WrittenAfter(int day) => notes.FirstOrDefault(n => n != null && n.AfterDay == day);
    }
}
