using System.Collections;
using System.Collections.Generic;
using Courtyard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.Story
{
    /// <summary>
    /// Shows story beats in the text box with a typewriter effect. A click finishes the line, the next click advances.
    /// </summary>
    public sealed class StoryPlayer : MonoBehaviour
    {
        [SerializeField] private CanvasGroup box;
        [SerializeField] private TMP_Text body;
        [SerializeField] private GameObject speakerRoot;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private GameObject continueHint;
        [SerializeField, Min(1f)] private float charactersPerSecond = 40f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

        private void Awake()
        {
            box.alpha = 0f;
            box.blocksRaycasts = false;
        }

        public IEnumerator Play(IReadOnlyList<StoryBeat> beats)
        {
            if (beats == null || beats.Count == 0) yield break;

            box.blocksRaycasts = true;
            yield return Tweens.Run(fadeDuration, t => box.alpha = t);

            foreach (var beat in beats)
                yield return ShowBeat(beat);

            yield return Tweens.Run(fadeDuration, t => box.alpha = 1f - t);
            box.blocksRaycasts = false;
        }

        private IEnumerator ShowBeat(StoryBeat beat)
        {
            var isDialogue = beat.Style == BeatStyle.Dialogue && !string.IsNullOrEmpty(beat.Speaker);
            speakerRoot.SetActive(isDialogue);
            speakerLabel.text = isDialogue ? beat.Speaker : string.Empty;
            body.fontStyle = beat.Style == BeatStyle.Thought ? FontStyles.Italic : FontStyles.Normal;
            body.text = beat.Text;
            body.maxVisibleCharacters = 0;
            continueHint.SetActive(false);
            body.ForceMeshUpdate();

            var total = body.textInfo.characterCount;
            var shown = 0f;
            yield return null; // don't let the click that opened this beat skip it

            while (shown < total)
            {
                if (PressedThisFrame())
                {
                    shown = total;
                }
                else
                {
                    shown += charactersPerSecond * Time.deltaTime;
                }

                body.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }

            continueHint.SetActive(true);
            while (!PressedThisFrame()) yield return null;
            yield return null;
        }

        private static bool PressedThisFrame() => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
    }
}
