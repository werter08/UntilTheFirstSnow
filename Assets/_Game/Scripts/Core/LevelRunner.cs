using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Courtyard.Audio;
using Courtyard.Kitchen;
using Courtyard.Minigames;
using Courtyard.Story;
using Courtyard.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Courtyard.Core
{
    /// <summary>
    /// Runs one day in the kitchen: calendar card → story → minigame steps (warmth drains) → serve → ending beat → outro → next level.
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
        [SerializeField] private CounterStation counter;
        [SerializeField] private FeedbackFx fx;
        [SerializeField] private HandsView hands;
        [SerializeField] private Transform stepRoot;
        [SerializeField] private StoryPlayer story;
        [SerializeField] private HintView hint;
        [Tooltip("Index 0 = him, index 1 = Mira (two-meter days).")]
        [SerializeField] private WarmthMeterView[] meterViews;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private DayCard dayCard;
        [SerializeField] private WarmthVignette vignette;

        [Header("Transitions")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.8f;
        [SerializeField] private Color endColor = Color.black;
        [SerializeField] private Color failColor = new(0.55f, 0.7f, 0.9f);
        [SerializeField] private Color ruinedColor = new(0.35f, 0.27f, 0.22f);
        [SerializeField, Min(0f)] private float failHoldSeconds = 2.5f;
        [Header("Idle nudge (no text): the right tool bounces if the player does nothing for a while")]
        [SerializeField, Min(1f)] private float nudgeAfterSeconds = 7f;
        [SerializeField, Min(0.5f)] private float nudgeRepeatSeconds = 4f;

        [Tooltip("Served drink icon height above the mug centre (art manifest: 140 px).")]
        [SerializeField] private float drinkIconHeight = 1.4f;

        private WarmthGroup _warmth;
        private LevelDefinition _level;
        private Coroutine _run;
        private MinigameStep _activeStep;
        private bool _draining;
        private float _idle;
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
            counter.Prepare(_level);
            hands.SetSweater(_level.Props.sweater);
            CreateMeters();
            _run = StartCoroutine(Run());
        }

        private void Update()
        {
            if (_draining) _warmth.Tick(Time.deltaTime);
            UpdateNudge();
            if (_warmth != null) AudioManager.SetWarmth(vignette.Coldest);
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
            vignette.Bind(_warmth, _level.Palette.vignetteTint);
        }

        private IEnumerator Run()
        {
            hint.Hide();
            AudioManager.SetAmbience(_level.Ambience);
            yield return dayCard.Show(_level);
            yield return fader.FadeIn(fadeDuration);
            yield return story.Play(_level.GetBeats(BeatTrigger.LevelStart));

            var qualities = new List<float>(_level.Steps.Count);
            for (var i = 0; i < _level.Steps.Count; i++)
            {
                var quality = -1f;
                _activeStep = Instantiate(_level.Steps[i], stepRoot);
                _activeStep.transform.position += (Vector3)counter.ZoneShift(_activeStep.Zone);
                _activeStep.Completed += q => quality = q;
                _activeStep.Ruined += OnStepRuined;
                hint.Show(_activeStep.HintText);
                counter.SetAllInteractable(false);
                _activeStep.Begin(new MinigameContext(_level, i, worldCamera, kitchen, counter, fx, hands));

                _draining = true;
                yield return new WaitUntil(() => quality >= 0f);
                _draining = false;

                hint.Hide();
                counter.SetAllInteractable(false);
                var step = _activeStep;
                _activeStep = null;
                Destroy(step.gameObject);
                AudioManager.StopAllLoops();

                if (step.Scored)
                {
                    qualities.Add(quality);
                    _warmth.Restore(step.WarmthTarget, quality * _level.WarmthPerStep);
                    fx.Punch(counter.Mug.transform);
                    fx.CelebrateQuality(quality, counter.Mug.transform.position + Vector3.up * 1.2f);
                }

                yield return story.Play(_level.GetBeats(BeatTrigger.AfterStep, i, IsGood(quality)));
            }

            // Serve the drink.
            var finalQuality = qualities.Count > 0 ? qualities.Average() : 1f;
            _warmth.Restore(WarmthTarget.Both, finalQuality * _level.DrinkWarmthBonus);
            var lastGood = qualities.Count == 0 || IsGood(qualities[qualities.Count - 1]);
            var drinks = _level.DrinkResults.Where(d => d != null && d.icon != null && d.Shows(lastGood)).Select(d => d.icon).ToList();
            if (drinks.Count > 0) counter.ShowServedDrink(drinks[drinks.Count - 1]);
            yield return fx.ShowDrinks(drinks, counter.Mug.transform.position + Vector3.up * drinkIconHeight, counter.MugSteam);
            yield return story.Play(_level.GetBeats(BeatTrigger.LevelEnd, 0, lastGood));
            if (_level.Finale) yield return Finale();

            yield return fader.FadeOut(endColor, fadeDuration);
            yield return Outro();
            if (GameManager.Instance != null)
                GameManager.Instance.CompleteLevel();
            else
                SceneManager.LoadScene(SceneNames.Ending);
        }

        [Header("Finale")]
        [Tooltip("Where the camera drifts on the last day (the two mugs on the sill).")]
        [SerializeField] private Transform finaleFocus;
        [SerializeField, Min(0.5f)] private float finaleZoom = 2.6f;
        [SerializeField, Min(0.1f)] private float finaleSeconds = 4f;
        [SerializeField] private CanvasGroup hudGroup;

        /// <summary>Last day: the HUD fades and the camera drifts to the two mugs on the snowy window sill.</summary>
        private IEnumerator Finale()
        {
            if (finaleFocus == null) yield break;

            // The served mug joins his on the window sill.
            yield return fader.FadeOut(Color.black, fadeDuration * 0.6f);
            counter.Mug.gameObject.SetActive(false);
            kitchen.SetSillMugs(true);
            yield return fader.FadeIn(fadeDuration * 0.6f);

            var cam = worldCamera.transform;
            var fromPos = cam.position;
            var toPos = new Vector3(finaleFocus.position.x, finaleFocus.position.y + 0.6f, fromPos.z);
            var fromSize = worldCamera.orthographicSize;
            yield return Tweens.Run(finaleSeconds, t =>
            {
                cam.position = Vector3.Lerp(fromPos, toPos, t);
                worldCamera.orthographicSize = Mathf.Lerp(fromSize, finaleZoom, t);
                if (hudGroup != null) hudGroup.alpha = 1f - t;
            });
            yield return new WaitForSeconds(1.5f);
        }

        /// <summary>Optional beat over black, e.g. Day 20's footsteps across the courtyard.</summary>
        private IEnumerator Outro()
        {
            if (_level.OutroHoldSeconds <= 0f) yield break;

            if (_level.OutroClip != null) AudioSource.PlayClipAtPoint(_level.OutroClip, worldCamera.transform.position);
            else AudioManager.Play(SfxId.Footsteps);
            if (_level.OutroWalkers != null && _level.OutroWalkers.Length > 0)
                yield return fader.Walk(_level.OutroWalkers, _level.OutroHoldSeconds);
            else
                yield return new WaitForSeconds(_level.OutroHoldSeconds);
        }

        private void UpdateNudge()
        {
            var pressing = Pointer.current != null && Pointer.current.press.isPressed;
            if (_activeStep == null || pressing)
            {
                _idle = 0f;
                return;
            }

            _idle += Time.deltaTime;
            if (_idle < nudgeAfterSeconds || _activeStep.NudgeTarget == null) return;
            fx.Punch(_activeStep.NudgeTarget, 0.8f);
            _idle = nudgeAfterSeconds - nudgeRepeatSeconds;
        }

        private bool IsGood(float quality) => quality >= _level.GoodResultThreshold;

        private void OnMeterDepleted()
        {
            if (_failed) return;
            _failed = true;
            StartCoroutine(Fail(_level.FailText, failColor));
        }

        private void OnStepRuined()
        {
            if (_failed) return;
            _failed = true;
            StartCoroutine(Fail(_level.RuinedText, ruinedColor));
        }

        private IEnumerator Fail(string message, Color colour)
        {
            _draining = false;
            if (_run != null) StopCoroutine(_run);
            if (_activeStep != null) Destroy(_activeStep.gameObject);
            counter.SetAllInteractable(false);
            hint.Hide();
            AudioManager.StopAllLoops();
            AudioManager.Play(SfxId.Fail);

            yield return fader.FadeOut(colour, fadeDuration, message);
            yield return new WaitForSeconds(failHoldSeconds);

            if (GameManager.Instance != null)
                GameManager.Instance.RestartLevel();
            else
                SceneManager.LoadScene(SceneNames.Kitchen);
        }
    }
}
