using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>What a step needs to know about the level it runs in.</summary>
    public sealed class MinigameContext
    {
        public MinigameContext(LevelDefinition level, int stepIndex, Camera worldCamera, KitchenDirector kitchen, CounterStation counter, FeedbackFx fx, HandsView hands,
            UI.ScreenFader fader = null, UI.MinigameGauge gauge = null, UI.MinigameGauge secondGauge = null, UI.HintView hint = null)
        {
            Gauge = gauge;
            SecondGauge = secondGauge;
            Hint = hint;
            Fader = fader;
            Level = level;
            StepIndex = stepIndex;
            WorldCamera = worldCamera;
            Kitchen = kitchen;
            Counter = counter;
            Fx = fx;
            Hands = hands;
        }

        public LevelDefinition Level { get; }
        public int StepIndex { get; }
        public Camera WorldCamera { get; }
        public KitchenDirector Kitchen { get; }
        public CounterStation Counter { get; }
        public FeedbackFx Fx { get; }
        public HandsView Hands { get; }

        /// <summary>Full-screen fade for story moments (e.g. walking to the door on Day 10). May be null.</summary>
        public UI.ScreenFader Fader { get; }

        /// <summary>The shared "stop in the green" gauge (grind, pour flow, milk). May be null.</summary>
        public UI.MinigameGauge Gauge { get; }

        /// <summary>A second gauge for steps that watch two things at once (pour-over: water and flow). May be null.</summary>
        public UI.MinigameGauge SecondGauge { get; }

        /// <summary>The hint line, for steps that tell the player what to do next (e.g. "click the cup"). May be null.</summary>
        public UI.HintView Hint { get; }
    }
}
