using Courtyard.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Mira's window across the courtyard (the team's drawings): Lit, Dark, Silhouette, Wave, Note. Wave animates —
    /// her silhouette and the waving frame alternate. A small warm light glows on her window whenever it is lit.
    /// </summary>
    public sealed class MiraWindow : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer window;
        [SerializeField] private Sprite lit;
        [SerializeField] private Sprite dark;
        [SerializeField] private Sprite silhouette;
        [SerializeField] private Sprite wave;
        [SerializeField] private Sprite note;
        [SerializeField] private Light2D glow;
        [SerializeField, Min(0.05f)] private float waveFrameSeconds = 0.4f;

        private float _timer;
        private bool _waveFrame;

        public MiraWindowState State { get; private set; }

        /// <summary>True once the drawn window sprites are wired (otherwise the placeholder window is used).</summary>
        public bool HasArt => lit != null && dark != null && silhouette != null && wave != null && note != null;

        public void SetState(MiraWindowState state)
        {
            State = state;
            _timer = 0f;
            _waveFrame = true;
            Refresh();
        }

        private void Update()
        {
            if (State != MiraWindowState.Wave) return;
            _timer += Time.deltaTime;
            if (_timer < waveFrameSeconds) return;
            _timer -= waveFrameSeconds;
            _waveFrame = !_waveFrame;
            Refresh();
        }

        private void Refresh()
        {
#pragma warning disable CS0618 // Empty is kept only for old data: it shows as Dark.
            window.sprite = State switch
            {
                MiraWindowState.Lit => lit,
                MiraWindowState.Silhouette => silhouette,
                MiraWindowState.Wave => _waveFrame ? wave : silhouette,
                MiraWindowState.Note => note,
                _ => dark,
            };
            if (glow != null) glow.enabled = State is not (MiraWindowState.Dark or MiraWindowState.Empty);
#pragma warning restore CS0618
        }
    }
}
