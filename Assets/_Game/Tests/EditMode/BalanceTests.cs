using System.Linq;
using Courtyard.Core;
using NUnit.Framework;
using UnityEditor;

namespace Courtyard.Tests
{
    /// <summary>
    /// Simulates a player through every level using the same warmth rules as LevelRunner
    /// (drain only while a step runs; scored steps warm or — when badly made — chill their target; the served drink
    /// warms or chills both).
    /// </summary>
    public sealed class BalanceTests
    {
        private const float UnscoredStepSeconds = 2f;

        private static LevelDefinition[] AllLevels() =>
            AssetDatabase.FindAssets("t:" + nameof(LevelDefinition), new[] { "Assets/_Game/Data/Levels" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelDefinition>)
                .ToArray();

        /// <summary>Returns the lowest warmth reached across all meters, or a negative value if the level fails.</summary>
        private static float Simulate(LevelDefinition level, float secondsPerStep, float quality)
        {
            var group = new WarmthGroup(level.TwoMeters ? 2 : 1, level.StartWarmth, level.EffectiveDrainPerSecond);
            var lowest = float.MaxValue;

            foreach (var step in level.Steps)
            {
                group.Tick(step.Scored ? secondsPerStep : UnscoredStepSeconds);
                lowest = group.Meters.Min(m => m.Value) is var v && v < lowest ? v : lowest;
                if (group.IsDepleted) return -1f;
                if (step.Scored) group.Apply(step.WarmthTarget, level.StepWarmthDelta(quality));
                if (group.IsDepleted) return -1f;
            }

            group.Apply(WarmthTarget.Both, level.DrinkWarmthDelta(quality));
            if (group.IsDepleted) return -1f;
            return lowest;
        }

        [Test]
        public void AveragePlayer_FinishesEveryDay()
        {
            foreach (var level in AllLevels())
                Assert.That(Simulate(level, 15f, 0.6f), Is.GreaterThan(0f), $"{level.name} fails for an average player");
        }

        [Test]
        public void GoodPlayer_StaysComfortablyWarm()
        {
            foreach (var level in AllLevels())
                Assert.That(Simulate(level, 10f, 0.85f), Is.GreaterThan(30f), $"{level.name} is too harsh for a good player");
        }

        [Test]
        public void TerribleBarista_FreezesOnEveryDayButTheLast()
        {
            // A badly made drink is cold: it takes warmth away, so careless play loses even at a normal pace.
            foreach (var level in AllLevels().Where(l => l.DayNumber != 30))
                Assert.That(Simulate(level, 15f, 0.1f), Is.LessThan(0f), $"{level.name}: bad drinks should let the cold win");
        }

        [Test]
        public void Day30_NeverFails_EvenWithBadDrinks()
        {
            var day30 = AllLevels().Single(l => l.DayNumber == 30);
            Assert.That(Simulate(day30, 30f, 0f), Is.GreaterThan(0f));
        }

        [Test]
        public void VerySlowPlayer_CanStillFail_OnTheColdestDay()
        {
            // Warmth must still matter (the limitation is rated): a very slow, sloppy player feels the cold on Day 20.
            var day20 = AllLevels().Single(l => l.DayNumber == 20);
            Assert.That(Simulate(day20, 30f, 0.3f), Is.LessThan(0f));
        }
    }
}
