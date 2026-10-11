using System.Collections;
using Courtyard.Audio;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Day 20, no input: his mug slides in beside hers and the V60 drips it full of black coffee (~1.5 s). It warms
    /// him by a fixed amount and doesn't count toward her served drink.
    /// </summary>
    public sealed class AutoFillHisMugStep : MinigameStep
    {
        [SerializeField, Min(0.3f)] private float seconds = 1.6f;
        [Tooltip("His cup is plain black coffee: a steady, medium warmth.")]
        [SerializeField, Range(0f, 1f)] private float fixedQuality = 0.6f;
        [SerializeField] private Sprite blackCoffee;
        [SerializeField] private Color dripColour = new(0.3f, 0.18f, 0.1f);

        public override bool CountsTowardDrink => false;
        public override float? FixedQuality => fixedQuality;

        public override void Begin(MinigameContext ctx) => StartCoroutine(Fill(ctx));

        private IEnumerator Fill(MinigameContext ctx)
        {
            AudioManager.Play(SfxId.MugClink);
            AudioManager.ResumeTrack(SfxId.Pour);
            AudioManager.SetTrackVolume(SfxId.Pour, 0.35f); // a thin drip, not a pour
            SpriteRenderer rackMug = null;
            foreach (var (slot, style) in ctx.Kitchen.RackMugs)
                if (style == Core.MugStyle.His && slot.gameObject.activeInHierarchy) rackMug = slot.GetComponent<SpriteRenderer>();
            yield return ctx.Counter.FillHisMug(seconds, blackCoffee,
                spout => ctx.Fx.Burst(spout, dripColour, 1, 0.2f, 0.05f, 2.5f), rackMug);
            AudioManager.StopLoop(SfxId.Pour, 0.2f);
            Complete(fixedQuality);
        }
    }
}
