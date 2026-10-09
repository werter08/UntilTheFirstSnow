using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>Story moment (Day 5): Mira waves from her window; click your mug on the counter to raise it and wave back.</summary>
    public sealed class WaveBackStep : MinigameStep
    {
        [SerializeField] private float raiseHeight = 1.2f;
        [SerializeField, Min(0f)] private float raiseSeconds = 0.4f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.2f;

        private MinigameContext _ctx;
        private MiraWindowState _windowBefore;

        public override void Begin(MinigameContext ctx)
        {
            _ctx = ctx;
            _windowBefore = ctx.Kitchen.WindowState;
            ctx.Kitchen.SetMiraWindow(MiraWindowState.Waving);
            ctx.Counter.Mug.Clicked += OnClicked;
            ctx.Counter.Mug.Interactable = true;
        }

        private void OnDestroy()
        {
            if (_ctx != null) _ctx.Counter.Mug.Clicked -= OnClicked;
        }

        private void OnClicked()
        {
            _ctx.Counter.Mug.Interactable = false;
            AudioManager.Play(SfxId.MugClink);
            StartCoroutine(WaveBack());
        }

        private IEnumerator WaveBack()
        {
            var mug = _ctx.Counter.Mug.transform;
            var start = mug.localPosition;
            var end = start + Vector3.up * raiseHeight;
            yield return Tweens.Run(raiseSeconds, t => mug.localPosition = Vector3.Lerp(start, end, t));
            yield return new WaitForSeconds(holdSeconds);
            yield return Tweens.Run(raiseSeconds, t => mug.localPosition = Vector3.Lerp(end, start, t));

            _ctx.Kitchen.SetMiraWindow(_windowBefore == MiraWindowState.Waving ? MiraWindowState.LitWarmSilhouette : _windowBefore);
            Complete(1f);
        }
    }
}
