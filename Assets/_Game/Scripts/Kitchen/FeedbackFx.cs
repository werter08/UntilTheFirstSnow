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

        [Header("Served drink")]
        [SerializeField] private string drinkSortingLayer = "Foreground";
        [SerializeField] private int drinkSortingOrder = 70;
        [SerializeField, Min(0.1f)] private float drinkSpacing = 1.3f;
        [SerializeField, Min(0f)] private float drinkPopSeconds = 0.35f;
        [SerializeField, Min(0f)] private float drinkHoldSeconds = 1.5f;
        [SerializeField, Min(0f)] private float drinkFadeSeconds = 0.4f;

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
            if (quality < perfectThreshold) return;
            Popup(perfectText, worldPosition);
            Burst(worldPosition, perfectSparkle, 18, 2.2f, 0.12f, -0.2f);
            AudioManager.Play(SfxId.Perfect);
        }

        /// <summary>
        /// The served drink: icons pop up (scale 0 → 1 with a little overshoot) side by side above
        /// <paramref name="worldPosition"/>, hold, then fade out. Optional steam puff from <paramref name="steam"/>.
        /// </summary>
        public IEnumerator ShowDrinks(IReadOnlyList<Sprite> icons, Vector3 worldPosition, ParticleSystem steam = null)
        {
            if (icons == null || icons.Count == 0) yield break;

            var renderers = new List<SpriteRenderer>();
            for (var i = 0; i < icons.Count; i++)
            {
                var go = new GameObject("Drink_" + icons[i].name);
                go.transform.position = worldPosition + Vector3.right * (i - (icons.Count - 1) * 0.5f) * drinkSpacing;
                go.transform.localScale = Vector3.zero;
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = icons[i];
                renderer.sortingLayerName = drinkSortingLayer;
                renderer.sortingOrder = drinkSortingOrder;
                renderers.Add(renderer);
            }

            if (steam != null) steam.Emit(12);
            AudioManager.Play(SfxId.MugClink);
            yield return Tweens.RunLinear(drinkPopSeconds, t =>
            {
                var overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                foreach (var r in renderers) r.transform.localScale = Vector3.one * (t * overshoot);
            });
            yield return new WaitForSeconds(drinkHoldSeconds);
            yield return Tweens.Run(drinkFadeSeconds, t =>
            {
                foreach (var r in renderers) r.color = new Color(1f, 1f, 1f, 1f - t);
            });
            foreach (var r in renderers) Destroy(r.gameObject);
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

        public void Popup(string text, Vector3 worldPosition)
        {
            var popup = Instantiate(popupTemplate, popupTemplate.transform.parent);
            popup.gameObject.SetActive(true);
            popup.text = text;
            popup.transform.position = new Vector3(worldPosition.x, worldPosition.y, popupTemplate.transform.position.z);
            StartCoroutine(PopupRoutine(popup));
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

        private IEnumerator PopupRoutine(TMP_Text popup)
        {
            var start = popup.transform.position;
            var colour = popup.color;
            yield return Tweens.RunLinear(popupSeconds, t =>
            {
                popup.transform.position = start + Vector3.up * (popupRise * Mathf.Sin(t * Mathf.PI * 0.5f));
                popup.transform.localScale = Vector3.one * (t < 0.15f ? Mathf.Lerp(0.6f, 1.1f, t / 0.15f) : Mathf.Lerp(1.1f, 1f, (t - 0.15f) / 0.85f));
                popup.color = new Color(colour.r, colour.g, colour.b, colour.a * (1f - Mathf.InverseLerp(0.6f, 1f, t)));
            });
            Destroy(popup.gameObject);
        }
    }
}
