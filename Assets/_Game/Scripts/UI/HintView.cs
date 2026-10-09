using Courtyard.Core;
using TMPro;
using UnityEngine;

namespace Courtyard.UI
{
    /// <summary>Small step hint at the top of the screen.</summary>
    public sealed class HintView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0f)] private float fadeDuration = 0.3f;

        private Coroutine _fade;

        public void Show(string text)
        {
            label.text = text;
            FadeTo(string.IsNullOrWhiteSpace(text) ? 0f : 1f);
        }

        public void Hide() => FadeTo(0f);

        private void FadeTo(float alpha)
        {
            if (_fade != null) StopCoroutine(_fade);
            var from = group.alpha;
            _fade = StartCoroutine(Tweens.Run(fadeDuration, t => group.alpha = Mathf.Lerp(from, alpha, t)));
        }
    }
}
