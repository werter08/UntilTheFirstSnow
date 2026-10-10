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

        [Tooltip("Mira waves this long (silhouette ↔ waving), pauses, and waves again until you raise your mug.")]
        [SerializeField, Min(0.5f)] private float waveSeconds = 2.5f;
        [SerializeField, Min(0f)] private float pauseSeconds = 2f;

        private MinigameContext _ctx;
        private Coroutine _waving;

        public override void Begin(MinigameContext ctx)
        {
            _ctx = ctx;
            _waving = StartCoroutine(MiraKeepsWaving());
            ctx.Counter.Mug.Clicked += OnClicked;
            ctx.Counter.Mug.Interactable = true;
            NudgeTarget = ctx.Counter.Mug.transform;
        }

        private void OnDestroy()
        {
            if (_ctx == null) return;
            _ctx.Counter.Mug.Clicked -= OnClicked;
            _ctx.Hands.Detach();
        }

        private IEnumerator MiraKeepsWaving()
        {
            while (true)
            {
                yield return _ctx.Kitchen.MiraWaves(waveSeconds);
                yield return new WaitForSeconds(pauseSeconds);
            }
        }

        private void OnClicked()
        {
            if (_waving != null) StopCoroutine(_waving);
            _ctx.Counter.Mug.Interactable = false;
            AudioManager.Play(SfxId.MugClink);
            StartCoroutine(WaveBack());
        }

        private IEnumerator WaveBack()
        {
            var mug = _ctx.Counter.Mug.transform;
            _ctx.Hands.Attach(mug, HandPose.RaiseMug);
            var start = mug.localPosition;
            var end = start + Vector3.up * raiseHeight;
            yield return Tweens.Run(raiseSeconds, t => mug.localPosition = Vector3.Lerp(start, end, t));
            yield return new WaitForSeconds(holdSeconds);
            yield return Tweens.Run(raiseSeconds, t => mug.localPosition = Vector3.Lerp(end, start, t));
            _ctx.Hands.Detach();

            _ctx.Kitchen.SetMiraWindow(MiraWindowState.Silhouette); // she stays at her window
            Complete(1f);
        }
    }
}
