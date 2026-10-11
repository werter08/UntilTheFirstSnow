using System.Linq;
using Courtyard.Core;
using NUnit.Framework;
using UnityEditor;

namespace Courtyard.Tests
{
    /// <summary>
    /// Simulates a player through every level using the same warmth rules as LevelRunner
    /// (drain only while a step runs; scored steps warm or — when badly made — chill their target; the served drink
    /// warms or chills whoever it is for — on Day 20 her drink warms her, his auto-filled cup warms him).
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
            var group = new WarmthGroup(level.TwoMeters ? 2 : 1, level.StartWarmth, level.EffectiveDrainPerSecond,
                level.WarmthLocked, level.MinWarmth);
            var lowest = float.MaxValue;

            foreach (var step in level.Steps)
            {
                group.Tick(step.Scored ? secondsPerStep : UnscoredStepSeconds);
                lowest = group.Meters.Min(m => m.Value) is var v && v < lowest ? v : lowest;
                if (group.IsDepleted) return -1f;
                if (step.Scored) group.Apply(level.TargetFor(step), level.StepWarmthDelta(step.FixedQuality ?? quality));
                if (group.IsDepleted) return -1f;
            }

            group.Apply(level.DrinkTarget, level.DrinkWarmthDelta(quality));
            if (group.IsDepleted) return -1f;
            return lowest;
        }

        [Test]
        public void AveragePlayer_NeverFails_AtAnyPace()
        {
            foreach (var level in AllLevels())
            {
                Assert.That(Simulate(level, 15f, 0.6f), Is.GreaterThan(0f), $"{level.name} fails for an average player");
                Assert.That(Simulate(level, 120f, 0.6f), Is.GreaterThan(0f), $"{level.name}: time alone must not freeze anyone");
            }
        }

        [Test]
        public void GoodPlayer_StaysComfortablyWarm()
        {
            foreach (var level in AllLevels())
                Assert.That(Simulate(level, 10f, 0.85f), Is.GreaterThan(30f), $"{level.name} is too harsh for a good player");
        }

        [Test]
        public void CarelessPlayer_CanOnlyLoseDay20()
        {
            // Spoiled or careless drinks chill; a careless player gets through every day except the coldest night.
            foreach (var level in AllLevels())
            {
                var result = Simulate(level, 15f, 0.1f);
                if (level.DayNumber == 20) Assert.That(result, Is.LessThan(0f), "Day 20: careless drinks should let the cold win");
                else Assert.That(result, Is.GreaterThan(0f), $"{level.name}: a careless player should still make it");
            }
        }

        [Test]
        public void Day1_CannotBeLost()
        {
            var day1 = AllLevels().Single(l => l.DayNumber == 1);
            Assert.That(Simulate(day1, 60f, 0f), Is.GreaterThanOrEqualTo(day1.MinWarmth));
        }

        [Test]
        public void Day30_NeverFails_EvenWithBadDrinks()
        {
            var day30 = AllLevels().Single(l => l.DayNumber == 30);
            Assert.That(Simulate(day30, 30f, 0f), Is.GreaterThan(0f));
        }
    }
}
