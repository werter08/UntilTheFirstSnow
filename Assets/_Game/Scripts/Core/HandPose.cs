namespace Courtyard.Core
{
    /// <summary>Which hand drawing is shown when the player uses a tool.</summary>
    public enum HandPose
    {
        Open,
        Point,
        Pinch,
        Grip,
        RaiseMug,
        [UnityEngine.Tooltip("No hand overlay: the tool's own art shows the hand (e.g. the kettle while pouring).")]
        Hidden,
    }
}
