using System.Collections.Generic;
using UnityEngine;

namespace Courtyard.Audio
{
    /// <summary>
    /// Music, ambience and sound effects. Lives in the Boot scene and survives scene loads (browsers only allow
    /// audio after the first click, which the Boot title screen provides). All static calls are safe when there is
    /// no manager (e.g. playing the Kitchen scene directly).
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private SfxLibrary library;
        [SerializeField] private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.5f;

        [Header("Cold muffles the music")]
        [SerializeField] private float warmCutoff = 22000f;
        [SerializeField] private float coldCutoff = 1200f;
        [Tooltip("Warmth (0..1) below which the low-pass starts closing.")]
        [SerializeField, Range(0f, 1f)] private float coldThreshold = 0.3f;
        [SerializeField, Min(0.01f)] private float cutoffSmoothing = 3f;

        private AudioSource _music;
        private AudioSource _ambience;
        private AudioSource _oneShots;
        private AudioLowPassFilter _lowPass;
        private readonly Dictionary<SfxId, AudioSource> _loops = new();
        private float _targetCutoff;

        public static AudioManager Instance { get; private set; }

        public static void Play(SfxId id)
        {
            if (Instance != null) Instance.PlayInternal(id);
        }

        public static void StartLoop(SfxId id)
        {
            if (Instance != null) Instance.StartLoopInternal(id);
        }

        public static void StopLoop(SfxId id)
        {
            if (Instance != null) Instance.StopLoopInternal(id);
        }

        public static void SetAmbience(AudioClip clip)
        {
            if (Instance == null || Instance._ambience.clip == clip) return;
            Instance._ambience.clip = clip;
            if (clip != null) Instance._ambience.Play();
            else Instance._ambience.Stop();
        }

        /// <summary>0 = freezing, 1 = warm. Below the cold threshold the music gets muffled.</summary>
        public static void SetWarmth(float normalized)
        {
            if (Instance == null) return;
            var cold = Mathf.InverseLerp(Instance.coldThreshold, 0f, normalized);
            Instance._targetCutoff = Mathf.Lerp(Instance.warmCutoff, Instance.coldCutoff, cold);
        }

        public static void StopAllLoops()
        {
            if (Instance == null) return;
            foreach (var source in Instance._loops.Values) source.Stop();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _music = CreateSource("Music", true, musicVolume);
            _lowPass = _music.gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = warmCutoff;
            _targetCutoff = warmCutoff;
            _ambience = CreateSource("Ambience", true, ambienceVolume);
            _oneShots = CreateSource("Sfx", false, 1f);

            if (music != null)
            {
                _music.clip = music;
                _music.Play();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _lowPass.cutoffFrequency = Mathf.Lerp(_lowPass.cutoffFrequency, _targetCutoff, 1f - Mathf.Exp(-cutoffSmoothing * Time.deltaTime));
        }

        private AudioSource CreateSource(string sourceName, bool loop, float volume)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            return source;
        }

        private void PlayInternal(SfxId id)
        {
            if (library == null || !library.TryGet(id, out var entry)) return;

            _oneShots.pitch = Random.Range(entry.pitch.x, entry.pitch.y);
            _oneShots.PlayOneShot(entry.clips[Random.Range(0, entry.clips.Length)], entry.volume);
        }

        private void StartLoopInternal(SfxId id)
        {
            if (library == null || !library.TryGet(id, out var entry)) return;

            if (!_loops.TryGetValue(id, out var source))
            {
                source = CreateSource("Loop_" + id, true, entry.volume);
                _loops[id] = source;
            }

            if (source.isPlaying) return;
            source.clip = entry.clips[Random.Range(0, entry.clips.Length)];
            source.Play();
        }

        private void StopLoopInternal(SfxId id)
        {
            if (_loops.TryGetValue(id, out var source)) source.Stop();
        }
    }
}
