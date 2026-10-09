using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>What a step needs to know about the level it runs in.</summary>
    public sealed class MinigameContext
    {
        public MinigameContext(LevelDefinition level, int stepIndex, Camera worldCamera)
        {
            Level = level;
            StepIndex = stepIndex;
            WorldCamera = worldCamera;
        }

        public LevelDefinition Level { get; }
        public int StepIndex { get; }
        public Camera WorldCamera { get; }
    }
}
