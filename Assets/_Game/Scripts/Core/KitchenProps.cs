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
        [UnityEngine.Tooltip("Today's drink is made in her mug (leaf print on the counter mug).")]
        public bool brewInHerMug;
        public bool scarf;
        public bool blanket;
        public PlantStage plantStage;
        [UnityEngine.Tooltip("Sleeve colour of the clerk's hands.")]
        public Sweater sweater;
        [UnityEngine.Range(0, 3)] public int fridgeNotes;
    }
}
