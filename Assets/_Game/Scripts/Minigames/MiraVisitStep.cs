using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Story moment (Day 10): a knock ("took-took"), her window goes dark; the screen fades to black, footsteps, and the
    /// eyes open slowly at the front door — Mira is standing there with her cup. The next step takes the cup.
    /// </summary>
    public sealed class MiraVisitStep : MinigameStep
    {
        [SerializeField, Min(0f)] private float knockDelaySeconds = 0.8f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.8f;
        [SerializeField, Min(0f)] private float walkSeconds = 1.6f;
        [Tooltip("Eyes opening at the door: a slow fade in.")]
        [SerializeField, Min(0f)] private float eyesOpenSeconds = 1.4f;

        public override void Begin(MinigameContext ctx) => StartCoroutine(Visit(ctx));

        private IEnumerator Visit(MinigameContext ctx)
        {
            AudioManager.Play(SfxId.Knock);
            yield return new WaitForSeconds(knockDelaySeconds);
            ctx.Kitchen.SetMiraWindow(MiraWindowState.Dark);

            if (ctx.Fader != null) yield return ctx.Fader.FadeOut(Color.black, fadeOutSeconds);
            AudioManager.Play(SfxId.Footsteps);
            yield return new WaitForSeconds(walkSeconds);
            ctx.Kitchen.ShowDoor(ctx.WorldCamera, true);
            if (ctx.Fader != null) yield return ctx.Fader.FadeIn(eyesOpenSeconds);
            Complete(1f);
        }
    }
}
