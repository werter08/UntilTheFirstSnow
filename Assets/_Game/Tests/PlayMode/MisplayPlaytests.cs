using System.Collections;
using System.Linq;
using Courtyard.Core;
using Courtyard.Kitchen;
using Courtyard.Minigames;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Courtyard.Tests
{
    /// <summary>Clumsy or curious players: accidental clicks, early releases, wrong answers, overdoing it.</summary>
    public sealed class MisplayPlaytests : StepPlayers
    {
        /// <summary>A quick tap on the kettle must not end InstantCoffee with an almost empty mug.</summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator InstantCoffee_QuickTapOnKettle_DoesNotEndTheStep()
        {
            yield return LoadDay(1);
            yield return PlayKettleBoil();
            yield return AdvanceTo<InstantCoffeeStep>();
            var step = Object.FindAnyObjectByType<InstantCoffeeStep>();
            yield return Drag(step.transform.Find("Spoon").position, step.transform.Find("Jar").position, Counter.Mug.transform.position + Vector3.up * 0.2f);
            yield return Wait(0.4f);

            yield return Click(Counter.KettleTarget.transform);
            yield return Wait(0.3f);
            Assert.That(step.IsComplete, Is.False, "Picking up and putting down the kettle ended the step.");
        }

        /// <summary>Three spoons: too strong — "it's bad, let's remake it": the reason shows and the drink starts again.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator InstantCoffee_ThreeSpoons_IsRemade()
        {
            yield return LoadDay(1);
            yield return PlayKettleBoil();
            yield return AdvanceTo<InstantCoffeeStep>();
            var step = Object.FindAnyObjectByType<InstantCoffeeStep>();
            for (var i = 0; i < 3; i++)
            {
                yield return Drag(step.transform.Find("Spoon").position, step.transform.Find("Jar").position, Counter.Mug.transform.position + Vector3.up * 0.2f);
                yield return Wait(0.5f);
            }

            yield return CarryKettleAndPour(0.85f);
            yield return WaitForCompletion();
            Assert.That(LastQuality, Is.EqualTo(0f), "Wrong amount of coffee: a spoiled (cold) cup.");
            Assert.That(step.Mistake, Does.Contain("Too much coffee"), "The player is told why.");
            Assert.That(GetField<bool>(Runner, "_failed"), Is.False, "A spoiled drink isn't a lost day.");
            yield return ExpectRemake();
            yield return PlayKettleBoil();
            yield return PlayInstantCoffee();
            yield return FinishDay(60f);
        }

        /// <summary>One spoon: too weak — the reason shows and the drink is remade.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator InstantCoffee_OneSpoon_IsRemade()
        {
            yield return LoadDay(1);
            yield return PlayKettleBoil();
            yield return AdvanceTo<InstantCoffeeStep>();
            var step = Object.FindAnyObjectByType<InstantCoffeeStep>();
            for (var i = 0; i < 1; i++)
            {
                yield return Drag(step.transform.Find("Spoon").position, step.transform.Find("Jar").position, Counter.Mug.transform.position + Vector3.up * 0.2f);
                yield return Wait(0.5f);
            }

            yield return CarryKettleAndPour(0.85f);
            yield return WaitForCompletion();
            Assert.That(LastQuality, Is.EqualTo(0f), "Wrong amount of coffee: a spoiled (cold) cup.");
            Assert.That(step.Mistake, Does.Contain("Not enough coffee"), "The player is told why.");
            yield return ExpectRemake();
        }

        /// <summary>Letting go of the grinder handle before the green zone should let the player keep grinding.</summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator Grinder_EarlyRelease_KeepsGrinding()
        {
            yield return LoadDay(5);
            yield return PlayKettleBoil();
            yield return PlayPlaceV60();
            yield return AdvanceTo<GrindBeansStep>();
            var step = Object.FindAnyObjectByType<GrindBeansStep>();
            yield return CarryGrinderToCup(step);
            float Grind() => GetField<float>(step, "_grind");
            var axle = Counter.GrinderArm.position;
            var knob = Counter.GrinderHandle.transform.position;
            var radius = Vector2.Distance(axle, knob);
            var angle = Mathf.Atan2(knob.y - axle.y, knob.x - axle.x);
            yield return Press(knob);
            while (Grind() < 0.25f)
            {
                angle += 0.25f;
                Move(Mouse.position, ToScreen(axle + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius));
                yield return null;
            }

            yield return ReleaseMouse();
            yield return Wait(0.3f);
            Assert.That(step.IsComplete, Is.False, "Releasing the handle at 25% ended the grind (target is ~55%).");
        }

        /// <summary>A short tap on the milk jug should not end SteamMilk with cold milk.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator SteamMilk_ShortTap_DoesNotEndTheStep()
        {
            yield return LoadDay(30);
            yield return PlayKettleBoil();
            yield return PlayPourWater();
            yield return AdvanceTo<SteamMilkStep>();
            var step = Object.FindAnyObjectByType<SteamMilkStep>();
            yield return Press(Counter.MilkJug.transform.position);
            yield return Wait(0.6f);
            yield return ReleaseMouse();
            yield return Wait(0.3f);
            Assert.That(step.IsComplete, Is.False, "A short tap on the jug ended SteamMilk.");
        }

        /// <summary>Forgetting the kettle: it keeps heating, then switches itself off with a poor result.</summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator Kettle_Forgotten_SwitchesOffByItself()
        {
            yield return LoadDay(1);
            yield return AdvanceTo<KettleBoilStep>();
            yield return Click(Counter.KettleSwitch.transform);
            yield return WaitForCompletion(30f);
            Assert.That(LastQuality, Is.LessThan(0.3f), "Boiling dry should score poorly.");
        }

        /// <summary>
        /// Terrible coffee on Day 1 (the kettle boils dry, a splash of water): each bad step chills, but the tutorial
        /// can't be lost — warmth stops at its floor and the day finishes.
        /// </summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator Day01_TerribleCoffee_CannotBeLost()
        {
            yield return LoadDay(1);
            yield return AdvanceTo<KettleBoilStep>();
            yield return Click(Counter.KettleSwitch.transform);
            yield return WaitForCompletion(30f);
            var meter = GetField<WarmthGroup>(Runner, "_warmth").Meters[0];
            var before = meter.Value;
            yield return Wait(2f); // frost + shiver, then the meter drops
            Assert.That(meter.Value, Is.LessThan(before - 5f), "A badly made step should chill.");

            // Remade: boiled dry again, and again — warmth stops at the floor; then made properly.
            yield return ExpectRemake();
            yield return Click(Counter.KettleSwitch.transform);
            yield return WaitForCompletion(30f);
            yield return Wait(3f);
            yield return PlayKettleBoil();
            yield return PlayInstantCoffee();
            Assert.That(meter.Value, Is.GreaterThanOrEqualTo(LoadLevel(1).MinWarmth), "Day 1 never drops below its floor.");
            Assert.That(GetField<bool>(Runner, "_failed"), Is.False, "Day 1 (tutorial) can't be lost.");
            yield return FinishDay(60f);
        }

        /// <summary>Day 10, wrong toppings: her drink is remade from its kettle step (her cup stays), then finished.</summary>
        [UnityTest, Timeout(400000)]
        public IEnumerator Day10_WrongToppings_AreRemade()
        {
            yield return LoadDay(10);
            yield return PlayKettleBoil();
            yield return PlayPlaceV60();
            yield return PlayGrindBeans();
            yield return PlayPourOver();
            yield return PlayTakeHerCup();
            yield return PlayKettleBoil();
            yield return PlayPourWater();
            yield return PlaySteamMilk();
            yield return PlayPickToppings<PickToppingsStep>(Topping.Salt, Topping.Cocoa);
            Assert.That(LastQuality, Is.EqualTo(0f));
            yield return ExpectRemake();
            yield return PlayKettleBoil();
            yield return PlayPourWater();
            yield return PlaySteamMilk();
            yield return PlayPickToppings<PickToppingsStep>(Topping.Cinnamon, Topping.Honey);
            yield return FinishDay();
        }

        /// <summary>The same spice twice is too much: the drink is spoiled when served, the day goes on.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator Spices_SameSpiceTwice_SpoilsTheDrink()
        {
            yield return LoadDay(30);
            yield return PlayKettleBoil();
            yield return PlayPourWater();
            yield return PlaySteamMilk();
            yield return AdvanceTo<PickToppingsStep>();
            yield return AddSpice(Topping.Cinnamon);
            yield return AddSpice(Topping.Cinnamon);
            Assert.That(GetField<bool>(Runner, "_failed"), Is.False, "Too much spice doesn't restart the day.");
            yield return Click(Counter.Mug.transform);
            yield return WaitForCompletion();
            Assert.That(LastQuality, Is.EqualTo(0f), "Two portions of cinnamon spoil the drink.");
            Assert.That(GetField<bool>(Runner, "_failed"), Is.False);
            yield return ExpectRemake();
        }

        /// <summary>Serving with only one spice is too little: a spoiled drink, the day goes on.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator Spices_ServeWithOne_SpoilsTheDrink()
        {
            yield return LoadDay(30);
            yield return PlayKettleBoil();
            yield return PlayPourWater();
            yield return PlaySteamMilk();
            yield return AdvanceTo<PickToppingsStep>();
            yield return AddSpice(Topping.Honey);
            yield return Click(Counter.Mug.transform);
            yield return WaitForCompletion();
            Assert.That(LastQuality, Is.EqualTo(0f), "One spice is too little.");
            Assert.That(GetField<bool>(Runner, "_failed"), Is.False, "A spoiled drink isn't a lost day.");
            yield return ExpectRemake();
        }

        /// <summary>A spice jar dropped away from the cup just goes back to its place.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator Spices_DroppedAwayFromCup_GoesBack()
        {
            yield return LoadDay(30);
            yield return PlayKettleBoil();
            yield return PlayPourWater();
            yield return PlaySteamMilk();
            yield return AdvanceTo<PickToppingsStep>();
            var jar = Counter.ToppingJars.First(j => j.Topping == Topping.Cocoa);
            var rest = jar.transform.position;
            yield return Drag(rest, rest + new Vector3(-3f, 1.5f, 0f));
            yield return Wait(0.6f);
            Assert.That(Vector3.Distance(jar.transform.position, rest), Is.LessThan(0.05f), "Jar should return to its place.");
            Assert.That(jar.IsPicked, Is.False);
        }

        /// <summary>Day 20: picking a decoy mug is gentle (0.6), not a fail.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Day20_WrongMug_IsGentle()
        {
            yield return LoadDay(20);
            yield return PlayReadHerWindow();
            yield return PlayKettleBoil();
            yield return PlayChooseMug(hers: false);
            Assert.That(LastQuality, Is.EqualTo(0.6f).Within(0.001f));
            yield return PlayPlaceV60();
            yield return PlayGrindBeans();
            yield return PlayPourOver();
            yield return PlaySteamMilk();
            yield return FinishDay();
        }
    }
}
