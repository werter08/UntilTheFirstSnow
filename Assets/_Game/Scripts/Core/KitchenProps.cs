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
        [UnityEngine.Tooltip("Topping jars on the counter (days with GuessHerDrink / HerDrinkFromMemory).")]
        public bool toppings;
        [UnityEngine.Tooltip("Mug on the counter today (Day 1 chipped, his terracotta, or her maple-leaf mug).")]
        public MugStyle brewMug;
        [UnityEngine.Tooltip("Mugs from earlier days, standing on the wall rack (never the one on the counter).")]
        public MugStyle[] rackMugs;
        [UnityEngine.Tooltip("Day 30: his and her mugs side by side on the window sill.")]
        public bool mugsOnSill;
        public bool scarf;
        public PlantStage plantStage;
        [UnityEngine.Tooltip("Day 1: the ceiling lamp is off (grey bulb, no light) — overcast and empty.")]
        public bool lampOff;
        [UnityEngine.Tooltip("Sleeve colour of the clerk's hands.")]
        public Sweater sweater;
        [UnityEngine.Range(0, 3)] public int fridgeNotes;
    }
}
