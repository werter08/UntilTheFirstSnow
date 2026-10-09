using System.Collections.Generic;
using Courtyard.Audio;
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
        private FeedbackFx _fx;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _fx = ctx.Fx;
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

            AudioManager.Play(SfxId.Topping);
            _fx.Punch(jar.transform, 0.7f);
            if (!_picked.Contains(jar.Topping)) _fx.Burst(jar.transform.position + Vector3.up * 0.5f, jar.SprinkleColour, 8, 1f, 0.05f);
            if (_picked.Remove(jar.Topping))
                jar.SetPicked(false);
            else if (_picked.Count < picksRequired)
            {
                _picked.Add(jar.Topping);
                jar.SetPicked(true);
            }

            _counter.Mug.Interactable = _picked.Count == picksRequired;
            NudgeTarget = _picked.Count == picksRequired ? _counter.Mug.transform : null; // never hint the answer
        }

        private void Serve()
        {
            if (IsComplete || _picked.Count != picksRequired) return;

            foreach (var jar in _counter.ToppingJars) jar.Target.Interactable = false;
            _counter.Mug.Interactable = false;
            AudioManager.Play(SfxId.MugClink);
            Complete(recipe.Score(_picked));
        }
    }
}
