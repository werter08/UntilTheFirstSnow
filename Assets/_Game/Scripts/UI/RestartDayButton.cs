using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Courtyard.UI
{
    /// <summary>"Restart day": starts the current day over (no calendar tearing — it's the same day).</summary>
    [RequireComponent(typeof(Button))]
    public sealed class RestartDayButton : MonoBehaviour
    {
        private bool _restarting;

        private void Awake() => GetComponent<Button>().onClick.AddListener(Restart);

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
