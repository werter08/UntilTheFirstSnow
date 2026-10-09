using System.Collections.Generic;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Pick toppings from the counter jars, then click the mug to serve. Scored against a <see cref="Recipe"/>.
    /// Used by both GuessHerDrink (with her hint in the story) and HerDrinkFromMemory (no hint).
    /// </summary>
    public sealed class PickToppingsStep : MinigameStep
    {
        [SerializeField] private Recipe recipe;
        [SerializeField, Min(1)] private int picksRequired = 2;

        private readonly List<Topping> _picked = new();
        private readonly List<(ToppingJar jar, System.Action handler)> _subscriptions = new();
        private CounterStation _counter;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            foreach (var jar in _counter.ToppingJars)
            {
                jar.SetPicked(false);
                var captured = jar;
                System.Action handler = () => Toggle(captured);
                jar.Target.Clicked += handler;
                jar.Target.Interactable = true;
                _subscriptions.Add((jar, handler));
            }

            _counter.Mug.Clicked += Serve;
            _counter.Mug.Interactable = false;
        }

        private void OnDestroy()
        {
            if (_counter == null) return;
            foreach (var (jar, handler) in _subscriptions) jar.Target.Clicked -= handler;
            _counter.Mug.Clicked -= Serve;
        }

        private void Toggle(ToppingJar jar)
        {
            if (IsComplete) return;

            if (_picked.Remove(jar.Topping))
                jar.SetPicked(false);
            else if (_picked.Count < picksRequired)
            {
                _picked.Add(jar.Topping);
                jar.SetPicked(true);
            }

            _counter.Mug.Interactable = _picked.Count == picksRequired;
        }

        private void Serve()
        {
            if (IsComplete || _picked.Count != picksRequired) return;

            foreach (var jar in _counter.ToppingJars) jar.Target.Interactable = false;
            _counter.Mug.Interactable = false;
            Complete(recipe.Score(_picked));
        }
    }
}
