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
