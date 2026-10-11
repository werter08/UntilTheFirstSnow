using System.Collections;
using System.Collections.Generic;
using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Day 20: pick her mug (maple leaf, seen on Day 10) from the mugs on the wall rack. A wrong pick is gentle (no hard
    /// fail). Uses the real rack, so no mug ever appears twice; the prefab's own mugs are only a fallback.
    /// </summary>
    public sealed class ChooseMugStep : MinigameStep
    {
        [Tooltip("Fallback mugs if the rack is empty (placeholder scenes).")]
        [SerializeField] private PointerTarget[] mugs;
        [SerializeField, Min(0)] private int herMugIndex;
        [SerializeField, Range(0f, 1f)] private float wrongQuality = 0.6f;
        [SerializeField] private float pickedLift = 0.3f;
        [SerializeField, Min(0f)] private float liftSeconds = 0.25f;
        [Tooltip("Day 20: the picked mug flies from the rack to the brewing spot.")]
        [SerializeField, Min(0.05f)] private float flySeconds = 0.6f;
        [Header("Why it went wrong")]
        [SerializeField] private string wrongMugText = "Hmm... that's not her mug.";

        private readonly List<(PointerTarget target, System.Action handler)> _subscriptions = new();
        private FeedbackFx _fx;
        private CounterStation _counter;
        private bool _pourOverNext;

        /// <summary>Remaking the drink: her mug is already on the counter.</summary>
        public override bool SkipOnRemake(CounterStation counter) => counter.MugPresent;

        public override void Begin(MinigameContext ctx)
        {
            _fx = ctx.Fx;
            _counter = ctx.Counter;
            var steps = ctx.Level.Steps;
            for (var i = ctx.StepIndex + 1; i < steps.Count; i++)
                if (steps[i] is PourOverStep) _pourOverNext = true;
            var rack = ctx.Kitchen.RackMugs;
            if (rack.Count > 0)
            {
                foreach (var mug in mugs) mug.gameObject.SetActive(false);
                foreach (var (slot, style) in rack)
                {
                    var target = slot.GetComponent<PointerTarget>();
                    if (target == null) continue;
                    var isHers = style == MugStyle.Hers;
                    Subscribe(target, () => Pick(slot, isHers));
                }

                return;
            }

            for (var i = 0; i < mugs.Length; i++)
            {
                var index = i;
                mugs[i].gameObject.SetActive(true);
                Subscribe(mugs[i], () => Pick(mugs[index].transform, index == herMugIndex));
            }
        }

        private void Subscribe(PointerTarget target, System.Action handler)
        {
            target.Clicked += handler;
            target.Interactable = true;
            _subscriptions.Add((target, handler));
        }

        private void OnDestroy()
        {
            foreach (var (target, handler) in _subscriptions)
                if (target != null) target.Clicked -= handler;
        }

        private void Pick(Transform mug, bool hers)
        {
            if (IsComplete) return;
            AudioManager.Play(SfxId.MugClink);
            var quality = hers ? 1f : wrongQuality;
            if (!hers) Explain(_fx, wrongMugText, mug.GetComponent<SpriteRenderer>());
            foreach (var (target, _) in _subscriptions) if (target != null) target.Interactable = false;
            StartCoroutine(_counter != null && !_counter.MugPresent ? FlyToCounter(mug, quality) : LiftThenComplete(mug, quality));
        }

        /// <summary>The mug leaves the rack and lands on the brewing spot, where the next steps fill it.</summary>
        private IEnumerator FlyToCounter(Transform mug, float quality)
        {
            var renderer = mug.GetComponent<SpriteRenderer>();
            var from = mug.position;
            var fromScale = mug.localScale;
            _counter.SetMugPresent(true); // measure the brewing mug's size and spot, then hide it until the landing
            var target = _counter.MugBody.bounds;
            _counter.SetMugPresent(false);
            var to = new Vector3(target.center.x, target.center.y, from.z);
            var grow = renderer != null && renderer.bounds.size.y > 0f ? target.size.y / renderer.bounds.size.y : 1f;
            _fx.Punch(mug);
            yield return Tweens.Run(flySeconds, k =>
            {
                mug.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.8f);
                mug.localScale = Vector3.Lerp(fromScale, fromScale * grow, k);
            });
            mug.gameObject.SetActive(false);
            mug.position = from;
            mug.localScale = fromScale;
            _counter.PlaceMug(renderer != null ? renderer.sprite : null, _pourOverNext);
            AudioManager.Play(SfxId.PutDown);
            _fx.Punch(_counter.Mug.transform, 0.6f);
            yield return new WaitForSeconds(0.2f);
            Complete(quality);
        }

        private IEnumerator LiftThenComplete(Transform mug, float quality)
        {
            var from = mug.localPosition;
            var to = from + Vector3.up * pickedLift;
            _fx.Punch(mug);
            yield return Tweens.Run(liftSeconds, t => mug.localPosition = Vector3.Lerp(from, to, t));
            yield return new WaitForSeconds(0.3f);
            yield return Tweens.Run(liftSeconds, t => mug.localPosition = Vector3.Lerp(to, from, t));
            Complete(quality);
        }
    }
}
