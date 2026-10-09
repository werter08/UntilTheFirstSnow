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
            AudioManager.Play(SfxId.Perfect);
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
