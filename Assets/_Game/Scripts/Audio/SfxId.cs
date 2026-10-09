namespace Courtyard.Audio
{
    /// <summary>Every sound the game plays. Clips are assigned in the SfxLibrary asset.</summary>
    public enum SfxId
    {
        UiClick,
        KettleSwitch,
        KettleBoil,
        Pour,
        Spoon,
        Grind,
        Steam,
        Squeal,
        MugClink,
        Topping,
        Knock,
        PaperNote,
        Footsteps,
        Perfect,
        Fail,
        KettleBubble, // appended: enum values are serialized in SfxLibrary, never insert in the middle
    }
}
