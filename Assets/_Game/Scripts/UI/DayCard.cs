using System.Collections;
using Courtyard.Core;
using TMPro;
using UnityEngine;

namespace Courtyard.UI
{
    /// <summary>Calendar page shown between days: "Day N" + time, then it flips away.</summary>
    public sealed class DayCard : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform page;
        [SerializeField] private TMP_Text dayNumber;
        [SerializeField] private TMP_Text clockTime;
        [SerializeField, Min(0f)] private float appearSeconds = 0.35f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.3f;
        [SerializeField, Min(0f)] private float flipSeconds = 0.45f;

        private void Awake() => group.alpha = 0f;

        public IEnumerator Show(LevelDefinition level)
        {
            dayNumber.text = level.DayNumber.ToString();
            clockTime.text = level.ClockTime;
            page.localScale = Vector3.one;
            page.localRotation = Quaternion.identity;

            yield return Tweens.Run(appearSeconds, t => group.alpha = t);
            yield return new WaitForSeconds(holdSeconds);

            // Page flip: tear upwards, folding away over the top edge.
            yield return Tweens.Run(flipSeconds, t =>
            {
                page.localScale = new Vector3(1f, Mathf.Lerp(1f, 0f, t), 1f);
                page.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 6f, t));
                group.alpha = 1f - t * t;
            });
            group.alpha = 0f;
        }
    }
}
