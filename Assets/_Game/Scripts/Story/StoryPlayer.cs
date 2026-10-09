using System.Collections;
using System.Collections.Generic;
using Courtyard.Audio;
using Courtyard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Courtyard.Story
{
    /// <summary>
    /// Shows story beats in the text box with a typewriter effect. A click finishes the line, the next click advances.
    /// </summary>
    public sealed class StoryPlayer : MonoBehaviour
    {
        [SerializeField] private CanvasGroup box;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text body;
        [SerializeField] private GameObject speakerRoot;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private GameObject continueHint;
        [SerializeField, Min(1f)] private float charactersPerSecond = 40f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

        [Header("Style colours (background / text)")]
        [SerializeField] private Color defaultBackground = new(0.1f, 0.1f, 0.12f, 0.88f);
        [SerializeField] private Color defaultText = new(0.95f, 0.93f, 0.88f);
        [Tooltip("Note beats look like a paper card.")]
        [SerializeField] private Color noteBackground = new(0.96f, 0.93f, 0.82f, 0.97f);
        [SerializeField] private Color noteText = new(0.2f, 0.17f, 0.14f);
        [Tooltip("Phone beats look like a chat bubble.")]
        [SerializeField] private Color phoneBackground = new(0.3f, 0.45f, 0.75f, 0.95f);
        [SerializeField] private Color phoneText = Color.white;

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
            ApplyStyleColours(beat.Style);
            if (beat.Style == BeatStyle.Note) AudioManager.Play(SfxId.PaperNote);
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
            var hintText = continueHint.GetComponent<TMP_Text>();
            var hintColour = hintText != null ? hintText.color : Color.white;
            while (!PressedThisFrame())
            {
                // Gentle pulse so first-time players notice they can click on.
                if (hintText != null)
                    hintText.color = new Color(hintColour.r, hintColour.g, hintColour.b, 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3f)));
                yield return null;
            }

            if (hintText != null) hintText.color = hintColour;
            yield return null;
        }

        private void ApplyStyleColours(BeatStyle style)
        {
            (background.color, body.color) = style switch
            {
                BeatStyle.Note => (noteBackground, noteText),
                BeatStyle.Phone => (phoneBackground, phoneText),
                _ => (defaultBackground, defaultText),
            };
        }

        private static bool PressedThisFrame() => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
    }
}
