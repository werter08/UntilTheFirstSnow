using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Courtyard.Kitchen;
using Courtyard.Minigames;
using Courtyard.Story;
using Courtyard.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Courtyard.Core
{
    /// <summary>
    /// Runs one day in the kitchen: story → minigame steps (warmth drains) → serve → ending beat → next level.
    /// Warmth only drains while a step is being played, never while story text is on screen.
    /// Day 20 uses two meters (him, Mira); the level fails gently when either empties.
    /// </summary>
    public sealed class LevelRunner : MonoBehaviour
    {
        [Tooltip("Used when the Kitchen scene is played directly, without the Boot scene.")]
        [SerializeField] private LevelDefinition fallbackLevel;

        [Header("Scene refs")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private KitchenDirector kitchen;
        [SerializeField] private Transform stepRoot;
        [SerializeField] private StoryPlayer story;
        [SerializeField] private HintView hint;
        [Tooltip("Index 0 = him, index 1 = Mira (two-meter days).")]
        [SerializeField] private WarmthMeterView[] meterViews;
        [SerializeField] private ScreenFader fader;

        [Header("Transitions")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.8f;
        [SerializeField] private Color endColor = Color.black;
        [SerializeField] private Color failColor = new(0.55f, 0.7f, 0.9f);
        [SerializeField, Min(0f)] private float failHoldSeconds = 2.5f;

        private WarmthGroup _warmth;
        private LevelDefinition _level;
        private Coroutine _run;
        private MinigameStep _activeStep;
        private bool _draining;
        private bool _failed;

        private void Start()
        {
            _level = GameManager.Instance != null && GameManager.Instance.CurrentLevel != null
                ? GameManager.Instance.CurrentLevel
                : fallbackLevel;

            if (_level == null)
            {
                Debug.LogError("LevelRunner: no level to play (GameManager has none and no fallback level is set).", this);
                return;
            }

            kitchen.Apply(_level);
            CreateMeters();
            _run = StartCoroutine(Run());
        }

        private void Update()
        {
            if (_draining) _warmth.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_warmth != null) _warmth.Depleted -= OnMeterDepleted;
        }

        private void CreateMeters()
        {
            _warmth = new WarmthGroup(_level.TwoMeters ? 2 : 1, _level.StartWarmth, _level.EffectiveDrainPerSecond);
            _warmth.Depleted += OnMeterDepleted;
            for (var i = 0; i < meterViews.Length; i++)
                meterViews[i].Bind(i < _warmth.Meters.Count ? _warmth.Meters[i] : null);
        }

        private IEnumerator Run()
        {
            hint.Hide();
            yield return fader.FadeIn(fadeDuration);
            yield return story.Play(_level.GetBeats(BeatTrigger.LevelStart));

            var qualities = new List<float>(_level.Steps.Count);
            for (var i = 0; i < _level.Steps.Count; i++)
            {
                var quality = -1f;
                _activeStep = Instantiate(_level.Steps[i], stepRoot);
                _activeStep.Completed += q => quality = q;
                hint.Show(_activeStep.HintText);
                _activeStep.Begin(new MinigameContext(_level, i, worldCamera, kitchen));

                _draining = true;
                yield return new WaitUntil(() => quality >= 0f);
                _draining = false;

                hint.Hide();
                var step = _activeStep;
                _activeStep = null;
                Destroy(step.gameObject);

                if (step.Scored)
                {
                    qualities.Add(quality);
                    _warmth.Restore(step.WarmthTarget, quality * _level.WarmthPerStep);
                }

                yield return story.Play(_level.GetBeats(BeatTrigger.AfterStep, i, IsGood(quality)));
            }

            // Serve the drink.
            var finalQuality = qualities.Count > 0 ? qualities.Average() : 1f;
            _warmth.Restore(WarmthTarget.Both, finalQuality * _level.DrinkWarmthBonus);
            var lastGood = qualities.Count == 0 || IsGood(qualities[qualities.Count - 1]);
            yield return story.Play(_level.GetBeats(BeatTrigger.LevelEnd, 0, lastGood));

            yield return fader.FadeOut(endColor, fadeDuration);
            if (GameManager.Instance != null)
                GameManager.Instance.CompleteLevel();
            else
                SceneManager.LoadScene(SceneNames.Ending);
        }

        private bool IsGood(float quality) => quality >= _level.GoodResultThreshold;

        private void OnMeterDepleted()
        {
            if (_failed) return;
            _failed = true;
            StartCoroutine(Fail());
        }

        private IEnumerator Fail()
        {
            _draining = false;
            if (_run != null) StopCoroutine(_run);
            if (_activeStep != null) Destroy(_activeStep.gameObject);
            hint.Hide();

            yield return fader.FadeOut(failColor, fadeDuration, _level.FailText);
            yield return new WaitForSeconds(failHoldSeconds);

            if (GameManager.Instance != null)
                GameManager.Instance.RestartLevel();
            else
                SceneManager.LoadScene(SceneNames.Kitchen);
        }
    }
}
