using Courtyard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.UI
{
    /// <summary>"Click to start" title. The first click also unlocks browser audio on WebGL.</summary>
    public sealed class BootScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup prompt;
        [SerializeField, Min(0.1f)] private float pulseSpeed = 2f;

        private bool _started;

        private void Update()
        {
            if (_started) return;

            prompt.alpha = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) return;

            if (GameManager.Instance == null)
            {
                Debug.LogError("BootScreen: GameManager is missing from the Boot scene.", this);
                return;
            }

            _started = true;
            Audio.AudioManager.Play(Audio.SfxId.UiClick);
            GameManager.Instance.StartGame();
        }
    }
}
