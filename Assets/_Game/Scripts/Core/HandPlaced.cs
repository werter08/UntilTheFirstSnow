using UnityEngine;

namespace Courtyard.Core
{
    /// <summary>
    /// Marks an object the team placed by hand in the scene: the editor builders keep its position, scale (and, for
    /// particle systems, their settings) instead of re-applying generated values. No runtime behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandPlaced : MonoBehaviour
    {
    }
}
