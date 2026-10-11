using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Courtyard.UI
{
    /// <summary>"Restart day": asks first, then starts the current day over (no calendar tearing — it's the same day).</summary>
    [RequireComponent(typeof(Button))]
    public sealed class RestartDayButton : MonoBehaviour
    {
        [SerializeField] private ConfirmPopup confirm;
        [SerializeField] private string question = "Start this day over?";

        private bool _restarting;

        private void Awake() => GetComponent<Button>().onClick.AddListener(Ask);

        private void Ask()
        {
            if (_restarting) return;
            if (confirm == null) Restart();
            else confirm.Ask(question, Restart);
        }

        private void Restart()
        {
            if (_restarting) return;
            _restarting = true;
            AudioManager.Play(SfxId.UiClick);
            AudioManager.StopAllLoops();
            if (GameManager.Instance != null) GameManager.Instance.RestartLevel();
            else SceneManager.LoadScene(SceneNames.Kitchen);
        }
    }
}
