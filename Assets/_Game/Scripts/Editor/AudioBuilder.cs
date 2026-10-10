using System;
using System.Linq;
using System.Reflection;
using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using static Courtyard.Editor.BuilderKit;
using Object = UnityEngine.Object;

namespace Courtyard.Editor
{
    /// <summary>
    /// Wires the processed audio (Tools/process_audio.sh → Assets/_Game/Audio) into the game: import settings, the
    /// AudioMixer (Master › Music, Ambience, SFX, UI; exposed volumes + the Music low-pass), the SFX library, per-day
    /// music/ambience, and the AudioManager in Boot. Missing files leave their slot empty and log a warning. Idempotent.
    /// </summary>
    internal static class AudioBuilder
    {
        private const string AudioFolder = "Assets/_Game/Audio/";
        private const string MixerPath = AudioFolder + "GameMixer.mixer";
        private const string LibraryPath = "Assets/_Game/Data/Audio/SfxLibrary.asset";

        /// <summary>SFX slot → file (in Audio/SFX), volume, random pitch range. Loops keep pitch 1 (code moves it).</summary>
        private static readonly (SfxId id, string file, float volume, float pitchMin, float pitchMax)[] Sfx =
        {
            (SfxId.UiClick, "sfx_ui_click", 0.5f, 0.95f, 1.05f),
            (SfxId.KettleSwitch, "sfx_kettle_switch", 0.8f, 0.95f, 1.05f),
            (SfxId.KettleBoil, "sfx_kettle_boil_loop", 0.55f, 1f, 1f),
            (SfxId.KettleDone, "sfx_kettle_done", 0.9f, 0.95f, 1.05f),
            (SfxId.Pour, "sfx_pour", 0.8f, 1f, 1f),
            (SfxId.Spoon, "sfx_spoon_scoop", 1f, 0.95f, 1.05f),
            (SfxId.SpoonTink, "sfx_spoon_tink", 0.8f, 0.95f, 1.05f),
            (SfxId.Grind, "sfx_grinder_loop", 0.6f, 1f, 1f),
            (SfxId.Steam, "sfx_frother_loop", 0.5f, 1f, 1f),
            (SfxId.Squeal, "sfx_froth_squeal", 0.5f, 1f, 1f),
            (SfxId.MugClink, "sfx_mug_down", 0.8f, 0.95f, 1.05f),
            (SfxId.Topping, "sfx_spoon_scoop", 0.6f, 1.05f, 1.15f), // spice shake: a lighter, higher scoop
            (SfxId.Knock, "sfx_door_knock", 0.9f, 0.95f, 1.05f),
            (SfxId.PaperNote, "sfx_paper", 0.7f, 0.95f, 1.05f),
            (SfxId.Footsteps, "sfx_footsteps_leaves", 0.8f, 0.95f, 1.05f),
            (SfxId.Perfect, "sfx_perfect", 0.35f, 0.95f, 1.05f),
            (SfxId.ClockTick, "sfx_clock_tick", 0.25f, 0.95f, 1.05f),
            (SfxId.Type, "sfx_clock_tick", 0.12f, 1.35f, 1.75f), // soft, pitched-up ticks while story text types out
            // Stand-ins until real sounds arrive (see Docs/Texts.md → SFX status).
            (SfxId.Curtain, "sfx_paper", 0.45f, 0.6f, 0.75f), // curtain swish: a low, slow paper rustle
            (SfxId.LampSwitch, "sfx_kettle_switch", 0.6f, 1.1f, 1.25f),
            (SfxId.Break, "sfx_mug_down", 0.9f, 0.55f, 0.7f), // breaking: a heavy, low clunk
        };

        /// <summary>Day → music (Audio/Music), ambience (Audio/Ambience), ambience volume. Null = none.</summary>
        private static readonly (int day, string music, string ambience, float ambienceVolume)[] Days =
        {
            (1, "music_day05", "amb_rain_light", 1f), // the title-screen track carries on into Day 1 (and Day 5)
            (5, "music_day05", "amb_rain_heavy", 0.8f),
            (10, "music_day10", "amb_birds", 0.6f),
            (20, "music_day20", "amb_wind", 0.8f),
            (30, "music_day30", "amb_wind", 0.15f), // room tone + a very quiet wind
        };

        private const string RoomTone = "amb_room_fridge";
        private const string ColdWind = "amb_wind";
        private const string EndingMusic = "music_day30";
        private const string TitleMusic = "music_day05"; // starts on the title screen and carries on into Day 1

        [MenuItem("Courtyard/Setup/Apply Audio")]
        public static void Apply()
        {
            ConfigureImports();
            var mixer = EnsureMixer();
            FillLibrary();
            PatchLevels();
            WireScenes(mixer);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- import settings

        /// <summary>SFX: decompress on load, mono. Music &amp; ambience: streaming, stereo.</summary>
        private static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var sfx = path.Contains("/SFX/");
                var settings = importer.defaultSampleSettings;
                var loadType = sfx ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
                if (importer.forceToMono == sfx && settings.loadType == loadType && settings.compressionFormat == AudioCompressionFormat.Vorbis)
                    continue;

                importer.forceToMono = sfx;
                settings.loadType = loadType;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                settings.preloadAudioData = sfx;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static AudioClip Clip(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}{folder}/{name}.ogg");
            if (clip == null) Debug.LogWarning($"AudioBuilder: missing {AudioFolder}{folder}/{name}.ogg — slot left empty (run Tools/process_audio.sh).");
            return clip;
        }

        // ---------------------------------------------------------------- mixer

        /// <summary>
        /// Creates the mixer through Unity's internal mixer editor API (there is no public one): groups, exposed
        /// volumes (MasterVolume, MusicVolume, AmbienceVolume, SfxVolume, UiVolume) and a Lowpass on Music
        /// (exposed as <see cref="AudioManager.MusicCutoffParameter"/>, 22 kHz = open).
        /// </summary>
        private static AudioMixer EnsureMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null) return existing;

            const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var asm = typeof(UnityEditor.Editor).Assembly;
            var controllerType = asm.GetType("UnityEditor.Audio.AudioMixerController");
            var groupType = asm.GetType("UnityEditor.Audio.AudioMixerGroupController");
            var effectType = asm.GetType("UnityEditor.Audio.AudioMixerEffectController");
            var groupPathType = asm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
            var effectPathType = asm.GetType("UnityEditor.Audio.AudioEffectParameterPath");

            var controller = controllerType.GetMethod("CreateMixerControllerAtPath", Any)!.Invoke(null, new object[] { MixerPath });
            var master = controllerType.GetProperty("masterGroup", Any)!.GetValue(controller);
            var snapshot = controllerType.GetProperty("TargetSnapshot", Any)!.GetValue(controller);

            void Expose(object path, object guid, string exposedName)
            {
                controllerType.GetMethod("AddExposedParameter", Any)!.Invoke(controller, new[] { path });
                var property = controllerType.GetProperty("exposedParameters", Any)!;
                var parameters = (Array)property.GetValue(controller);
                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters.GetValue(i);
                    var field = parameter.GetType().GetField("guid", Any)!;
                    if (!field.GetValue(parameter).Equals(guid)) continue;
                    parameter.GetType().GetField("name", Any)!.SetValue(parameter, exposedName);
                    parameters.SetValue(parameter, i);
                }

                property.SetValue(controller, parameters);
            }

            void ExposeVolume(object group, string exposedName)
            {
                var guid = groupType.GetMethod("GetGUIDForVolume", Any)!.Invoke(group, null);
                Expose(Activator.CreateInstance(groupPathType, group, guid), guid, exposedName);
            }

            object NewGroup(string groupName)
            {
                var group = controllerType.GetMethod("CreateNewGroup", Any)!.Invoke(controller, new object[] { groupName, false });
                controllerType.GetMethod("AddChildToParent", Any)!.Invoke(controller, new[] { group, master });
                return group;
            }

            ExposeVolume(master, "MasterVolume");
            var music = NewGroup("Music");
            ExposeVolume(music, "MusicVolume");
            ExposeVolume(NewGroup("Ambience"), "AmbienceVolume");
            ExposeVolume(NewGroup("SFX"), "SfxVolume");
            ExposeVolume(NewGroup("UI"), "UiVolume");

            // Low-pass on Music; the cold closes it (AudioManager).
            var lowpass = Activator.CreateInstance(effectType, "Lowpass");
            effectType.GetMethod("PreallocateGUIDs", Any)!.Invoke(lowpass, null);
            ((Object)lowpass).hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset((Object)lowpass, (Object)controller);
            groupType.GetMethod("InsertEffect", Any, null, new[] { effectType, typeof(int) }, null)!.Invoke(music, new[] { lowpass, (object)0 });
            var cutoff = effectType.GetMethod("GetGUIDForParameter", Any)!.Invoke(lowpass, new object[] { "Cutoff freq" });
            effectType.GetMethod("SetValueForParameter", Any)!.Invoke(lowpass, new[] { controller, snapshot, "Cutoff freq", (object)22000f });
            Expose(Activator.CreateInstance(effectPathType, music, lowpass, cutoff), cutoff, AudioManager.MusicCutoffParameter);

            EditorUtility.SetDirty((Object)controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath);
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        private static AudioMixerGroup Group(AudioMixer mixer, string name) =>
            mixer == null ? null : mixer.FindMatchingGroups(name).FirstOrDefault(g => g.name == name);

        // ---------------------------------------------------------------- SFX library

        private static void FillLibrary()
        {
            ContentBuilder.SyncSfxLibrary(); // a slot for every SfxId
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
            Edit(library, so =>
            {
                var entries = so.FindProperty("entries");
                foreach (var (id, file, volume, pitchMin, pitchMax) in Sfx)
                {
                    for (var i = 0; i < entries.arraySize; i++)
                    {
                        var entry = entries.GetArrayElementAtIndex(i);
                        if (entry.FindPropertyRelative("id").enumValueIndex != (int)id) continue;
                        var clip = Clip("SFX", file);
                        var clips = entry.FindPropertyRelative("clips");
                        clips.arraySize = clip != null ? 1 : 0;
                        if (clip != null) clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                        entry.FindPropertyRelative("volume").floatValue = volume;
                        entry.FindPropertyRelative("pitch").vector2Value = new Vector2(pitchMin, pitchMax);
                    }
                }
            });
        }

        // ---------------------------------------------------------------- levels & scenes

        private static void PatchLevels()
        {
            foreach (var (day, music, ambience, volume) in Days)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"Assets/_Game/Data/Levels/Day_{day:00}.asset");
                if (level == null)
                {
                    Debug.LogWarning($"AudioBuilder: Day_{day:00}.asset not found.");
                    continue;
                }

                Edit(level, so =>
                {
                    so.FindProperty("music").objectReferenceValue = Clip("Music", music);
                    so.FindProperty("ambience").objectReferenceValue = Clip("Ambience", ambience);
                    so.FindProperty("ambienceVolume").floatValue = volume;
                });
            }
        }

        private static void EnsureAudioManager(UnityEngine.SceneManagement.Scene scene, SfxLibrary library, AudioMixer mixer)
        {
            var manager = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<AudioManager>(true)).FirstOrDefault(m => m != null);
            if (manager == null)
            {
                var go = new GameObject("AudioManager");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                manager = go.AddComponent<AudioManager>();
            }

            Wire(manager, "library", library);
            Wire(manager, "mixer", mixer);
            Wire(manager, "musicGroup", Group(mixer, "Music"));
            Wire(manager, "ambienceGroup", Group(mixer, "Ambience"));
            Wire(manager, "sfxGroup", Group(mixer, "SFX"));
            Wire(manager, "uiGroup", Group(mixer, "UI"));
            Wire(manager, "roomTone", Clip("Ambience", RoomTone));
            Wire(manager, "coldWind", Clip("Ambience", ColdWind));
            Wire(manager, "musicVolume", 0.25f); // ≈ -12 dB
        }

        private static void WireScenes(AudioMixer mixer)
        {
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
            // Every scene carries a wired AudioManager, so Kitchen or Ending played directly in the Editor has sound too.
            // The first one survives scene loads; later copies remove themselves (AudioManager.Awake).
            foreach (var path in BuildScript.Scenes)
                EditScene(path, scene => EnsureAudioManager(scene, library, mixer));

            // The title screen starts the first day's music (it continues into Day 1 without a restart).
            EditScene(BuildScript.Scenes[0], scene =>
            {
                var cue = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<MusicCue>(true)).FirstOrDefault(c => c != null);
                if (cue == null)
                {
                    var go = new GameObject("MusicCue");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                    cue = go.AddComponent<MusicCue>();
                }

                Wire(cue, "music", Clip("Music", TitleMusic));
            });

            // The Ending keeps Day 30's music playing.
            EditScene(BuildScript.Scenes[2], scene =>
            {
                var cue = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<MusicCue>(true)).FirstOrDefault(c => c != null);
                if (cue == null)
                {
                    var go = new GameObject("MusicCue");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                    cue = go.AddComponent<MusicCue>();
                }

                Wire(cue, "music", Clip("Music", EndingMusic));
            });

            // The kitchen clock ticks in the silence.
            EditScene(BuildScript.Scenes[1], scene =>
            {
                var clock = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Clock");
                if (clock == null)
                {
                    Debug.LogWarning("AudioBuilder: no 'Clock' in the Kitchen scene; the clock won't tick.");
                    return;
                }

                if (clock.GetComponent<ClockTicker>() == null) clock.gameObject.AddComponent<ClockTicker>();
            });
        }
    }
}
