using System.Collections;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>Story moment (Day 10): a knock, her window goes dark and Mira appears at the edge of the frame.</summary>
    public sealed class MiraVisitStep : MinigameStep
    {
        [SerializeField, Min(0f)] private float knockDelaySeconds = 0.6f;
        [SerializeField, Min(0f)] private float afterArrivalSeconds = 0.8f;

        public override void Begin(MinigameContext ctx) => StartCoroutine(Visit(ctx));

        private IEnumerator Visit(MinigameContext ctx)
        {
            yield return new WaitForSeconds(knockDelaySeconds);
            ctx.Kitchen.SetMiraWindow(MiraWindowState.Dark);
            ctx.Kitchen.SetMiraAtDoor(true);
            yield return new WaitForSeconds(afterArrivalSeconds);
            Complete(1f);
        }
    }
}
