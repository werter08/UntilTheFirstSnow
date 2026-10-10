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

        private readonly List<(PointerTarget target, System.Action handler)> _subscriptions = new();
        private FeedbackFx _fx;

        public override void Begin(MinigameContext ctx)
        {
            _fx = ctx.Fx;
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
            StartCoroutine(LiftThenComplete(mug, hers ? 1f : wrongQuality));
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
