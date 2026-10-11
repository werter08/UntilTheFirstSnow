using System.Collections;
using System.Linq;
using System.Text;
using Courtyard.Core;
using Courtyard.Story;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Courtyard.UI
{
    /// <summary>
    /// The ending: the kitchen window at night in the first snow, two steaming mugs on the sill; the camera drifts slowly
    /// toward them while the title comes up, the credits roll (hold the mouse to roll faster), "Thanks for playing", and a
    /// click goes back to the title. Texts come from the ending sequence: the first Note is the title, the first Thought
    /// the tagline, the Notes after them the credits, the last Thought the thanks. Without the cinematic parts it plays
    /// the sequence in the story box as before.
    /// </summary>
    public sealed class EndingScreen : MonoBehaviour
    {
        [SerializeField] private StorySequence sequence;
        [SerializeField] private StoryPlayer story;
        [SerializeField] private ScreenFader fader;
        [SerializeField, Min(0f)] private float fadeDuration = 1.2f;

        [Header("Cinematic ending (optional)")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector3 cameraTo;
        [SerializeField, Min(0.5f)] private float cameraSizeTo = 2.6f;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text tagline;
        [SerializeField] private CanvasGroup dim;
        [SerializeField] private RectTransform creditsViewport;
        [SerializeField] private TMP_Text credits;
        [SerializeField] private CanvasGroup thanksGroup;
        [SerializeField] private TMP_Text thanks;
        [SerializeField] private CanvasGroup prompt;
        [SerializeField, Min(10f)] private float creditsSpeed = 70f;
        [SerializeField, Min(1f)] private float fastForward = 4f;

        private bool Cinematic => titleGroup != null && credits != null && thanksGroup != null && worldCamera != null;

        private IEnumerator Start()
        {
            if (!Cinematic)
            {
                yield return fader.FadeIn(fadeDuration);
                yield return story.Play(sequence.Beats);
                yield return fader.FadeOut(Color.black, fadeDuration);
                ToTitle();
                yield break;
            }

            Fill();
            foreach (var group in new[] { titleGroup, dim, thanksGroup, prompt }) group.alpha = 0f;
            credits.rectTransform.anchoredPosition = Vector2.zero; // its top edge just under the viewport

            StartCoroutine(Drift(60f));
            yield return fader.FadeIn(fadeDuration * 1.6f);
            yield return new WaitForSeconds(1.2f);

            // The title over the window.
            yield return Tweens.Run(1.6f, t => titleGroup.alpha = t);
            yield return new WaitForSeconds(3.2f);
            yield return Tweens.Run(1.2f, t => titleGroup.alpha = 1f - t);

            // Credits roll up over a soft dim (hold the mouse to roll faster).
            yield return Tweens.Run(0.8f, t => dim.alpha = t);
            Canvas.ForceUpdateCanvases();
            var rect = credits.rectTransform;
            // The text hangs from its top edge, anchored to the viewport's bottom: it has left the viewport once its top
            // is a viewport and a text height above.
            var end = creditsViewport.rect.height + credits.preferredHeight + 20f;
            while (rect.anchoredPosition.y < end)
            {
                var speed = creditsSpeed * (Pointer.current != null && Pointer.current.press.isPressed ? fastForward : 1f);
                rect.anchoredPosition += Vector2.up * (speed * Time.deltaTime);
                yield return null;
            }

            credits.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.6f);

            // Thanks, then back to the title on a click.
            yield return Tweens.Run(1.2f, t => thanksGroup.alpha = t);
            yield return new WaitForSeconds(1.5f);
            while (!(Pointer.current != null && Pointer.current.press.wasPressedThisFrame))
            {
                prompt.alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * 1.6f);
                yield return null;
            }

            Audio.AudioManager.Play(Audio.SfxId.UiClick);
            yield return fader.FadeOut(Color.black, fadeDuration);
            ToTitle();
        }

        /// <summary>Title, tagline, credits and thanks from the ending sequence.</summary>
        private void Fill()
        {
            var beats = sequence.Beats;
            var firstNote = beats.FirstOrDefault(b => b.Style == BeatStyle.Note);
            var firstThought = beats.FirstOrDefault(b => b.Style == BeatStyle.Thought);
            var lastThought = beats.LastOrDefault(b => b.Style == BeatStyle.Thought);
            if (firstNote != null) title.text = firstNote.Text;
            if (firstThought != null) tagline.text = firstThought.Text;
            if (lastThought != null && lastThought != firstThought) thanks.text = lastThought.Text;

            var text = new StringBuilder();
            foreach (var beat in beats)
            {
                if (beat == firstNote || beat == firstThought || beat == lastThought) continue;
                text.AppendLine(beat.Text.Replace("\n", "\n")).AppendLine();
            }

            credits.text = text.ToString().TrimEnd();
        }

        /// <summary>The camera eases toward the two mugs on the sill.</summary>
        private IEnumerator Drift(float seconds)
        {
            var t = worldCamera.transform;
            var from = t.position;
            var fromSize = worldCamera.orthographicSize;
            var to = new Vector3(cameraTo.x, cameraTo.y, from.z);
            yield return Tweens.Run(seconds, k =>
            {
                t.position = Vector3.Lerp(from, to, k);
                worldCamera.orthographicSize = Mathf.Lerp(fromSize, cameraSizeTo, k);
            });
        }

        private static void ToTitle()
        {
            if (GameManager.Instance != null) GameManager.Instance.ReturnToTitle();
            else SceneManager.LoadScene(SceneNames.Boot);
        }
    }
}
