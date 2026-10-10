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

        public static GameManager Instance { get; private set; }

        public LevelDefinition CurrentLevel =>
            _currentIndex >= 0 && _currentIndex < levels.Length ? levels[_currentIndex] : null;

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
            SceneManager.LoadScene(SceneNames.Kitchen);
        }

        public void CompleteLevel()
        {
            _currentIndex++;
            if (CurrentLevel != null)
            {
                SceneManager.LoadScene(SceneNames.Kitchen);
                return;
            }

            _currentIndex = -1;
            SceneManager.LoadScene(SceneNames.Ending);
        }

        public void RestartLevel() => SceneManager.LoadScene(SceneNames.Kitchen);

        public void ReturnToTitle()
        {
            _currentIndex = -1;
            SceneManager.LoadScene(SceneNames.Boot);
        }
    }
}
