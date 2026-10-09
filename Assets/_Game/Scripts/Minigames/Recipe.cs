using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>A drink defined by its toppings, e.g. Mira_Usual.</summary>
    [CreateAssetMenu(menuName = "Courtyard/Recipe", fileName = "Recipe")]
    public sealed class Recipe : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private List<Topping> toppings = new();

        public string DisplayName => displayName;
        public IReadOnlyList<Topping> Toppings => toppings;

        /// <summary>
        /// 1 for an exact match, 0 for nothing right. Each correct topping counts; extra or missing ones lower the score.
        /// </summary>
        public float Score(IReadOnlyCollection<Topping> picked)
        {
            var wanted = toppings.Distinct().ToList();
            var chosen = picked.Distinct().ToList();
            var size = Mathf.Max(wanted.Count, chosen.Count);
            if (size == 0) return 1f;

            return (float)chosen.Count(wanted.Contains) / size;
        }
    }
}
