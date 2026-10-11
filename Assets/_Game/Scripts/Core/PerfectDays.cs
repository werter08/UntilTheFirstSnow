using System.Collections.Generic;
using UnityEngine;

namespace Courtyard.Core
{
    /// <summary>Days played perfectly (every step at "perfect"), remembered between sessions (PlayerPrefs).</summary>
    public static class PerfectDays
    {
        private const string KeyPrefix = "courtyard.perfect.day";

        public static bool Has(int day) => PlayerPrefs.GetInt(KeyPrefix + day, 0) == 1;

        public static void Mark(int day)
        {
            if (Has(day)) return;
            PlayerPrefs.SetInt(KeyPrefix + day, 1);
            PlayerPrefs.Save();
        }

        public static int Count(IEnumerable<int> days)
        {
            var count = 0;
            foreach (var day in days)
                if (Has(day)) count++;
            return count;
        }
    }
}
