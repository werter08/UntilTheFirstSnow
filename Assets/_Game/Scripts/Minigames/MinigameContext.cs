using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>What a step needs to know about the level it runs in.</summary>
    public sealed class MinigameContext
    {
        public MinigameContext(LevelDefinition level, int stepIndex, Camera worldCamera, KitchenDirector kitchen, CounterStation counter, FeedbackFx fx)
        {
            Level = level;
            StepIndex = stepIndex;
            WorldCamera = worldCamera;
            Kitchen = kitchen;
            Counter = counter;
            Fx = fx;
        }

        public LevelDefinition Level { get; }
        public int StepIndex { get; }
        public Camera WorldCamera { get; }
        public KitchenDirector Kitchen { get; }
        public CounterStation Counter { get; }
        public FeedbackFx Fx { get; }
    }
}
