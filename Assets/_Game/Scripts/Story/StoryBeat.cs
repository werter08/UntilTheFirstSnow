using System;
using UnityEngine;

namespace Courtyard.Story
{
    [Serializable]
    public sealed class StoryBeat
    {
        [SerializeField] private BeatTrigger trigger;
        [Tooltip("Only used when Trigger is AfterStep (0-based step index).")]
        [SerializeField, Min(0)] private int stepIndex;
        [SerializeField] private BeatCondition condition;
        [SerializeField] private BeatStyle style;
        [Tooltip("Shown for Dialogue beats.")]
        [SerializeField] private string speaker;
        [SerializeField, TextArea(2, 5)] private string text;
        [Tooltip("After this beat is read, Mira's window changes (e.g. Day 20: 'Hold on.' → Lit).")]
        [SerializeField] private bool setsMiraWindow;
        [SerializeField] private Core.MiraWindowState miraWindowAfter;

        public BeatTrigger Trigger => trigger;
        public int StepIndex => stepIndex;
        public BeatCondition Condition => condition;
        public BeatStyle Style => style;
        public string Speaker => speaker;
        public string Text => text;
        public bool SetsMiraWindow => setsMiraWindow;
        public Core.MiraWindowState MiraWindowAfter => miraWindowAfter;

        public bool Matches(BeatTrigger when, int step, bool goodResult)
        {
            if (trigger != when) return false;
            if (when == BeatTrigger.AfterStep && stepIndex != step) return false;

            return condition switch
            {
                BeatCondition.GoodResult => goodResult,
                BeatCondition.PoorResult => !goodResult,
                _ => true,
            };
        }
    }
}
