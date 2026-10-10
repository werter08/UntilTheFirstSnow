using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Courtyard.Audio
{
    /// <summary>
    /// Music, ambience and sound effects. Lives in the Boot scene and survives scene loads (browsers only allow
    /// audio after the first click, which the Boot title screen provides). All static calls are safe when there is
    /// no manager (e.g. playing the Kitchen scene directly).
    /// <para>
    /// Routing: sources go to the mixer groups (Music, Ambience, SFX, UI). The cold muffles the music through the Music
    /// group's low-pass ("MusicCutoff"). WebGL has no mixer effects or filters, so there the music is ducked instead.
    /// </para>
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public const string MusicCutoffParameter = "MusicCutoff";

        [SerializeField] private SfxLibrary library;

        [Header("Mixer (optional: without it sources play straight to the listener)")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup uiGroup;

        [Header("Music")]
        [Tooltip("About -12 dB.")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.25f;
        [SerializeField, Min(0f)] private float musicCrossfadeSeconds = 2f;

        [Header("Ambience")]
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.6f;
        [Tooltip("Room tone that always plays under everything (very quiet).")]
        [SerializeField] private AudioClip roomTone;
        [SerializeField, Range(0f, 1f)] private float roomToneVolume = 0.12f;
        [SerializeField, Min(0f)] private float ambienceCrossfadeSeconds = 2f;

        [Header("Cold muffles the music")]
        [SerializeField] private float warmCutoff = 22000f;
        [SerializeField] private float coldCutoff = 1500f;
        [Tooltip("Warmth (0..1) below which the low-pass starts closing.")]
        [SerializeField, Range(0f, 1f)] private float coldThreshold = 0.3f;
        [SerializeField, Min(0.01f)] private float cutoffSmoothing = 3f;
        [Tooltip("WebGL fallback (no filters there): music volume at full cold.")]
        [SerializeField, Range(0f, 1f)] private float coldMusicDuck = 0.45f;

        [Header("Cold swell (fail / bad drink): built in code from the wind ambience")]
        [SerializeField] private AudioClip coldWind;
        [SerializeField] private float swellCutoff = 800f;

        [Header("One-shots")]
        [SerializeField, Min(1)] private int voices = 8;

        private readonly AudioSource[] _music = new AudioSource[2];
        private readonly AudioSource[] _ambience = new AudioSource[2];
        private int _musicIndex;
        private int _ambienceIndex;
        private AudioSource _room;
        private AudioSource _swell;
        private AudioSource[] _voices;
        private int _nextVoice;
        private AudioLowPassFilter[] _fallbackLowPass;
        private readonly Dictionary<SfxId, AudioSource> _loops = new();
        private readonly Dictionary<SfxId, float> _loopBaseVolume = new();
        private readonly Dictionary<AudioSource, Coroutine> _fades = new();
        private readonly HashSet<AudioSource> _fadingOut = new();
        private readonly HashSet<SfxId> _warned = new();
        private float _targetCutoff;
        private float _cutoff;
        private float _swellCutoffWeight;
        private Coroutine _musicFade;
        private Coroutine _ambienceFade;

        public static AudioManager Instance { get; private set; }

        /// <summary>True while level music is playing (the kitchen clock only ticks in the silence).</summary>
        public static bool HasMusic => Instance != null && Instance._music[Instance._musicIndex].clip != null;

        private static bool FiltersSupported => Application.platform != RuntimePlatform.WebGLPlayer;

        // ---------------------------------------------------------------- one-shots

        /// <summary>Frame of the last sound effect asked for (one-shot or loop) — lets fallbacks stay quiet when an action has its own sound.</summary>
        public static int LastSfxFrame { get; private set; } = -1;

        public static void Play(SfxId id)
        {
            LastSfxFrame = Time.frameCount;
            if (Instance != null) Instance.PlayInternal(id);
        }

        // ---------------------------------------------------------------- loops (kettle, grinder, frother…)

        public static void StartLoop(SfxId id, float fadeInSeconds = 0f)
        {
            LastSfxFrame = Time.frameCount;
            if (Instance != null) Instance.StartLoopInternal(id, fadeInSeconds);
        }

        public static void StopLoop(SfxId id, float fadeOutSeconds = 0f)
        {
            if (Instance != null) Instance.StopLoopInternal(id, fadeOutSeconds);
        }

        public static void SetLoopPitch(SfxId id, float pitch)
        {
            if (Instance != null && Instance._loops.TryGetValue(id, out var source)) source.pitch = pitch;
        }

        /// <summary>Loop volume as a share (0..1) of the library volume.</summary>
        public static void SetLoopVolume(SfxId id, float scale)
        {
            if (Instance == null || !Instance._loops.TryGetValue(id, out var source) || Instance._fades.ContainsKey(source)) return;
            source.volume = Instance._loopBaseVolume[id] * Mathf.Clamp01(scale);
        }

        public static bool IsLooping(SfxId id) =>
            Instance != null && Instance._loops.TryGetValue(id, out var source) && source.isPlaying;

        // ---------------------------------------------------------------- tracks (one-shots you can pause: the pour)

        /// <summary>Starts the clip, or continues it from where it was paused.</summary>
        public static void ResumeTrack(SfxId id)
        {
            if (Instance != null) Instance.ResumeTrackInternal(id);
        }

        public static void PauseTrack(SfxId id)
        {
            if (Instance != null && Instance._loops.TryGetValue(id, out var source)) source.Pause();
        }

        /// <summary>Stops and rewinds, so the next <see cref="ResumeTrack"/> starts from the beginning.</summary>
        public static void StopTrack(SfxId id)
        {
            if (Instance != null && Instance._loops.TryGetValue(id, out var source)) source.Stop();
        }

        public static void SetTrackVolume(SfxId id, float scale) => SetLoopVolume(id, scale);

        // ---------------------------------------------------------------- music & ambience

        /// <summary>Crossfades to the level's music (null = silence). The same clip keeps playing uninterrupted.</summary>
        public static void SetMusic(AudioClip clip)
        {
            if (Instance == null || Instance._music[Instance._musicIndex].clip == clip) return;
            Instance.Crossfade(Instance._music, ref Instance._musicIndex, ref Instance._musicFade, clip,
                Instance.musicVolume, Instance.musicCrossfadeSeconds);
        }

        /// <summary>Crossfades the day's ambience (null = only the room tone). <paramref name="volume"/> is 0..1.</summary>
        public static void SetAmbience(AudioClip clip, float volume = 1f)
        {
            if (Instance == null) return;
            var current = Instance._ambience[Instance._ambienceIndex];
            var target = Instance.ambienceVolume * volume;
            if (current.clip == clip)
            {
                current.volume = target;
                return;
            }

            Instance.Crossfade(Instance._ambience, ref Instance._ambienceIndex, ref Instance._ambienceFade, clip,
                target, Instance.ambienceCrossfadeSeconds);
        }

        /// <summary>0 = freezing, 1 = warm. Below the cold threshold the music gets muffled.</summary>
        public static void SetWarmth(float normalized)
        {
            if (Instance == null) return;
            var cold = Mathf.InverseLerp(Instance.coldThreshold, 0f, normalized);
            Instance._targetCutoff = Mathf.Lerp(Instance.warmCutoff, Instance.coldCutoff, cold);
        }

        /// <summary>
        /// The cold creeps in: the wind swells (0 → <paramref name="peak"/> → 0 over <paramref name="seconds"/>) and the
        /// music closes to ~800 Hz at the same time.
        /// </summary>
        public static void ColdSwell(float seconds = 2f, float peak = 0.8f)
        {
            if (Instance != null) Instance.StartCoroutine(Instance.SwellRoutine(seconds, peak));
        }

        public static void StopAllLoops()
        {
            if (Instance == null) return;
            foreach (var source in Instance._loops.Values)
                if (!Instance._fades.ContainsKey(source)) source.Stop(); // let fade-outs finish
        }

        // ---------------------------------------------------------------- lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // The ears travel with the manager: exactly one AudioListener in every scene (cameras may have none).
            if (GetComponent<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            SilenceOtherListeners();

            for (var i = 0; i < 2; i++)
            {
                _music[i] = CreateSource("Music" + i, true, 0f, musicGroup);
                _ambience[i] = CreateSource("Ambience" + i, true, 0f, ambienceGroup);
            }

            _room = CreateSource("RoomTone", true, roomToneVolume, ambienceGroup);
            if (roomTone != null)
            {
                _room.clip = roomTone;
                _room.Play();
            }

            _swell = CreateSource("ColdSwell", true, 0f, sfxGroup);
            _voices = new AudioSource[voices];
            for (var i = 0; i < voices; i++) _voices[i] = CreateSource("Voice" + i, false, 1f, sfxGroup);

            // Without a mixer (or its low-pass) the music sources carry their own filter — except on WebGL.
            if (mixer == null && FiltersSupported)
            {
                _fallbackLowPass = new AudioLowPassFilter[2];
                for (var i = 0; i < 2; i++) _fallbackLowPass[i] = _music[i].gameObject.AddComponent<AudioLowPassFilter>();
            }

            _cutoff = _targetCutoff = warmCutoff;
            ApplyCutoff();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) =>
            SilenceOtherListeners();

        private void SilenceOtherListeners()
        {
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (listener.gameObject != gameObject) listener.enabled = false;
        }

        private void Update()
        {
            _cutoff = Mathf.Lerp(_cutoff, _targetCutoff, 1f - Mathf.Exp(-cutoffSmoothing * Time.unscaledDeltaTime));
            ApplyCutoff();
        }

        private void ApplyCutoff()
        {
            var cutoff = Mathf.Lerp(_cutoff, Mathf.Min(_cutoff, swellCutoff), _swellCutoffWeight);
            if (FiltersSupported)
            {
                if (mixer != null) mixer.SetFloat(MusicCutoffParameter, cutoff);
                else if (_fallbackLowPass != null)
                    foreach (var filter in _fallbackLowPass) filter.cutoffFrequency = cutoff;
                return;
            }

            // WebGL: no filters — duck the music by how far the cutoff has closed.
            var muffle = Mathf.InverseLerp(warmCutoff, swellCutoff, cutoff);
            var duck = Mathf.Lerp(1f, coldMusicDuck, muffle);
            var playing = _music[_musicIndex];
            if (_musicFade == null && playing.clip != null) playing.volume = musicVolume * duck;
        }

        private AudioSource CreateSource(string sourceName, bool loop, float volume, AudioMixerGroup group)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.outputAudioMixerGroup = group;
            source.Stop(); // AddComponent enables it with playOnAwake on: it reports isPlaying (with no clip) until stopped
            return source;
        }

        private bool TryGetEntry(SfxId id, out SfxEntry entry)
        {
            if (library != null && library.TryGet(id, out entry)) return true;
            entry = null;
            if (_warned.Add(id)) Debug.LogWarning($"AudioManager: no clip for {id} (see Data/Audio/SfxLibrary).", this);
            return false;
        }

        private static bool IsUi(SfxId id) => id is SfxId.UiClick;

        // ---------------------------------------------------------------- internals

        private void PlayInternal(SfxId id)
        {
            if (!TryGetEntry(id, out var entry)) return;

            // A small pool, so each one-shot gets its own random pitch without bending the others.
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.outputAudioMixerGroup = IsUi(id) ? uiGroup : sfxGroup;
            voice.pitch = Random.Range(entry.pitch.x, entry.pitch.y);
            voice.PlayOneShot(entry.clips[Random.Range(0, entry.clips.Length)], entry.volume);
        }

        private AudioSource LoopSource(SfxId id, SfxEntry entry, bool loop)
        {
            if (!_loops.TryGetValue(id, out var source))
            {
                source = CreateSource("Loop_" + id, loop, entry.volume, IsUi(id) ? uiGroup : sfxGroup);
                _loops[id] = source;
            }

            source.loop = loop;
            _loopBaseVolume[id] = entry.volume;
            return source;
        }

        private void StartLoopInternal(SfxId id, float fadeIn)
        {
            if (!TryGetEntry(id, out var entry)) return;

            var source = LoopSource(id, entry, true);
            var fading = _fades.ContainsKey(source);
            if (source.isPlaying && (!fading || !_fadingOut.Contains(source))) return; // playing or already fading in
            if (!source.isPlaying)
            {
                source.clip = entry.clips[Random.Range(0, entry.clips.Length)];
                source.pitch = 1f;
                source.volume = fadeIn > 0f ? 0f : entry.volume;
                source.Play();
            }

            Fade(source, entry.volume, fadeIn, stopAtEnd: false);
        }

        private void StopLoopInternal(SfxId id, float fadeOut)
        {
            if (!_loops.TryGetValue(id, out var source) || !source.isPlaying) return;
            if (fadeOut > 0f && _fadingOut.Contains(source)) return; // already on its way out
            if (fadeOut <= 0f)
            {
                CancelFade(source);
                source.Stop();
                return;
            }

            Fade(source, 0f, fadeOut, stopAtEnd: true);
        }

        private void ResumeTrackInternal(SfxId id)
        {
            if (!TryGetEntry(id, out var entry)) return;

            var source = LoopSource(id, entry, false);
            if (source.isPlaying) return;
            if (source.clip != null && source.time > 0f && source.time < source.clip.length)
            {
                source.UnPause();
                return;
            }

            source.clip = entry.clips[Random.Range(0, entry.clips.Length)];
            source.volume = entry.volume;
            source.Play();
        }

        private void Fade(AudioSource source, float to, float seconds, bool stopAtEnd)
        {
            CancelFade(source);
            if (seconds <= 0f)
            {
                source.volume = to;
                if (stopAtEnd) source.Stop();
                return;
            }

            if (stopAtEnd) _fadingOut.Add(source);
            _fades[source] = StartCoroutine(FadeRoutine(source, to, seconds, stopAtEnd));
        }

        private void CancelFade(AudioSource source)
        {
            if (!_fades.TryGetValue(source, out var routine)) return;
            if (routine != null) StopCoroutine(routine);
            _fades.Remove(source);
            _fadingOut.Remove(source);
        }

        private IEnumerator FadeRoutine(AudioSource source, float to, float seconds, bool stopAtEnd)
        {
            var from = source.volume;
            for (var t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }

            source.volume = to;
            if (stopAtEnd) source.Stop();
            _fades.Remove(source);
            _fadingOut.Remove(source);
        }

        private void Crossfade(AudioSource[] pair, ref int index, ref Coroutine running, AudioClip clip, float volume, float seconds)
        {
            if (running != null) StopCoroutine(running);
            var from = pair[index];
            index = 1 - index;
            var to = pair[index];
            to.Stop();
            to.clip = clip;
            to.volume = 0f;
            if (clip != null) to.Play();
            var isMusic = pair == _music;
            running = StartCoroutine(CrossfadeRoutine(from, to, clip != null ? volume : 0f, seconds, isMusic));
        }

        private IEnumerator CrossfadeRoutine(AudioSource from, AudioSource to, float volume, float seconds, bool isMusic)
        {
            yield return null; // let the caller store this routine before it can finish
            var start = from.volume;
            for (var t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                var k = t / seconds;
                from.volume = Mathf.Lerp(start, 0f, k);
                to.volume = Mathf.Lerp(0f, volume, k);
                yield return null;
            }

            from.Stop();
            from.clip = null;
            to.volume = volume;
            if (isMusic) _musicFade = null;
            else _ambienceFade = null;
        }

        private IEnumerator SwellRoutine(float seconds, float peak)
        {
            if (coldWind != null)
            {
                _swell.clip = coldWind;
                _swell.time = Random.Range(0f, Mathf.Max(0f, coldWind.length - seconds));
                _swell.volume = 0f;
                _swell.Play();
            }

            for (var t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                var envelope = Mathf.Sin(t / seconds * Mathf.PI); // 0 → 1 → 0
                _swell.volume = peak * envelope;
                _swellCutoffWeight = Mathf.Max(_swellCutoffWeight, Mathf.Clamp01(t / (seconds * 0.5f)));
                yield return null;
            }

            _swell.Stop();
            _swellCutoffWeight = 0f;
        }
    }
}
