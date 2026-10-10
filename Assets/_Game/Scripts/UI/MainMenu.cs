using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Courtyard.UI
{
    /// <summary>
    /// The title screen: an autumn evening through the kitchen window, the title and two sticky-note buttons.
    /// In the browser, sound can only start after a click, so there the buttons wait behind "click anywhere to begin".
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private CanvasGroup title;
        [SerializeField] private CanvasGroup buttons;
        [Tooltip("\"click anywhere to begin\" — only used where audio needs a first click (WebGL).")]
        [SerializeField] private CanvasGroup wakePrompt;
        [Tooltip("Black overlay that fades in when the game starts.")]
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;
        [SerializeField, Min(0f)] private float revealSeconds = 1.6f;
        [SerializeField, Min(0f)] private float leaveSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float pulseSpeed = 1.6f;

        private bool _awake;
        private bool _leaving;
        private int _wakeFrame = -1;

        private static bool NeedsWakeClick => Application.platform == RuntimePlatform.WebGLPlayer;

        private void Start()
        {
            startButton.onClick.AddListener(StartGame);
            // Quitting makes no sense in a browser tab.
            quitButton.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
            quitButton.onClick.AddListener(Quit);

            SetGroup(title, 0f, false);
            SetGroup(buttons, 0f, false);
            SetGroup(fade, 0f, false);
            SetGroup(wakePrompt, 0f, false);
            StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            yield return Tweens.Run(revealSeconds, k => title.alpha = k);
            if (NeedsWakeClick) yield return Tweens.Run(0.5f, k => wakePrompt.alpha = k);
            else Wake();
        }

        private void Update()
        {
            if (_awake || !NeedsWakeClick || wakePrompt.alpha <= 0f) return;
            wakePrompt.alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * pulseSpeed);
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame) Wake();
        }

        /// <summary>First click (browser): sound starts, the prompt fades and the buttons come in.</summary>
        private void Wake()
        {
            if (_awake) return;
            _awake = true;
            _wakeFrame = Time.frameCount;
            if (NeedsWakeClick) AudioManager.Play(SfxId.UiClick);
            StartCoroutine(ShowButtons());
        }

        private IEnumerator ShowButtons()
        {
            var from = wakePrompt.alpha;
            yield return Tweens.Run(0.6f, k =>
            {
                wakePrompt.alpha = from * (1f - k);
                buttons.alpha = k;
            });
            SetGroup(buttons, 1f, true);
        }

        private void StartGame()
        {
            if (_leaving || Time.frameCount == _wakeFrame) return;
            if (GameManager.Instance == null)
            {
                Debug.LogError("MainMenu: GameManager is missing from the Boot scene.", this);
                return;
            }

            _leaving = true;
            AudioManager.Play(SfxId.UiClick);
            StartCoroutine(Leave());
        }

        private IEnumerator Leave()
        {
            buttons.interactable = false;
            fade.blocksRaycasts = true;
            yield return Tweens.Run(leaveSeconds, k => fade.alpha = k);
            GameManager.Instance.StartGame(); // the music keeps playing into Day 1
        }

        private void Quit()
        {
            AudioManager.Play(SfxId.UiClick);
            Application.Quit();
        }

        private static void SetGroup(CanvasGroup group, float alpha, bool interactive)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.interactable = interactive;
            group.blocksRaycasts = interactive;
        }
    }
}
