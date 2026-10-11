using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Day 10 at the door: click Mira's cup to take it. Back in the kitchen (fade, footsteps) her cup stands on the counter
    /// next to his finished coffee — the next steps fill hers.
    /// </summary>
    public sealed class TakeHerCupStep : MinigameStep
    {
        [SerializeField, Min(0f)] private float liftSeconds = 0.3f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.7f;
        [SerializeField, Min(0f)] private float walkSeconds = 1.2f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.9f;

        private MinigameContext _ctx;
        private PointerTarget _cup;

        public override void Begin(MinigameContext ctx)
        {
            _ctx = ctx;
            _cup = ctx.Kitchen.HerCupAtDoor;
            if (_cup == null)
            {
                StartCoroutine(BackToKitchen()); // no door scene: just bring her cup to the counter
                return;
            }

            _cup.Clicked += OnCupClicked;
            _cup.Interactable = true;
            NudgeTarget = _cup.transform;
        }

        private void OnDestroy()
        {
            if (_cup != null) _cup.Clicked -= OnCupClicked;
        }

        private void OnCupClicked()
        {
            if (IsComplete) return;
            _cup.Interactable = false;
            _cup.Clicked -= OnCupClicked;
            AudioManager.Play(SfxId.MugClink);
            StartCoroutine(BackToKitchen());
        }

        private IEnumerator BackToKitchen()
        {
            if (_cup != null)
            {
                var t = _cup.transform;
                var from = t.localPosition;
                _ctx.Fx.Punch(t);
                yield return Tweens.Run(liftSeconds, k => t.localPosition = from + Vector3.up * (0.3f * k));
                t.localPosition = from;
            }

            if (_ctx.Fader != null) yield return _ctx.Fader.FadeOut(Color.black, fadeOutSeconds);
            AudioManager.ResumeTrack(SfxId.Footsteps);
            _ctx.Kitchen.ShowDoor(_ctx.WorldCamera, false);
            _ctx.Counter.BringHerCup();
            yield return new WaitForSeconds(walkSeconds);
            AudioManager.StopLoop(SfxId.Footsteps, 0.4f); // the steps stop as the kitchen fades back in
            if (_ctx.Fader != null) yield return _ctx.Fader.FadeIn(fadeInSeconds);
            Complete(1f);
        }
    }
}
