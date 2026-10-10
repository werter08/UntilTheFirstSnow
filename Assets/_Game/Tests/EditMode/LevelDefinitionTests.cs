using System.Collections.Generic;
using System.Linq;
using Courtyard.Core;
using NUnit.Framework;
using UnityEditor;

namespace Courtyard.Tests
{
    public sealed class LevelDefinitionTests
    {
        private const string LevelsFolder = "Assets/_Game/Data/Levels";

        private static IEnumerable<LevelDefinition> AllLevels() =>
            AssetDatabase.FindAssets("t:" + nameof(LevelDefinition), new[] { LevelsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelDefinition>);

        [Test]
        public void AtLeastOneLevelExists()
        {
            Assert.That(AllLevels(), Is.Not.Empty);
        }

        [Test]
        public void EveryLevel_IsValid()
        {
            var errors = new List<string>();
            foreach (var level in AllLevels()) level.Validate(errors);

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void AllFiveDaysExist()
        {
            var days = AllLevels().Select(l => l.DayNumber).OrderBy(d => d);
            Assert.That(days, Is.EqualTo(LevelDefinition.ValidDays));
        }

        [Test]
        public void Day30_DoesNotDrain_AndDay20_HasTwoMeters()
        {
            var levels = AllLevels().ToDictionary(l => l.DayNumber);
            Assert.That(levels[30].WarmthLocked, Is.True, "Day 30: warm together.");
            Assert.That(levels.Values.All(l => l.EffectiveDrainPerSecond == 0f), Is.True, "Warmth must not drain with time.");
            Assert.That(levels[1].MinWarmth, Is.GreaterThanOrEqualTo(15f), "Day 1 (tutorial) can't be lost.");
            Assert.That(levels[20].TwoMeters, Is.True);
        }

        [Test]
        public void EveryLevel_HasAtLeastOneScoredStep()
        {
            foreach (var level in AllLevels())
                Assert.That(level.Steps.Any(s => s.Scored), Is.True, level.name);
        }

        [Test]
        public void DayNumbers_AreUnique()
        {
            var days = AllLevels().Select(l => l.DayNumber).ToList();
            Assert.That(days, Is.Unique);
        }
    }
}
