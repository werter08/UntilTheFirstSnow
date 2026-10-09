namespace Courtyard.Story
{
    /// <summary>Lets a beat play only after a good or a poor result (e.g. Day 10 "correct"/"wrong" endings).</summary>
    public enum BeatCondition
    {
        Always,
        GoodResult,
        PoorResult,
    }
}
