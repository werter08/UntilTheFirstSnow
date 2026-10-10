using Courtyard.UI;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// When the coldest warmth meter runs low, his breath shows: small mist puffs rise from the bottom of the view,
    /// faster the colder it gets. Pure feedback; no gameplay effect.
    /// </summary>
    public sealed class ColdBreath : MonoBehaviour
    {
        [SerializeField] private WarmthVignette vignette;
        [SerializeField] private FeedbackFx fx;
        [SerializeField, Range(0f, 1f)] private float breathBelow = 0.35f;
        [Tooltip("Seconds between breaths just below the threshold and at zero warmth.")]
        [SerializeField] private Vector2 intervalWarmCold = new(3.2f, 1.4f);
        [SerializeField] private Color breathColour = new(0.92f, 0.96f, 1f, 0.65f);

        private float _timer = 0.5f;

        private void Update()
        {
            if (vignette == null || fx == null || !vignette.IsBound) return;

            var warmth = vignette.Coldest;
            if (warmth >= breathBelow)
            {
                _timer = 0.5f;
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            var cold = Mathf.InverseLerp(breathBelow, 0f, warmth);
            _timer = Mathf.Lerp(intervalWarmCold.x, intervalWarmCold.y, cold);
            fx.Puff(transform.position, breathColour, Mathf.RoundToInt(Mathf.Lerp(3f, 7f, cold)), 1.1f, 0.5f, -0.05f);
        }
    }
}
