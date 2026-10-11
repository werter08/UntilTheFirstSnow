using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Spices by hand: carry a jar to the cup and let go over it to shake in one portion (it settles on the drink);
    /// repeat for the second spice, then click the cup to serve. Scored against a <see cref="Recipe"/>.
    /// Too much (two portions of one spice, or more than <see cref="picksRequired"/> in total) or too little
    /// (serving with fewer) ruins the drink and the day restarts.
    /// Used by GuessHerDrink (her hint in the story) and HerDrinkFromMemory (no hint).
    /// </summary>
    public sealed class PickToppingsStep : MinigameStep
    {
        [SerializeField] private Recipe recipe;
        [SerializeField, Min(1)] private int picksRequired = 2;
        [Tooltip("Most portions of any single spice before the drink is ruined.")]
        [SerializeField, Min(1)] private int maxPortionsPerSpice = 1;
        [SerializeField] private float shakeTilt = -55f;
        [SerializeField, Min(0f)] private float shakeSeconds = 0.35f;
        [SerializeField, Min(0f)] private float returnSeconds = 0.25f;

        private readonly Dictionary<Topping, int> _portions = new();
        private readonly List<(ToppingJar jar, System.Action<Vector2> start, System.Action<Vector2> drag, System.Action<Vector2> end)> _subscriptions = new();
        private readonly Dictionary<ToppingJar, Vector3> _rest = new();
        private CounterStation _counter;
        private UI.HintView _hint;
        private FeedbackFx _fx;
        [Tooltip("Once the spices are in: what to do next.")]
        [SerializeField] private string serveHintText = "Done? Click the cup to serve it.";

        [Header("Why it went wrong ({0} = the spice)")]
        [SerializeField] private string tooMuchOneText = "Too much {0} — one portion each.";
        [SerializeField] private string tooManyText = "Too many spices — just two.";
        [SerializeField] private string tooFewText = "Not enough — it needs two spices.";
        [SerializeField] private string wrongTasteText = "Not quite her taste...";
        private Topping? _overdone;

        [Tooltip("Tilt (degrees) of a jar while it is carried.")]
        [SerializeField] private float carryTilt = -12f;
        private Vector2 _grabOffset;
        private bool _busy;
        private bool _spoiled;

        private int TotalPortions => _portions.Values.Sum();

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _hint = ctx.Hint;
            _fx = ctx.Fx;
            foreach (var jar in _counter.ToppingJars)
            {
                jar.SetPicked(false);
                _rest[jar] = jar.transform.position;
                if (jar.Drag == null) continue;

                var captured = jar;
                System.Action<Vector2> start = w => OnJarDragStarted(captured, w);
                System.Action<Vector2> drag = w => OnJarDragged(captured, w);
                System.Action<Vector2> end = w => OnJarDragEnded(captured, w);
                jar.Drag.DragStarted += start;
                jar.Drag.Dragged += drag;
                jar.Drag.DragEnded += end;
                jar.Drag.Interactable = true;
                _subscriptions.Add((jar, start, drag, end));
            }

            _counter.Mug.Clicked += Serve;
            _counter.Mug.Interactable = false;
        }

        private void OnDestroy()
        {
            if (_counter == null) return;
            foreach (var (jar, start, drag, end) in _subscriptions)
            {
                jar.Drag.DragStarted -= start;
                jar.Drag.Dragged -= drag;
                jar.Drag.DragEnded -= end;
                jar.transform.position = _rest[jar];
                jar.transform.localRotation = Quaternion.identity;
                jar.Drag.Interactable = false;
            }

            _counter.Mug.Clicked -= Serve;
        }

        private void OnJarDragStarted(ToppingJar jar, Vector2 world)
        {
            if (_busy || IsComplete) return;
            _grabOffset = (Vector2)jar.transform.position - world;
            // Picked up: tipped toward the cup, ready to shake (the lift and sound come from InteractionFeedback).
            StartCoroutine(Tilt(jar.transform, Quaternion.Euler(0f, 0f, carryTilt)));
        }

        private static IEnumerator Tilt(Transform jar, Quaternion to)
        {
            var from = jar.localRotation;
            yield return Tweens.Run(0.12f, k => jar.localRotation = Quaternion.Slerp(from, to, k));
        }

        private void OnJarDragged(ToppingJar jar, Vector2 world)
        {
            if (_busy || IsComplete) return;
            jar.transform.position = world + _grabOffset;
            if (OverCup(jar, world)) StartCoroutine(ShakeIn(jar)); // reached the cup: in it goes, no need to let go
        }

        /// <summary>Forgiving: the jar or the hand holding it over the cup counts.</summary>
        private bool OverCup(ToppingJar jar, Vector2 world) =>
            _counter.IsAboveMug(jar.transform.position, 0.8f) || _counter.IsAboveMug(world, 0.8f) || _counter.IsOverMug(world);

        private void OnJarDragEnded(ToppingJar jar, Vector2 world)
        {
            if (_busy || IsComplete) return;
            if (OverCup(jar, world))
                StartCoroutine(ShakeIn(jar));
            else
                StartCoroutine(ReturnJar(jar));
        }

        private IEnumerator ShakeIn(ToppingJar jar)
        {
            _busy = true;
            var t = jar.transform;
            var above = _counter.MugTop + new Vector3(0.1f, 0.9f, 0f);
            var from = t.position;
            yield return Tweens.Run(shakeSeconds * 0.5f, k => t.position = Vector3.Lerp(from, above, k));
            yield return Tweens.Run(shakeSeconds, k => t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI) * shakeTilt));

            _portions[jar.Topping] = _portions.TryGetValue(jar.Topping, out var n) ? n + 1 : 1;
            jar.SetPortions(_portions[jar.Topping]);
            _counter.AddSpice(jar.SprinkleColour);
            AudioManager.Play(SfxId.Topping);
            CameraShake.Kick(0.02f, 0.15f);
            _fx.Burst(_counter.MugTop, jar.SprinkleColour, 10, 0.8f, 0.05f, 1f);
            _fx.Punch(_counter.Mug.transform, 0.5f);

            yield return ReturnJar(jar);
            _busy = false;

            // Too much (two portions of one spice, or more than two spices) spoils it — judged when served.
            if (_portions[jar.Topping] > maxPortionsPerSpice) _overdone ??= jar.Topping;
            if (_portions[jar.Topping] > maxPortionsPerSpice || TotalPortions > picksRequired) _spoiled = true;

            _counter.Mug.Interactable = TotalPortions > 0;
            NudgeTarget = TotalPortions == picksRequired ? _counter.Mug.transform : null; // never hint the answer
            // Two spices in: say it plainly — the cup is clicked to serve (and it wiggles).
            if (TotalPortions == picksRequired && _hint != null) _hint.Show(serveHintText);
        }

        private IEnumerator ReturnJar(ToppingJar jar)
        {
            var t = jar.transform;
            var from = t.position;
            var fromRotation = t.localRotation;
            yield return Tweens.Run(returnSeconds, k =>
            {
                t.position = Vector3.Lerp(from, _rest[jar], k);
                t.localRotation = Quaternion.Slerp(fromRotation, Quaternion.identity, k);
            });
        }

        private void Serve()
        {
            if (IsComplete || _busy || TotalPortions == 0) return;

            foreach (var jar in _counter.ToppingJars)
                if (jar.Drag != null) jar.Drag.Interactable = false;
            _counter.Mug.Interactable = false;

            if (_spoiled || TotalPortions < picksRequired)
            {
                var why = _overdone.HasValue ? string.Format(tooMuchOneText, _overdone.Value.ToString().ToLowerInvariant())
                    : TotalPortions > picksRequired ? tooManyText
                    : tooFewText;
                Explain(_fx, why, _counter.MugBody);
                Ruin(); // too much or too little: a cold drink, the day goes on
                return;
            }

            AudioManager.Play(SfxId.MugClink);
            var quality = recipe.Score(_portions.Keys.ToList());
            if (ShouldExplain(quality)) Explain(_fx, wrongTasteText, _counter.MugBody);
            Complete(quality);
        }
    }
}
