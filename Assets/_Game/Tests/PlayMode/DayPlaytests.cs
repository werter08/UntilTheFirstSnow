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
            yield return PlayPlaceV60();
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
            yield return PlayPlaceV60();
            yield return PlayGrindBeans();
            yield return PlayPourOver();
            yield return PlayTakeHerCup();
            yield return PlayKettleBoil();   // her cup: boil, pour, milk, spices — then it's handed to her at the door
            yield return PlayPourWater();
            yield return PlaySteamMilk();
            yield return PlayPourMilk();
            yield return PlayPickToppings<PickToppingsStep>(Topping.Cinnamon, Topping.Honey);
            Assert.That(LastQuality, Is.EqualTo(1f), "Cinnamon + honey is Mira's usual.");
            yield return FinishDay();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Day20_TwoMetersAndHerMug()
        {
            // Her mug first (it goes to the brewing spot), her drink, then his cup fills by itself beside it.
            yield return LoadDay(20);
            yield return PlayReadHerWindow();
            yield return PlayKettleBoil();
            yield return PlayChooseMug();
            Assert.That(LastQuality, Is.EqualTo(1f), "Her mug is the right one.");
            Assert.That(Counter.MugPresent, Is.True, "Her mug should now stand at the brewing spot.");
            yield return PlayPlaceV60();
            yield return PlayGrindBeans();
            yield return PlayPourOver();
            yield return PlaySteamMilk();
            yield return PlayPourMilk();
            yield return AdvanceTo<AutoFillHisMugStep>();
            yield return WaitForCompletion();
            Assert.That(Counter.HasHisCup, Is.True, "His cup should stand beside hers.");
            yield return FinishDay();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Day30_HerUsualFromMemory()
        {
            yield return LoadDay(30);
            yield return PlayKettleBoil();
            yield return PlayPourWater();
            yield return PlaySteamMilk();
            yield return PlayPourMilk();
            yield return PlayPickToppings<PickToppingsStep>(Topping.Honey, Topping.Cinnamon);
            yield return FinishDay();
        }
    }
}
