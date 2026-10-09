using System;

namespace Courtyard.Core
{
    /// <summary>Which kitchen props are visible on a given day.</summary>
    [Serializable]
    public struct KitchenProps
    {
        public bool beans;
        public bool grinder;
        public bool milk;
        public bool cinnamon;
        public bool herMug;
        public bool scarf;
        public bool blanket;
        public PlantStage plantStage;
        [UnityEngine.Range(0, 3)] public int fridgeNotes;
    }
}
