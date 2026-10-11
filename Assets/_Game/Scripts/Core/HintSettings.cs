using System;
using UnityEngine;

namespace Courtyard.Core
{
    /// <summary>The player's "Hints: on/off" choice, remembered between sessions (PlayerPrefs).</summary>
    public static class HintSettings
    {
        private const string Key = "courtyard.hints";

        /// <summary>Raised when the player switches hints on or off.</summary>
        public static event Action Changed;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Key, 1) == 1;
            set
            {
                if (value == Enabled) return;
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }
    }
}
