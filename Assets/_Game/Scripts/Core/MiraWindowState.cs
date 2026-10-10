namespace Courtyard.Core
{
    /// <summary>Mira's window across the courtyard. Values are serialized in the levels: append only, never reorder.</summary>
    public enum MiraWindowState
    {
        Dark,
        Lit,
        Silhouette,
        Wave,
        Note,
        [System.Obsolete("Use Dark.")] Empty,
    }
}
