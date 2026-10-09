using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// The electric kettle's look: switch light (green = ready, red = heating), the badge pulsing while the temperature
    /// rises, the lid trembling and then rattling as it boils, spout steam, and the "hand holding the kettle" sprite
    /// while pouring. Sits on the kettle root, whose origin is the pouring spout tip.
    /// </summary>
    public sealed class KettleView : MonoBehaviour
    {
        [Header("Standing kettle")]
        [SerializeField] private GameObject idle;
        [Tooltip("Lid + knob; shakes as the water boils.")]
        [SerializeField] private Transform lid;
        [SerializeField] private SpriteRenderer badge;
        [SerializeField] private ParticleSystem spoutSteam;

        [Header("Switch")]
        [SerializeField] private SpriteRenderer switchRenderer;
        [SerializeField] private Sprite switchReady;
        [SerializeField] private Sprite switchOn;

        [Header("Pouring")]
        [SerializeField] private SpriteRenderer pourGrey;
        [SerializeField] private SpriteRenderer pourBeige;
        [SerializeField] private Transform stream;
        [SerializeField] private float pourTilt = 18f;

        [Header("Feel")]
        [SerializeField] private Color badgeIdle = Color.white;
        [SerializeField] private Color badgeHeating = new(1f, 0.55f, 0.35f);
        [SerializeField, Min(0.1f)] private float badgePulseSpeed = 4f;
        [Tooltip("Lid lift while trembling (just before the boil).")]
        [SerializeField, Min(0f)] private float trembleLift = 0.015f;
        [Tooltip("Lid lift and tilt once it is really boiling.")]
        [SerializeField, Min(0f)] private float rattleLift = 0.07f;
        [SerializeField, Min(0f)] private float rattleAngle = 7f;
        [SerializeField, Min(0.1f)] private float rattleSpeed = 38f;
        [SerializeField, Min(0f)] private float maxSteamRate = 28f;

        private Vector3 _lidRest;
        private Sweater _sweater;
        private bool _heating;
        private float _boil;

        private void Awake() => _lidRest = lid.localPosition;

        public void Prepare(Sweater sweater)
        {
            _sweater = sweater;
            SetHeating(false);
            SetBoil(0f);
            SetPouring(false);
        }

        public void SetHeating(bool heating)
        {
            _heating = heating;
            if (switchReady != null && switchOn != null) switchRenderer.sprite = heating ? switchOn : switchReady;
        }

        /// <summary>0 = still, between 0 and 1 the lid trembles, 1 = rattling at the boil.</summary>
        public void SetBoil(float amount) => _boil = Mathf.Clamp01(amount);

        public void SetPouring(bool pouring)
        {
            idle.SetActive(!pouring);
            pourGrey.enabled = pouring && _sweater == Sweater.Grey;
            pourBeige.enabled = pouring && _sweater == Sweater.Beige;
            transform.localRotation = Quaternion.Euler(0f, 0f, pouring ? -pourTilt : 0f);
            stream.localRotation = Quaternion.Euler(0f, 0f, pouring ? pourTilt : 0f);
        }

        private void Update()
        {
            var t = Time.time;
            var rattling = _boil >= 1f;
            var lift = rattling ? rattleLift : trembleLift * _boil;
            var bounce = Mathf.Abs(Mathf.Sin(t * rattleSpeed)) * lift;
            var angle = rattling ? Mathf.Sin(t * rattleSpeed * 0.63f) * rattleAngle : 0f;
            lid.localPosition = _lidRest + new Vector3(0f, bounce, 0f);
            lid.localRotation = Quaternion.Euler(0f, 0f, angle);

            badge.color = _heating
                ? Color.Lerp(badgeIdle, badgeHeating, 0.5f + 0.5f * Mathf.Sin(t * badgePulseSpeed))
                : badgeIdle;

            var emission = spoutSteam.emission;
            emission.rateOverTime = _boil * maxSteamRate;
        }
    }
}
