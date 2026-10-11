using UnityEngine;
using UnityEngine.SceneManagement;

namespace Courtyard.Core
{
    /// <summary>Owns level order and scene flow. Lives in the Boot scene and survives scene loads.</summary>
    public sealed class GameManager : MonoBehaviour
    {
        [Tooltip("Played in this order, then the Ending scene.")]
        [SerializeField] private LevelDefinition[] levels;

        private int _currentIndex = -1;
        private bool _restarting;

        /// <summary>
        /// The day the calendar shows before tearing to <paramref name="level"/>: the last day played, or the same day
        /// on the first day and after a restart.
        /// </summary>
        public int CalendarFromDay(LevelDefinition level)
        {
            if (_restarting || _currentIndex <= 0 || levels == null || _currentIndex > levels.Length - 1) return level.DayNumber;
            return levels[_currentIndex - 1].DayNumber;
        }

        public static GameManager Instance { get; private set; }

        public LevelDefinition CurrentLevel =>
            Epilogue ? EpilogueLevel : _currentIndex >= 0 && _currentIndex < levels.Length ? levels[_currentIndex] : null;

        /// <summary>All days in order.</summary>
        public System.Collections.Generic.IReadOnlyList<LevelDefinition> Levels => levels;

        /// <summary>True while the post-credits morning plays in the kitchen (the last day's kitchen, nothing to make).</summary>
        public bool Epilogue { get; private set; }

        private LevelDefinition EpilogueLevel => levels != null && levels.Length > 0 ? levels[levels.Length - 1] : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// The Kitchen scene was played directly in the Editor: set up the normal day order and continue from
        /// <paramref name="current"/> (so Day 1 → 5 → 10 → 20 → 30 → Ending still follows).
        /// </summary>
        public static GameManager CreateForDirectPlay(LevelDefinition[] allLevels, LevelDefinition current)
        {
            var manager = new GameObject("GameManager (direct play)").AddComponent<GameManager>();
            manager.levels = allLevels;
            manager._currentIndex = System.Array.IndexOf(allLevels, current);
            return manager;
        }

        public void StartGame()
        {
            if (levels == null || levels.Length == 0)
            {
                Debug.LogError("GameManager: no levels assigned.", this);
                return;
            }

            _currentIndex = 0;
            _restarting = false;
            Epilogue = false;
            SceneManager.LoadScene(SceneNames.Kitchen);
        }

        public void CompleteLevel()
        {
            _currentIndex++;
            _restarting = false;
            if (CurrentLevel != null)
            {
                SceneManager.LoadScene(SceneNames.Kitchen);
                return;
            }

            _currentIndex = -1;
            SceneManager.LoadScene(SceneNames.Ending);
        }

        public void RestartLevel()
        {
            _restarting = true; // same day again: no pages to tear
            SceneManager.LoadScene(SceneNames.Kitchen);
        }

        /// <summary>After the credits: the kitchen the morning after the last day.</summary>
        public void PlayEpilogue()
        {
            Epilogue = true;
            SceneManager.LoadScene(SceneNames.Kitchen);
        }

        public void ReturnToTitle()
        {
            Epilogue = false;
            _currentIndex = -1;
            SceneManager.LoadScene(SceneNames.Boot);
        }
    }
}
