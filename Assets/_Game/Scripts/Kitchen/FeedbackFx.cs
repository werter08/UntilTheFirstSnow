using System.Collections;
using System.Collections.Generic;
using Courtyard.Audio;
using Courtyard.Core;
using TMPro;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Small "juice" helpers for the kitchen: squash-and-stretch punches and floating text popups ("Perfect!").
    /// </summary>
    public sealed class FeedbackFx : MonoBehaviour
    {
        [Header("Punch")]
        [SerializeField, Range(0f, 0.5f)] private float punchAmount = 0.15f;
        [SerializeField, Min(0.01f)] private float punchSeconds = 0.3f;
        [SerializeField, Min(0f)] private float punchWobbles = 2f;

        [Header("Popup")]
        [SerializeField] private TMP_Text popupTemplate;
        [SerializeField] private string perfectText = "Perfect!";
        [SerializeField, Range(0f, 1f)] private float perfectThreshold = 0.9f;
        [SerializeField] private float popupRise = 0.9f;
        [SerializeField, Min(0.01f)] private float popupSeconds = 1f;

        [Header("Particle bursts")]
        [Tooltip("World-space emitter used for sparkles, dust, splashes and sprinkles (emission off; we Emit()).")]
        [SerializeField] private ParticleSystem burst;
        [SerializeField] private Color perfectSparkle = new(1f, 0.85f, 0.4f);
        [Tooltip("Soft round puffs (radial sprite): warm glow, cold mist, breath.")]
        [SerializeField] private ParticleSystem softBurst;
        [SerializeField] private Color warmGlow = new(1f, 0.72f, 0.42f, 0.55f);
        [SerializeField, Range(0f, 1f)] private float niceThreshold = 0.7f;

        [Header("Cold (a badly made drink)")]
        [SerializeField] private string chillText = "Brr… cold.";
        [SerializeField] private Color chillTextColour = new(0.62f, 0.82f, 1f);
        [SerializeField] private Color frostColour = new(0.8f, 0.92f, 1f);
        [SerializeField] private Color mistColour = new(0.88f, 0.94f, 1f, 0.75f);
        [SerializeField, Min(0f)] private float shakeSeconds = 0.5f;
        [SerializeField, Min(0f)] private float shakeAmount = 0.07f;
        [SerializeField, Min(0f)] private float chillHoldSeconds = 0.6f;

        [Header("Why it went wrong")]
        [SerializeField] private Color mistakeTextColour = new(0.72f, 0.24f, 0.14f);
        [SerializeField, Min(0.1f)] private float mistakeSeconds = 2.8f;
        [SerializeField] private float mistakeRise = 0.3f;
        [SerializeField, Range(0.2f, 2f)] private float mistakeScale = 0.6f;
        [SerializeField] private Color mistakeDust = new(0.55f, 0.55f, 0.6f, 0.6f);

        [Header("Served drink")]
        [SerializeField, Min(0f)] private float serveHoldSeconds = 1.4f;

        private readonly Dictionary<Transform, (Coroutine routine, Vector3 baseScale)> _punches = new();

        private void Awake() => popupTemplate.gameObject.SetActive(false);

        /// <summary>Squash-and-stretch on a transform; restarting a punch keeps the original scale.</summary>
        public void Punch(Transform target, float strength = 1f)
        {
            if (target == null) return;

            var baseScale = target.localScale;
            if (_punches.TryGetValue(target, out var running))
            {
                StopCoroutine(running.routine);
                baseScale = running.baseScale;
            }

            _punches[target] = (StartCoroutine(PunchRoutine(target, baseScale, strength)), baseScale);
        }

        /// <summary>Shows "Perfect!" over <paramref name="worldPosition"/> when the quality earns it.</summary>
        public void CelebrateQuality(float quality, Vector3 worldPosition)
        {
            if (quality < niceThreshold) return;
            // A good drink glows warm; a perfect one also sparkles.
            Puff(worldPosition + Vector3.down * 0.6f, warmGlow, quality >= perfectThreshold ? 10 : 5, 0.7f, 0.45f, -0.15f);
            AudioManager.Play(SfxId.Perfect); // finished in the green zone: the little "well done" jingle
            if (quality < perfectThreshold)
            {
                Burst(worldPosition, perfectSparkle, 6, 1.4f, 0.08f, -0.2f);
                return;
            }

            Popup(perfectText, worldPosition);
            Burst(worldPosition, perfectSparkle, 18, 2.2f, 0.12f, -0.2f);
        }

        /// <summary>A badly made drink: frost crystals and cold mist over the mug, "Brr…", the camera shivers.</summary>
        public IEnumerator Chill(Vector3 mugPosition, Transform shakeTarget)
        {
            var top = mugPosition + Vector3.up * 0.7f;
            Popup(chillText, top + Vector3.up * 0.5f, chillTextColour, 0.7f);
            Burst(top, frostColour, 40, 2.2f, 0.11f, 0.35f);
            Puff(top, mistColour, 16, 1f, 0.7f, -0.1f);
            if (shakeTarget != null) yield return Shake(shakeTarget);
            yield return new WaitForSeconds(chillHoldSeconds);
        }

        /// <summary>Short decaying shiver (restores the exact start position).</summary>
        public IEnumerator Shake(Transform target)
        {
            var start = target.localPosition;
            yield return Tweens.RunLinear(shakeSeconds, t =>
            {
                var offset = Random.insideUnitCircle * shakeAmount * (1f - t);
                target.localPosition = start + new Vector3(offset.x, offset.y, 0f);
            });
            target.localPosition = start;
        }

        /// <summary>Soft round puffs that drift and grow (falls back to square bursts if there is no soft emitter).</summary>
        public void Puff(Vector3 worldPosition, Color colour, int count, float speed = 0.6f, float size = 0.4f, float gravity = -0.1f)
        {
            var system = softBurst != null ? softBurst : burst;
            if (system == null) return;
            var main = system.main;
            main.gravityModifier = gravity;
            for (var i = 0; i < count; i++)
            {
                var direction = Random.insideUnitCircle;
                system.Emit(new ParticleSystem.EmitParams
                {
                    position = worldPosition + (Vector3)(Random.insideUnitCircle * 0.2f),
                    velocity = new Vector3(direction.x * 0.6f, Mathf.Abs(direction.y) * 0.4f + 0.3f, 0f) * speed,
                    startColor = colour,
                    startSize = size * Random.Range(0.6f, 1.2f),
                    startLifetime = Random.Range(0.8f, 1.6f),
                    rotation = Random.Range(0f, 360f),
                }, 1);
            }
        }

        /// <summary>
        /// The served drink, in place: the mug bounces, steams and glows warm (the drink itself is already shown
        /// inside the mug; no floating icon).
        /// </summary>
        public IEnumerator ServeDrink(Transform mug, Vector3 mugTop, ParticleSystem steam = null)
        {
            if (steam != null) steam.Emit(12);
            AudioManager.Play(SfxId.MugClink);
            Punch(mug, 1.2f);
            Puff(mugTop, warmGlow, 8, 0.6f, 0.45f, -0.15f);
            yield return new WaitForSeconds(serveHoldSeconds);
        }

        /// <summary>
        /// Little squares thrown in an arc from <paramref name="from"/> so they land at <paramref name="to"/> after
        /// <paramref name="seconds"/> (e.g. ground coffee falling from the grinder into the V60).
        /// </summary>
        public void Throw(Vector3 from, Vector3 to, Color colour, int count, float seconds = 0.45f, float size = 0.06f)
        {
            if (burst == null) return;
            var main = burst.main;
            main.gravityModifier = 1f;
            var g = Mathf.Abs(Physics.gravity.y);
            for (var i = 0; i < count; i++)
            {
                var target = to + (Vector3)(Random.insideUnitCircle * 0.12f);
                var v = (target - from) / seconds + Vector3.up * (0.5f * g * seconds);
                burst.Emit(new ParticleSystem.EmitParams
                {
                    position = from + (Vector3)(Random.insideUnitCircle * 0.05f),
                    velocity = v,
                    startColor = colour,
                    startSize = size * Random.Range(0.6f, 1.2f),
                    startLifetime = seconds,
                    rotation = Random.Range(0f, 360f),
                }, 1);
            }
        }

        /// <summary>A puff of little squares: sparkles, coffee dust, droplets, sprinkles.</summary>
        public void Burst(Vector3 worldPosition, Color colour, int count, float speed = 1.4f, float size = 0.08f, float gravity = 0.6f)
        {
            if (burst == null) return;
            for (var i = 0; i < count; i++)
            {
                var direction = Random.insideUnitCircle.normalized;
                var parameters = new ParticleSystem.EmitParams
                {
                    position = worldPosition + (Vector3)(Random.insideUnitCircle * 0.08f),
                    velocity = new Vector3(direction.x, Mathf.Abs(direction.y) + 0.3f, 0f) * speed * Random.Range(0.5f, 1f),
                    startColor = colour,
                    startSize = size * Random.Range(0.6f, 1.2f),
                    startLifetime = Random.Range(0.4f, 0.8f),
                    rotation = Random.Range(0f, 360f),
                };
                var main = burst.main;
                main.gravityModifier = gravity;
                burst.Emit(parameters, 1);
            }
        }

        private readonly Dictionary<Transform, Coroutine> _wiggles = new();

        /// <summary>
        /// "Use me": a quick side-to-side wiggle. Stops on its own if the game turns the object meanwhile (the player
        /// picked it up), so it never fights a step's own movement.
        /// </summary>
        public void Wiggle(Transform target, float degrees = 7f, float seconds = 0.55f)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;
            if (_wiggles.TryGetValue(target, out var running) && running != null) return; // already wiggling
            _wiggles[target] = StartCoroutine(WiggleRoutine(target, degrees, seconds));
        }

        private IEnumerator WiggleRoutine(Transform target, float degrees, float seconds)
        {
            var rest = target.localRotation;
            var last = rest;
            for (var elapsed = 0f; elapsed < seconds && target != null; elapsed += Time.deltaTime)
            {
                if (target.localRotation != last) break; // someone else is turning it now
                var k = elapsed / seconds;
                last = rest * Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 6f) * degrees * (1f - k));
                target.localRotation = last;
                yield return null;
            }

            if (target != null && target.localRotation == last) target.localRotation = rest;
            _wiggles.Remove(target);
        }

        /// <summary>A small text that stays above something until removed (e.g. "1 / 2 spoons").</summary>
        public TMP_Text Label(string text, Vector3 worldPosition, Color colour, float scale = 0.55f)
        {
            var label = Instantiate(popupTemplate, popupTemplate.transform.parent);
            label.gameObject.SetActive(true);
            label.text = text;
            label.color = colour;
            label.fontSize *= scale;
            label.transform.position = new Vector3(worldPosition.x, worldPosition.y, popupTemplate.transform.position.z);
            return label;
        }

        /// <summary>What went wrong, where it happened: a line that lingers above the tool, a grey puff and a wobble.</summary>
        public void Explain(string text, Vector3 worldPosition, Transform target = null)
        {
            Popup(text, worldPosition, mistakeTextColour, mistakeScale, mistakeSeconds, mistakeRise);
            Puff(worldPosition + Vector3.down * 0.35f, mistakeDust, 5, 0.4f, 0.3f, -0.05f);
            if (target != null) Punch(target, 0.7f);
        }

        public void Popup(string text, Vector3 worldPosition, Color? colour = null, float scale = 1f, float seconds = -1f, float rise = -1f)
        {
            var popup = Instantiate(popupTemplate, popupTemplate.transform.parent);
            popup.gameObject.SetActive(true);
            popup.text = text;
            if (colour.HasValue) popup.color = colour.Value;
            popup.fontSize *= scale;
            popup.transform.position = new Vector3(worldPosition.x, worldPosition.y, popupTemplate.transform.position.z);
            StartCoroutine(PopupRoutine(popup, seconds > 0f ? seconds : popupSeconds, rise >= 0f ? rise : popupRise));
        }

        private IEnumerator PunchRoutine(Transform target, Vector3 baseScale, float strength)
        {
            var amount = punchAmount * strength;
            yield return Tweens.RunLinear(punchSeconds, t =>
            {
                if (target == null) return;
                var wave = Mathf.Sin(t * Mathf.PI * (1f + punchWobbles * 2f)) * (1f - t) * amount;
                target.localScale = new Vector3(baseScale.x * (1f + wave), baseScale.y * (1f - wave), baseScale.z);
            });

            if (target != null) target.localScale = baseScale;
            _punches.Remove(target);
        }

        private IEnumerator PopupRoutine(TMP_Text popup, float seconds, float rise)
        {
            var start = popup.transform.position;
            var colour = popup.color;
            yield return Tweens.RunLinear(seconds, t =>
            {
                popup.transform.position = start + Vector3.up * (rise * Mathf.Sin(t * Mathf.PI * 0.5f));
                popup.transform.localScale = Vector3.one * (t < 0.15f ? Mathf.Lerp(0.6f, 1.1f, t / 0.15f) : Mathf.Lerp(1.1f, 1f, (t - 0.15f) / 0.85f));
                popup.color = new Color(colour.r, colour.g, colour.b, colour.a * (1f - Mathf.InverseLerp(0.6f, 1f, t)));
            });
            Destroy(popup.gameObject);
        }
    }
}
