using System.Linq;
using Courtyard.Core;
using Courtyard.Minigames;
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

        /// <summary>A careless player keeps remaking; after this many tries we stop (the day is still going).</summary>
        private const int MaxAttempts = 4;

        /// <summary>
        /// Returns the lowest warmth reached across all meters, or a negative value if the level fails. A bad step chills
        /// and the drink is remade from its first step (the latest kettle boil), as LevelRunner does.
        /// </summary>
        private static float Simulate(LevelDefinition level, float secondsPerStep, float quality)
        {
            var group = new WarmthGroup(level.TwoMeters ? 2 : 1, level.StartWarmth, level.EffectiveDrainPerSecond,
                level.WarmthLocked, level.MinWarmth);
            var lowest = float.MaxValue;
            var attempts = 0;
            var remaking = false;

            for (var i = 0; i < level.Steps.Count; i++)
            {
                var step = level.Steps[i];
                if (remaking && step is ChooseMugStep) continue; // her mug is already on the counter
                group.Tick(step.Scored ? secondsPerStep : UnscoredStepSeconds);
                lowest = group.Meters.Min(m => m.Value) is var v && v < lowest ? v : lowest;
                if (group.IsDepleted) return -1f;
                if (!step.Scored) continue;

                var q = step.FixedQuality ?? quality;
                group.Apply(level.TargetFor(step), level.StepWarmthDelta(q));
                lowest = group.Meters.Min(m => m.Value) is var after && after < lowest ? after : lowest;
                if (group.IsDepleted) return -1f;
                if (!step.CountsTowardDrink || !level.IsBad(q)) continue;

                if (++attempts >= MaxAttempts) return lowest; // still trying: not lost (yet)
                remaking = true;
                i = RemakeFrom(level, i) - 1;
            }

            group.Apply(level.DrinkTarget, level.DrinkWarmthDelta(quality));
            if (group.IsDepleted) return -1f;
            return lowest;
        }

        private static int RemakeFrom(LevelDefinition level, int badStep)
        {
            for (var j = badStep; j >= 0; j--)
                if (level.Steps[j] is KettleBoilStep) return j;
            return 0;
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
            // Careless drinks chill and must be remade; a careless player gets through every day except the coldest night.
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
