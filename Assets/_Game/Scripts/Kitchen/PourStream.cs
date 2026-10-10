using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// The water falling from the kettle's spout, as drops. The steps switch the stream with its SpriteRenderer
    /// (enabled = pouring, colour alpha = fading as the kettle runs dry); the renderer itself never draws — the drops do.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PourStream : MonoBehaviour
    {
        [SerializeField] private ParticleSystem drops;
        [SerializeField, Min(0f)] private float dropsPerSecond = 45f;

        private SpriteRenderer _switch;

        private void Awake()
        {
            _switch = GetComponent<SpriteRenderer>();
            _switch.forceRenderingOff = true; // no more solid rectangle
        }

        private void LateUpdate()
        {
            if (drops == null) return;
            var pouring = _switch.enabled && isActiveAndEnabled;
            var emission = drops.emission;
            emission.rateOverTime = pouring ? dropsPerSecond * _switch.color.a : 0f;
        }
    }
}
