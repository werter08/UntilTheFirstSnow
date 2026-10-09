using Courtyard.Minigames;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Courtyard.Tests
{
    public sealed class RecipeTests
    {
        private Recipe _recipe;

        [SetUp]
        public void SetUp()
        {
            _recipe = ScriptableObject.CreateInstance<Recipe>();
            var so = new SerializedObject(_recipe);
            var toppings = so.FindProperty("toppings");
            toppings.arraySize = 2;
            toppings.GetArrayElementAtIndex(0).enumValueIndex = (int)Topping.Cinnamon;
            toppings.GetArrayElementAtIndex(1).enumValueIndex = (int)Topping.Honey;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_recipe);

        [Test]
        public void ExactMatch_ScoresOne() =>
            Assert.That(_recipe.Score(new[] { Topping.Honey, Topping.Cinnamon }), Is.EqualTo(1f));

        [Test]
        public void OneRight_ScoresHalf() =>
            Assert.That(_recipe.Score(new[] { Topping.Cinnamon, Topping.Salt }), Is.EqualTo(0.5f));

        [Test]
        public void NoneRight_ScoresZero() =>
            Assert.That(_recipe.Score(new[] { Topping.Cocoa, Topping.Salt }), Is.EqualTo(0f));

        [Test]
        public void ExtraToppings_LowerTheScore() =>
            Assert.That(_recipe.Score(new[] { Topping.Cinnamon, Topping.Honey, Topping.Salt }), Is.EqualTo(2f / 3f).Within(0.001f));

        [Test]
        public void MiraUsual_IsCinnamonAndHoney()
        {
            var usual = AssetDatabase.LoadAssetAtPath<Recipe>("Assets/_Game/Data/Recipes/Mira_Usual.asset");
            Assert.That(usual, Is.Not.Null);
            Assert.That(usual.Score(new[] { Topping.Cinnamon, Topping.Honey }), Is.EqualTo(1f));
        }
    }
}
