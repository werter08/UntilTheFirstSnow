using System.Collections.Generic;
using UnityEngine;

namespace Courtyard.Story
{
    /// <summary>A standalone list of beats, e.g. the ending scene's final lines.</summary>
    [CreateAssetMenu(menuName = "Courtyard/Story Sequence", fileName = "StorySequence")]
    public sealed class StorySequence : ScriptableObject
    {
        [SerializeField] private List<StoryBeat> beats = new();

        public IReadOnlyList<StoryBeat> Beats => beats;
    }
}
