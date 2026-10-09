using System.Collections;
using Courtyard.Core;
using Courtyard.Minigames;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Courtyard.Tests
{
    public sealed class FailPlaytests : StepPlayers
    {
        /// <summary>Idle on Day 20 (fastest drain): the gentle fail shows and the day restarts.</summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator Day20_IdlePlayerFreezes_AndDayRestarts()
        {
            yield return LoadDay(20);
            yield return AdvanceTo<KettleBoilStep>();
            var firstRunner = Runner;

            var deadline = Time.realtimeSinceStartup + 60f;
            while (!GetField<bool>(firstRunner, "_failed") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(GetField<bool>(firstRunner, "_failed"), Is.True, "Warmth should run out when idle on Day 20.");

            deadline = Time.realtimeSinceStartup + 20f;
            while (firstRunner != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Kitchen), "Day should restart in the Kitchen.");
            Assert.That(Object.FindAnyObjectByType<LevelRunner>(), Is.Not.Null);
        }
    }
}
