using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Click one of the mugs. Hers (leaf print, seen on Day 10) is correct; a wrong pick still continues (no hard fail).
    /// Mug positions are shuffled each time.
    /// </summary>
    public sealed class ChooseMugStep : MinigameStep
    {
        [SerializeField] private PointerTarget[] mugs;
        [SerializeField, Min(0)] private int herMugIndex;
        [SerializeField, Range(0f, 1f)] private float wrongQuality = 0.6f;
        [SerializeField] private float pickedLift = 0.3f;
        [SerializeField, Min(0f)] private float liftSeconds = 0.25f;

        private FeedbackFx _fx;

        public override void Begin(MinigameContext ctx)
        {
            _fx = ctx.Fx;
            ShufflePositions();
            for (var i = 0; i < mugs.Length; i++)
            {
                var index = i;
                mugs[i].Clicked += () => Pick(index);
            }
        }

        private void ShufflePositions()
        {
            var positions = new Vector3[mugs.Length];
            for (var i = 0; i < mugs.Length; i++) positions[i] = mugs[i].transform.localPosition;

            for (var i = positions.Length - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (positions[i], positions[j]) = (positions[j], positions[i]);
            }

            for (var i = 0; i < mugs.Length; i++) mugs[i].transform.localPosition = positions[i];
        }

        private void Pick(int index)
        {
            if (IsComplete) return;

            foreach (var mug in mugs) mug.Interactable = false;
            AudioManager.Play(SfxId.MugClink);
            StartCoroutine(LiftThenComplete(mugs[index].transform, index == herMugIndex ? 1f : wrongQuality));
        }

        private System.Collections.IEnumerator LiftThenComplete(Transform mug, float quality)
        {
            var from = mug.localPosition;
            var to = from + Vector3.up * pickedLift;
            _fx.Punch(mug);
            yield return Tweens.Run(liftSeconds, t => mug.localPosition = Vector3.Lerp(from, to, t));
            Complete(quality);
        }
    }
}
