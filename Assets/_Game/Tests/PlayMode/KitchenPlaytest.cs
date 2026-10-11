using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Courtyard.Core;
using Courtyard.Kitchen;
using Courtyard.Minigames;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Courtyard.Tests
{
    /// <summary>
    /// Plays the real Kitchen scene with a virtual mouse (InputTestFixture), so minigames are tested end to end
    /// through the EventSystem exactly as a player would click, drag and hold.
    /// </summary>
    public abstract class KitchenPlaytest : InputTestFixture
    {
        protected const float TimeScale = 4f;
        protected Mouse Mouse;
        protected Camera Camera;
        protected CounterStation Counter;
        protected LevelRunner Runner;
        protected float LastQuality = -1f;

        private float _previousTimeScale;

        public override void Setup()
        {
            base.Setup();
            // URP's Rendering Debugger reads input through its own actions; with the virtual test mouse that logs
            // "Cached unprocessed value unexpectedly became outdated" errors. It's an editor tool: off for playtests.
            UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI = false;
            Mouse = InputSystem.AddDevice<Mouse>();
            _previousTimeScale = Time.timeScale;
            Time.timeScale = TimeScale;
        }

        public override void TearDown()
        {
            Time.timeScale = _previousTimeScale;
            if (GameManager.Instance != null) UnityEngine.Object.Destroy(GameManager.Instance.gameObject);
            base.TearDown();
        }

        /// <summary>Starts the Kitchen scene on the given day (a GameManager is created as the Boot scene would).</summary>
        protected IEnumerator LoadDay(int day)
        {
            var level = LoadLevel(day);
            Assert.That(level, Is.Not.Null, $"Day {day} level asset");

            var go = new GameObject("TestGameManager");
            var manager = go.AddComponent<GameManager>();
            SetField(manager, "levels", new[] { level });
            SetField(manager, "_currentIndex", 0);

            SceneManager.LoadScene(SceneNames.Kitchen);
            yield return null;
            yield return null;
            Camera = Camera.main;
            Counter = UnityEngine.Object.FindAnyObjectByType<CounterStation>();
            Runner = UnityEngine.Object.FindAnyObjectByType<LevelRunner>();
            Assert.That(Counter, Is.Not.Null);
            Assert.That(Runner, Is.Not.Null);
        }

        protected static LevelDefinition LoadLevel(int day)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>($"Assets/_Game/Data/Levels/Day_{day:00}.asset");
#else
            return null;
#endif
        }

        /// <summary>Clicks through story text until a step of type T is running; records its quality when it completes.</summary>
        protected IEnumerator AdvanceTo<T>(float timeoutSeconds = 40f) where T : MinigameStep
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var step = UnityEngine.Object.FindAnyObjectByType<T>();
                // Its turn has come (a step can start early, e.g. Day 1's spoons while the kettle heats).
                var runner = UnityEngine.Object.FindAnyObjectByType<LevelRunner>();
                var active = runner != null ? GetField<MinigameStep>(runner, "_activeStep") : null;
                if (step != null && !step.IsComplete && active == step)
                {
                    LastQuality = -1f;
                    step.Completed += q => LastQuality = q;
                    yield return Wait(0.2f);
                    yield break;
                }

                yield return ClickScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.25f));
                yield return Wait(0.15f);
            }

            Assert.Fail($"Timed out waiting for {typeof(T).Name}.");
        }

        protected IEnumerator WaitForCompletion(float timeoutSeconds = 20f)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (LastQuality < 0f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(LastQuality, Is.GreaterThanOrEqualTo(0f), "Step did not complete.");
        }

        // ---------------------------------------------------------------- input

        protected Vector2 ToScreen(Vector3 world) => Camera.WorldToScreenPoint(world);

        protected IEnumerator MoveTo(Vector2 screen)
        {
            Move(Mouse.position, screen);
            yield return null;
        }

        protected IEnumerator ClickScreen(Vector2 screen)
        {
            yield return MoveTo(screen);
            Press(Mouse.leftButton);
            yield return null;
            yield return null;
            Release(Mouse.leftButton);
            yield return null;
        }

        protected IEnumerator Click(Transform target) => ClickScreen(ToScreen(target.position));

        protected IEnumerator Press(Vector3 world)
        {
            yield return MoveTo(ToScreen(world));
            Press(Mouse.leftButton);
            yield return null;
        }

        protected IEnumerator ReleaseMouse()
        {
            Release(Mouse.leftButton);
            yield return null;
        }

        /// <summary>Drags along world points (pressing at the first), in small moves so the EventSystem sees a drag.</summary>
        protected IEnumerator Drag(params Vector3[] path)
        {
            yield return Press(path[0]);
            for (var i = 1; i < path.Length; i++)
            {
                var from = ToScreen(path[i - 1]);
                var to = ToScreen(path[i]);
                for (var t = 1; t <= 16; t++)
                {
                    Move(Mouse.position, Vector2.Lerp(from, to, t / 16f));
                    yield return null;
                }
            }

            yield return ReleaseMouse();
        }

        protected static IEnumerator Wait(float gameSeconds)
        {
            yield return new WaitForSeconds(gameSeconds);
        }

        // ---------------------------------------------------------------- reflection helpers

        protected static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.SetValue(target, value);

        protected static T GetField<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(target);

        protected static Transform FindDeep(string name) =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == name);
    }
}
