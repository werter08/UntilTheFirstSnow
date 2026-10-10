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
        /// <summary>
        /// Day 20, the only day you can lose: with warmth already low from careless drinks, one more bad drink (the
        /// kettle boils dry) lets the cold win — the gentle fail shows and the day restarts. Idling alone never does.
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator Day20_CarelessDrinks_TheColdWins_AndDayRestarts()
        {
            yield return LoadDay(20);
            yield return AdvanceTo<KettleBoilStep>();
            var firstRunner = Runner;
            var warmth = GetField<WarmthGroup>(firstRunner, "_warmth");
            var before = warmth.Meters[0].Value;
            yield return Wait(10f);
            Assert.That(warmth.Meters[0].Value, Is.EqualTo(before), "Time alone doesn't make it colder.");

            warmth.Chill(WarmthTarget.Both, warmth.Meters[0].Value - 5f); // as if the earlier drinks went badly
            yield return Click(Counter.KettleSwitch.transform);         // …and the kettle is forgotten
            var deadline = Time.realtimeSinceStartup + 60f;
            while (!GetField<bool>(firstRunner, "_failed") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(GetField<bool>(firstRunner, "_failed"), Is.True, "A bad drink on low warmth should let the cold win.");

            deadline = Time.realtimeSinceStartup + 20f;
            while (firstRunner != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Kitchen), "Day should restart in the Kitchen.");
            Assert.That(Object.FindAnyObjectByType<LevelRunner>(), Is.Not.Null);
        }
    }
}
