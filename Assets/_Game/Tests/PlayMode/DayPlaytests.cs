using System.Collections;
using Courtyard.Minigames;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Courtyard.Tests
{
    /// <summary>Each day played start to finish with a virtual mouse.</summary>
    public sealed class DayPlaytests : StepPlayers
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator Day01_KettleAndInstantCoffee()
        {
            yield return LoadDay(1);
            yield return PlayKettleBoil();
            yield return PlayInstantCoffee();
            yield return FinishDay();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Day05_GrindPourAndWaveBack()
        {
            yield return LoadDay(5);
            yield return PlayKettleBoil();
            yield return PlayGrindBeans();
            yield return PlayPourOver();
            yield return PlayWaveBack();
            yield return FinishDay();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Day10_MiraVisitsAndGuessesRight()
        {
            yield return LoadDay(10);
            yield return PlayKettleBoil();
            yield return PlayGrindBeans();
            yield return PlayPourOver();
            yield return PlaySteamMilk();
            yield return PlayPickToppings<PickToppingsStep>(Topping.Cinnamon, Topping.Honey);
            Assert.That(LastQuality, Is.EqualTo(1f), "Cinnamon + honey is Mira's usual.");
            yield return FinishDay();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Day20_TwoMetersAndHerMug()
        {
            yield return LoadDay(20);
            yield return PlayKettleBoil();
            yield return PlayGrindBeans();
            yield return PlayPourOver();
            yield return PlaySteamMilk();
            yield return PlayChooseMug();
            Assert.That(LastQuality, Is.EqualTo(1f), "Her mug is the right one.");
            yield return FinishDay();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Day30_HerUsualFromMemory()
        {
            yield return LoadDay(30);
            yield return PlayKettleBoil();
            yield return PlaySteamMilk();
            yield return PlayPickToppings<PickToppingsStep>(Topping.Honey, Topping.Cinnamon);
            yield return FinishDay();
        }
    }
}
