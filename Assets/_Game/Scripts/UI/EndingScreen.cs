using System.Collections;
using Courtyard.Core;
using Courtyard.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Courtyard.UI
{
    /// <summary>Plays the final lines, then returns to the title.</summary>
    public sealed class EndingScreen : MonoBehaviour
    {
        [SerializeField] private StorySequence sequence;
        [SerializeField] private StoryPlayer story;
        [SerializeField] private ScreenFader fader;
        [SerializeField, Min(0f)] private float fadeDuration = 1.2f;

        private IEnumerator Start()
        {
            yield return fader.FadeIn(fadeDuration);
            yield return story.Play(sequence.Beats);
            yield return fader.FadeOut(Color.black, fadeDuration);

            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToTitle();
            else
                SceneManager.LoadScene(SceneNames.Boot);
        }
    }
}
