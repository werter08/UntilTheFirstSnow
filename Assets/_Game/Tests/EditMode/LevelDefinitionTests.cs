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
        public void DayNumbers_AreUnique()
        {
            var days = AllLevels().Select(l => l.DayNumber).ToList();
            Assert.That(days, Is.Unique);
        }
    }
}
