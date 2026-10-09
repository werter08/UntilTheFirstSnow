using System.Collections.Generic;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Pick toppings for Mira's drink, then click her mug to serve. Scored against a <see cref="Recipe"/>.
    /// Used by both GuessHerDrink (with her hint in the story) and HerDrinkFromMemory (no hint).
    /// </summary>
    public sealed class PickToppingsStep : MinigameStep
    {
        [SerializeField] private Recipe recipe;
        [SerializeField] private ToppingOption[] options;
        [SerializeField] private PointerTarget serveMug;
        [SerializeField, Min(1)] private int picksRequired = 2;

        private readonly List<Topping> _picked = new();

        public override void Begin(MinigameContext ctx)
        {
            foreach (var option in options)
            {
                option.pickedMarker.SetActive(false);
                var captured = option;
                option.target.Clicked += () => Toggle(captured);
            }

            serveMug.Clicked += Serve;
            serveMug.Interactable = false;
        }

        private void Toggle(ToppingOption option)
        {
            if (IsComplete) return;

            if (_picked.Remove(option.topping))
                option.pickedMarker.SetActive(false);
            else if (_picked.Count < picksRequired)
            {
                _picked.Add(option.topping);
                option.pickedMarker.SetActive(true);
            }

            serveMug.Interactable = _picked.Count == picksRequired;
        }

        private void Serve()
        {
            if (IsComplete || _picked.Count != picksRequired) return;

            foreach (var option in options) option.target.Interactable = false;
            serveMug.Interactable = false;
            Complete(recipe.Score(_picked));
        }
    }
}
