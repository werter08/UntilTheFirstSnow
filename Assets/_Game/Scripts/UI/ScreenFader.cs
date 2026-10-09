using System.Collections;
using Courtyard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Courtyard.UI
{
    /// <summary>Full-screen colour fade with an optional centred message. Blocks input while visible.</summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text message;
        [Tooltip("Optional: images that 'walk' across the black screen (Day 20: two mugs across the courtyard).")]
        [SerializeField] private RectTransform walker;
        [SerializeField] private Image[] walkerImages;

        /// <summary>Carries the sprites across the faded-out screen with a footstep bob.</summary>
        public IEnumerator Walk(Sprite[] sprites, float seconds)
        {
            if (walker == null || sprites == null || sprites.Length == 0) yield break;

            for (var i = 0; i < walkerImages.Length; i++)
            {
                var show = i < sprites.Length && sprites[i] != null;
                walkerImages[i].gameObject.SetActive(show);
                if (show) walkerImages[i].sprite = sprites[i];
            }

            walker.gameObject.SetActive(true);
            var width = ((RectTransform)walker.parent).rect.width;
            yield return Tweens.RunLinear(seconds, t =>
            {
                var steps = t * seconds * 2.2f;
                var bob = Mathf.Abs(Mathf.Sin(steps * Mathf.PI)) * 14f;
                walker.anchoredPosition = new Vector2(Mathf.Lerp(-width * 0.35f, width * 0.35f, t), bob);
                var alpha = Mathf.Min(1f, Mathf.Min(t, 1f - t) * 6f);
                foreach (var img in walkerImages) img.color = new Color(1f, 1f, 1f, alpha);
            });
            walker.gameObject.SetActive(false);
        }

        public IEnumerator FadeIn(float duration)
        {
            var from = group.alpha;
            yield return Tweens.Run(duration, t => group.alpha = Mathf.Lerp(from, 0f, t));
            group.blocksRaycasts = false;
            message.text = string.Empty;
        }

        public IEnumerator FadeOut(Color color, float duration, string text = null)
        {
            background.color = color;
            message.text = text ?? string.Empty;
            group.blocksRaycasts = true;
            var from = group.alpha;
            yield return Tweens.Run(duration, t => group.alpha = Mathf.Lerp(from, 1f, t));
        }
    }
}
