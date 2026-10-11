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
        [Tooltip("All days in order, so playing the Kitchen scene directly still goes on to the next day.")]
        [SerializeField] private LevelDefinition[] allLevels;

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
        [SerializeField, Min(0f)] private float failHoldSeconds = 2.5f;
        [Header("Idle nudge (no text): the right tool bounces if the player does nothing for a while")]
        [SerializeField, Min(1f)] private float nudgeAfterSeconds = 7f;
        [SerializeField, Min(0.5f)] private float nudgeRepeatSeconds = 4f;

        [Header("Hints")]
        [Tooltip("After the tutorial day, a minigame's hint shows only the first time it appears, for this long.")]
        [SerializeField, Min(0.5f)] private float firstTimeHintSeconds = 4f;
        [Tooltip("The recipe on the wall (click to read); pulses at the start of some days and on the first idle.")]
        [SerializeField] private RecipeCard recipeCard;
        [Tooltip("The shared \"stop in the green\" gauge the minigames use.")]
        [SerializeField] private MinigameGauge gauge;


        private WarmthGroup _warmth;
        private LevelDefinition _level;
        private Coroutine _run;
        private MinigameStep _activeStep;
        private bool _draining;
        private float _idle;
        private bool _failed;
        private bool _chilledByDrink;
        private bool _pulsedOnIdle;
        private Coroutine _hintTimer;

        private void Start()
        {
            if (GameManager.Instance == null && fallbackLevel != null && allLevels != null && System.Array.IndexOf(allLevels, fallbackLevel) >= 0)
                GameManager.CreateForDirectPlay(allLevels, fallbackLevel);

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
            story.BeatRead += OnBeatRead;
            HintSettings.Changed += OnHintSettingChanged;
            if (recipeCard != null) recipeCard.Set(_level.RecipeCardText, kitchen.NoteCard);
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
            if (story != null) story.BeatRead -= OnBeatRead;
            HintSettings.Changed -= OnHintSettingChanged;
        }

        private void CreateMeters()
        {
            _warmth = new WarmthGroup(_level.TwoMeters ? 2 : 1, _level.StartWarmth, _level.EffectiveDrainPerSecond,
                _level.WarmthLocked, _level.MinWarmth);
            _warmth.Depleted += OnMeterDepleted;
            for (var i = 0; i < meterViews.Length; i++)
                meterViews[i].Bind(i < _warmth.Meters.Count ? _warmth.Meters[i] : null);
            vignette.Bind(_warmth, _level.Palette.vignetteTint);
        }

        private IEnumerator Run()
        {
            hint.Hide();
            AudioManager.SetMusic(_level.Music);
            AudioManager.SetAmbience(_level.Ambience, _level.AmbienceVolume);
            yield return dayCard.Show(_level);
            yield return fader.FadeIn(fadeDuration);
            if (_level.PulseRecipeCard && recipeCard != null) recipeCard.Pulse();
            yield return story.Play(_level.GetBeats(BeatTrigger.LevelStart));

            var qualities = new SortedDictionary<int, float>(); // step index → result, for the drink being made
            var beatsShown = new HashSet<int>();
            MinigameStep early = null; // a step that started with the previous one (Day 1: spoons while the kettle heats)
            var remaking = false;
            for (var i = 0; i < _level.Steps.Count; i++)
            {
                if (remaking && _level.Steps[i].SkipOnRemake(counter)) continue; // e.g. her mug is already chosen

                var quality = -1f;
                if (early != null)
                {
                    _activeStep = early; // already on the counter: now it's its turn
                    early = null;
                    counter.SetAllInteractable(false);
                    _activeStep.Completed += q => quality = q;
                    _activeStep.OnPreviousDone();
                }
                else
                {
                    _activeStep = StartStep(i);
                    _activeStep.Completed += q => quality = q;
                    counter.SetAllInteractable(false);
                    _activeStep.Begin(new MinigameContext(_level, i, worldCamera, kitchen, counter, fx, hands, fader, gauge));
                    StartCoroutine(FadeStepParts(_activeStep.transform, 0f, 1f)); // its own tools fade in
                }

                ShowHint(_level.Steps[i]);
                if (i + 1 < _level.Steps.Count && _level.Steps[i + 1].StartsWithPrevious)
                {
                    early = StartStep(i + 1);
                    early.Begin(new MinigameContext(_level, i + 1, worldCamera, kitchen, counter, fx, hands, fader, gauge));
                    StartCoroutine(FadeStepParts(early.transform, 0f, 1f));
                }

                _draining = true;
                yield return new WaitUntil(() => quality >= 0f);
                _draining = false;

                // Let the result land (the last pour, the meter, the punch) before the step's tools fade away.
                HideHint();
                if (gauge != null) gauge.Hide();
                if (early == null) counter.SetAllInteractable(false);
                var step = _activeStep;
                _activeStep = null;
                yield return new WaitForSeconds(stepHoldSeconds);
                yield return FadeStepParts(step.transform, 1f, 0f);
                Destroy(step.gameObject);
                if (early == null) AudioManager.StopAllLoops();

                if (step.Scored)
                {
                    if (step.CountsTowardDrink) qualities[i] = quality;
                    var delta = _level.StepWarmthDelta(quality);
                    if (delta < 0f) yield return Chill(); // frost first, then the meter drops (and may run out)
                    _chilledByDrink = delta < 0f;
                    _warmth.Apply(_level.TargetFor(step), delta); // two-meter days: her drink warms her, his cup him
                    _chilledByDrink = false; // Depleted fires inside Apply; later drain-outs are plain cold
                    if (_failed) yield break;
                    fx.Punch(counter.Mug.transform);
                    if (delta >= 0f) fx.CelebrateQuality(quality, counter.Mug.transform.position + Vector3.up * 1.2f);
                    counter.SetSteam(quality);

                    // A bad drink isn't served: "It's bad, let's remake it", and it's made again from its first step
                    // (the cold it let in stays — remaking costs warmth, and the cold can still win).
                    if (step.CountsTowardDrink && _level.IsBad(quality))
                    {
                        var from = RemakeFrom(i);
                        if (early != null) Destroy(early.gameObject);
                        early = null;
                        yield return Remake(from);
                        foreach (var later in qualities.Keys.Where(k => k >= from).ToList()) qualities.Remove(later);
                        remaking = true;
                        i = from - 1; // the loop's ++ lands on it
                        continue;
                    }
                }

                yield return new WaitForSeconds(stepGapSeconds); // a breath before the next line or step
                if (beatsShown.Add(i)) yield return story.Play(_level.GetBeats(BeatTrigger.AfterStep, i, IsGood(quality)));
            }

            var drinkQualities = qualities.Values.ToList();
            // Serve the drink.
            var finalQuality = drinkQualities.Count > 0 ? drinkQualities.Average() : 1f;
            var drinkDelta = _level.DrinkWarmthDelta(finalQuality);
            if (drinkDelta < 0f) yield return Chill();
            _chilledByDrink = drinkDelta < 0f;
            _warmth.Apply(_level.DrinkTarget, drinkDelta);
            _chilledByDrink = false; // Depleted fires inside Apply; later drain-outs are plain cold
            if (_failed) yield break;
            var lastGood = drinkQualities.Count == 0 || IsGood(drinkQualities[drinkQualities.Count - 1]);
            var drinks = _level.DrinkResults.Where(d => d != null && d.icon != null && d.Shows(lastGood)).Select(d => d.icon).ToList();
            if (drinks.Count > 0) counter.ShowServedDrink(drinks[drinks.Count - 1]);
            yield return fx.ServeDrink(counter.Mug.transform, counter.MugTop, counter.MugSteam);
            if (_level.ServeAtDoor) yield return ServeAtDoor(lastGood);
            else yield return story.Play(_level.GetBeats(BeatTrigger.LevelEnd, 0, lastGood));
            yield return NewNote();
            if (_level.Finale) yield return Finale();

            yield return fader.FadeOut(endColor, fadeDuration);
            yield return Outro();
            if (GameManager.Instance != null)
                GameManager.Instance.CompleteLevel();
            else
                SceneManager.LoadScene(SceneNames.Ending);
        }

        private MinigameStep StartStep(int index)
        {
            var step = Instantiate(_level.Steps[index], stepRoot);
            step.transform.position += (Vector3)counter.ZoneShift(step.Zone);
            return step;
        }

        [Header("Remaking a bad drink")]
        [SerializeField] private Color remakeColour = new(0.22f, 0.16f, 0.13f);
        [SerializeField, Min(0f)] private float remakeHoldSeconds = 1.6f;

        /// <summary>The drink starts again from its first step: the latest kettle boil at or before <paramref name="badStep"/>.</summary>
        private int RemakeFrom(int badStep)
        {
            for (var j = badStep; j >= 0; j--)
                if (_level.Steps[j] is KettleBoilStep) return j;
            for (var j = 0; j <= badStep; j++)
                if (_level.Steps[j].Scored) return j;
            return badStep;
        }

        /// <summary>"It's bad. Let's remake it." over a warm dark fade; the mug is emptied and the tools go back.</summary>
        private IEnumerator Remake(int fromStep)
        {
            HideHint();
            if (gauge != null) gauge.Hide();
            AudioManager.StopAllLoops();
            yield return fader.FadeOut(remakeColour, fadeDuration * 0.7f, _level.RemakeText);
            counter.ResetDrink(_level, fromStep);
            yield return new WaitForSeconds(remakeHoldSeconds);
            yield return fader.FadeIn(fadeDuration * 0.7f);
        }

        [Header("Day 10: her drink at the door")]
        [SerializeField, Min(0f)] private float doorFadeSeconds = 0.7f;

        /// <summary>Day 10: the finished cup goes to her at the front door — her lines there — then back to the kitchen.</summary>
        private IEnumerator ServeAtDoor(bool good)
        {
            yield return fader.FadeOut(Color.black, doorFadeSeconds);
            AudioManager.ResumeTrack(SfxId.Footsteps);
            kitchen.ShowDoor(worldCamera, true);
            yield return new WaitForSeconds(0.6f);
            AudioManager.StopLoop(SfxId.Footsteps, 0.3f);
            yield return fader.FadeIn(doorFadeSeconds);
            var cup = kitchen.HerCupAtDoor;
            if (cup != null) fx.Puff(cup.transform.position + Vector3.up * 0.5f, new Color(1f, 1f, 1f, 0.5f), 10, 0.5f, 0.4f, -0.2f); // her drink steams
            yield return story.Play(_level.GetBeats(BeatTrigger.LevelEnd, 0, good));
            yield return fader.FadeOut(Color.black, doorFadeSeconds);
            kitchen.ShowDoor(worldCamera, false);
            yield return fader.FadeIn(doorFadeSeconds);
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
            else AudioManager.ResumeTrack(SfxId.Footsteps);
            // Day 20: exactly the two mugs on the counter — his, and the one chosen for her.
            var walkers = counter.HasHisCup && counter.HisCupSprite != null
                ? new[] { counter.HisCupSprite, counter.MugBody.sprite }
                : _level.OutroWalkers;
            if (walkers != null && walkers.Length > 0)
                yield return fader.Walk(walkers, _level.OutroHoldSeconds);
            else
                yield return new WaitForSeconds(_level.OutroHoldSeconds);
            AudioManager.StopLoop(SfxId.Footsteps, 0.4f); // the walk ends with the scene, not after it
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
            if (_idle < nudgeAfterSeconds) return;
            // The first time the player is stuck today, the recipe on the wall pulses too.
            if (!_pulsedOnIdle && recipeCard != null)
            {
                _pulsedOnIdle = true;
                recipeCard.Pulse();
            }

            if (_activeStep.NudgeTarget == null) return;
            fx.Punch(_activeStep.NudgeTarget, 0.8f);
            _idle = nudgeAfterSeconds - nudgeRepeatSeconds;
        }

        /// <summary>A badly made drink: frost on the mug, a shiver, and the meters drop.</summary>
        private IEnumerator Chill()
        {
            AudioManager.ColdSwell(1.2f, 0.5f); // a gust of cold
            yield return fx.Chill(counter.Mug.transform.position, worldCamera.transform);
        }

        [Header("Between steps")]
        [Tooltip("Pause after a step finishes, before its tools fade away.")]
        [SerializeField, Min(0f)] private float stepHoldSeconds = 0.5f;
        [Tooltip("Fade of a step's own tools (spoon, gauges…) in and out.")]
        [SerializeField, Min(0f)] private float stepFadeSeconds = 0.35f;
        [Tooltip("Pause after the result feedback, before the next story line or step.")]
        [SerializeField, Min(0f)] private float stepGapSeconds = 0.6f;

        /// <summary>
        /// Fades a step's own sprites and texts (alpha scaled from <paramref name="from"/> to <paramref name="to"/>),
        /// keeping whatever colour the step gives them. Counter tools aren't children of the step, so they stay put.
        /// </summary>
        private IEnumerator FadeStepParts(Transform stepRoot, float from, float to)
        {
            if (stepRoot == null || stepFadeSeconds <= 0f) yield break;
            var sprites = stepRoot.GetComponentsInChildren<SpriteRenderer>(true);
            var texts = stepRoot.GetComponentsInChildren<TMPro.TMP_Text>(true);
            var spriteAlpha = sprites.Select(r => r.color.a).ToArray();
            var textAlpha = texts.Select(t => t.color.a).ToArray();
            void Set(float k)
            {
                for (var i = 0; i < sprites.Length; i++)
                    if (sprites[i] != null) { var c = sprites[i].color; c.a = spriteAlpha[i] * k; sprites[i].color = c; }
                for (var i = 0; i < texts.Length; i++)
                    if (texts[i] != null) { var c = texts[i].color; c.a = textAlpha[i] * k; texts[i].color = c; }
            }

            Set(from);
            yield return Tweens.Run(stepFadeSeconds, t => Set(Mathf.Lerp(from, to, t)));
            if (stepRoot != null) Set(to);
        }

        [Header("End-of-day note")]
        [SerializeField] private string newNoteHint = "A new note on the fridge. Click it to read.";
        [Tooltip("Seconds between the new note's little nudges until it is read.")]
        [SerializeField, Min(0.5f)] private float newNoteNudgeSeconds = 1.5f;

        /// <summary>The day's note pops onto the fridge; the day ends once the player has read it (hint and nudges until then).</summary>
        private IEnumerator NewNote()
        {
            var slot = kitchen.PinNoteAfter(_level.DayNumber);
            var card = kitchen.NoteCard;
            if (slot == null || card == null) yield break;

            var opened = false;
            void OnOpened() => opened = true;
            card.Opened += OnOpened;
            AwaitingNote = slot;
            hint.Show(newNoteHint);
            fx.Burst(slot.position, new Color(1f, 0.92f, 0.6f), 10, 1.2f, 0.07f, -0.2f);
            for (var t = 0f; !opened; t += Time.deltaTime)
            {
                if (t > 1.5f && Mathf.Repeat(t, newNoteNudgeSeconds) < Time.deltaTime) fx.Punch(slot, 0.6f); // like her window: "here"
                yield return null;
            }

            hint.Hide();
            while (card.IsOpen) yield return null;
            card.Opened -= OnOpened;
            AwaitingNote = null;
        }

        /// <summary>The new note waiting to be read (null when none).</summary>
        public Transform AwaitingNote { get; private set; }

        // ---------------------------------------------------------------- hints

        /// <summary>
        /// Story prompts always show. Other hints: the whole step on the tutorial day, else only the first time that
        /// minigame appears in the game, for a few seconds — and never when the player switched hints off.
        /// </summary>
        private void ShowHint(MinigameStep prefab)
        {
            HideHint();
            if (string.IsNullOrWhiteSpace(prefab.HintText)) return;
            if (prefab.AlwaysShowHint || (HintSettings.Enabled && _level.TutorialHints))
            {
                hint.Show(prefab.HintText);
                return;
            }

            if (!HintSettings.Enabled || !IsFirstAppearance(prefab)) return;
            hint.Show(prefab.HintText);
            _hintTimer = StartCoroutine(HideHintAfter(firstTimeHintSeconds));
        }

        private IEnumerator HideHintAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _hintTimer = null;
            hint.Hide();
        }

        private void HideHint()
        {
            if (_hintTimer != null) StopCoroutine(_hintTimer);
            _hintTimer = null;
            hint.Hide();
        }

        /// <summary>No earlier day (in day order) uses this minigame.</summary>
        private bool IsFirstAppearance(MinigameStep prefab)
        {
            if (allLevels == null) return true;
            foreach (var level in allLevels)
            {
                if (level == null || level.DayNumber >= _level.DayNumber) continue;
                foreach (var step in level.Steps)
                    if (step == prefab) return false;
            }

            return true;
        }

        private void OnHintSettingChanged()
        {
            if (HintSettings.Enabled || _activeStep == null || _activeStep.AlwaysShowHint) return;
            HideHint();
        }

        private void OnBeatRead(Story.StoryBeat beat)
        {
            if (beat.SetsMiraWindow) kitchen.SetMiraWindow(beat.MiraWindowAfter);
        }

        private bool IsGood(float quality) => quality >= _level.GoodResultThreshold;

        private void OnMeterDepleted()
        {
            if (_failed) return;
            _failed = true;
            StartCoroutine(Fail(_chilledByDrink ? _level.ColdDrinkText : _level.FailText, failColor));
        }

        private IEnumerator Fail(string message, Color colour)
        {
            _draining = false;
            if (_run != null) StopCoroutine(_run);
            if (_activeStep != null) Destroy(_activeStep.gameObject);
            counter.SetAllInteractable(false);
            hint.Hide();
            AudioManager.StopAllLoops();
            AudioManager.ColdSwell(); // the wind swells and the music closes in

            yield return fader.FadeOut(colour, fadeDuration, message);
            yield return new WaitForSeconds(failHoldSeconds);

            if (GameManager.Instance != null)
                GameManager.Instance.RestartLevel();
            else
                SceneManager.LoadScene(SceneNames.Kitchen);
        }
    }
}
