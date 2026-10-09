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
