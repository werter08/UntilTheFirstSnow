using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using TMPro;
using UnityEngine;

namespace Courtyard.UI
{
    /// <summary>
    /// The calendar between days: it shows the last day played, then the pages in between are torn off one by one and
    /// fall away (faster and faster when there are many) until the new day is on top; then it flips away and the day
    /// starts. The first day, or a restart, just shows the day.
    /// </summary>
    public sealed class DayCard : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform page;
        [SerializeField] private TMP_Text dayNumber;
        [SerializeField] private TMP_Text clockTime;
        [SerializeField, Min(0f)] private float appearSeconds = 0.35f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.3f;
        [SerializeField, Min(0f)] private float flipSeconds = 0.45f;

        [Header("Tearing off the days in between")]
        [Tooltip("Pause on the old day before the first page goes.")]
        [SerializeField, Min(0f)] private float beforeTearSeconds = 0.6f;
        [Tooltip("Gap between tears: the first one, and the quickest (many pages speed up).")]
        [SerializeField, Min(0.02f)] private float firstTearGap = 0.5f;
        [SerializeField, Min(0.02f)] private float fastestTearGap = 0.16f;
        [Tooltip("How long a torn page takes to fall out of sight.")]
        [SerializeField, Min(0.1f)] private float fallSeconds = 1.1f;
        [SerializeField] private float fallDistance = 1300f;

        private void Awake() => group.alpha = 0f;

        public IEnumerator Show(LevelDefinition level) => Show(level, level.DayNumber);

        /// <summary>Shows <paramref name="fromDay"/>, tears pages until the level's day, then flips into the day.</summary>
        public IEnumerator Show(LevelDefinition level, int fromDay)
        {
            var toDay = level.DayNumber;
            fromDay = Mathf.Clamp(fromDay, 1, toDay);
            dayNumber.text = fromDay.ToString();
            clockTime.text = fromDay == toDay ? level.ClockTime : string.Empty;
            page.localScale = Vector3.one;
            page.localRotation = Quaternion.identity;

            yield return Tweens.Run(appearSeconds, t => group.alpha = t);

            if (fromDay < toDay)
            {
                yield return new WaitForSeconds(beforeTearSeconds);
                var pages = toDay - fromDay;
                for (var day = fromDay + 1; day <= toDay; day++)
                {
                    // The torn sheet (old number) falls; the next day is underneath.
                    var torn = Instantiate(page, page.parent);
                    torn.name = "TornPage";
                    torn.SetAsLastSibling();
                    dayNumber.text = day.ToString();
                    if (day == toDay) clockTime.text = level.ClockTime;
                    AudioManager.Play(SfxId.PaperNote);
                    StartCoroutine(Fall(torn));

                    var progress = pages > 1 ? (day - fromDay - 1) / (float)(pages - 1) : 1f;
                    yield return new WaitForSeconds(Mathf.Lerp(firstTearGap, fastestTearGap, Mathf.Sqrt(progress)));
                }
            }

            yield return new WaitForSeconds(holdSeconds);

            // Page flip: tear upwards, folding away over the top edge.
            AudioManager.Play(SfxId.PaperNote);
            yield return Tweens.Run(flipSeconds, t =>
            {
                page.localScale = new Vector3(1f, Mathf.Lerp(1f, 0f, t), 1f);
                page.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 6f, t));
                group.alpha = 1f - t * t;
            });
            group.alpha = 0f;
        }

        /// <summary>A torn page: a little tug and tilt, then it drops away with a spin and fades.</summary>
        private IEnumerator Fall(RectTransform torn)
        {
            var start = torn.anchoredPosition;
            var side = Random.value < 0.5f ? -1f : 1f;
            var drift = Random.Range(60f, 160f) * side;
            var spin = Random.Range(25f, 50f) * side;
            var canvasGroup = torn.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            yield return Tweens.RunLinear(fallSeconds, t =>
            {
                if (torn == null) return;
                var tug = Mathf.Sin(Mathf.Clamp01(t / 0.15f) * Mathf.PI) * 18f;       // pulled up a bit as it tears
                var drop = t * t * fallDistance;                                       // then gravity
                torn.anchoredPosition = start + new Vector2(drift * t, tug - drop);
                torn.localRotation = Quaternion.Euler(0f, 0f, spin * Mathf.SmoothStep(0f, 1f, t) + Mathf.Sin(t * 9f) * 4f * (1f - t));
                canvasGroup.alpha = 1f - Mathf.InverseLerp(0.6f, 1f, t);
            });
            if (torn != null) Destroy(torn.gameObject);
        }
    }
}
