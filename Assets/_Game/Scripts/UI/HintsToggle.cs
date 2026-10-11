using Courtyard.Audio;
using Courtyard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Courtyard.UI
{
    /// <summary>A small "Hints: on / off" switch in the corner; the choice is remembered.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class HintsToggle : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string onText = "Hints: on";
        [SerializeField] private string offText = "Hints: off";

        private void Awake() => GetComponent<Button>().onClick.AddListener(Toggle);

        private void OnEnable()
        {
            HintSettings.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => HintSettings.Changed -= Refresh;

        private void Toggle()
        {
            AudioManager.Play(SfxId.UiClick);
            HintSettings.Enabled = !HintSettings.Enabled;
        }

        private void Refresh()
        {
            if (label != null) label.text = HintSettings.Enabled ? onText : offText;
            // Round icon button: the bulb dims when hints are off; the tooltip says which way it is.
            if (TryGetComponent<RoundButton>(out var round))
            {
                round.Dimmed = !HintSettings.Enabled;
                round.Tooltip = HintSettings.Enabled ? onText : offText;
            }
        }
    }
}
