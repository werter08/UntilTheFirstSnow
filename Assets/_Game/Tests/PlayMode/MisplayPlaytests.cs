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
            Assert.That(step.IsComplete, Is.False, "A tap on the kettle ended the step.");
        }

        /// <summary>Letting go of the grinder handle before the green zone should let the player keep grinding.</summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator Grinder_EarlyRelease_KeepsGrinding()
        {
            yield return LoadDay(5);
            yield return PlayKettleBoil();
            yield return AdvanceTo<GrindBeansStep>();
            var step = Object.FindAnyObjectByType<GrindBeansStep>();
            var meter = GetField<SpriteBar>(step, "meter");
            var axle = Counter.GrinderArm.position;
            var knob = Counter.GrinderHandle.transform.position;
            var radius = Vector2.Distance(axle, knob);
            var angle = Mathf.Atan2(knob.y - axle.y, knob.x - axle.x);
            yield return Press(knob);
            while (meter.Value < 0.25f)
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

        /// <summary>Day 10, wrong toppings: the gentle 'It's… nice' ending plays and the day still finishes.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Day10_WrongToppings_StillFinishes()
        {
            yield return LoadDay(10);
            yield return PlayKettleBoil();
            yield return PlayGrindBeans();
            yield return PlayPourOver();
            yield return PlaySteamMilk();
            yield return PlayPickToppings<PickToppingsStep>(Topping.Salt, Topping.Cocoa);
            Assert.That(LastQuality, Is.EqualTo(0f));
            yield return FinishDay();
        }

        /// <summary>Clicking a topping twice un-picks it; the mug only serves with exactly two.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator Toppings_TogglesAndNeedsExactlyTwo()
        {
            yield return LoadDay(30);
            yield return PlayKettleBoil();
            yield return PlaySteamMilk();
            yield return AdvanceTo<PickToppingsStep>();
            var cinnamon = Counter.ToppingJars.First(j => j.Topping == Topping.Cinnamon);
            yield return Click(cinnamon.transform);
            yield return Click(cinnamon.transform);
            Assert.That(cinnamon.IsPicked, Is.False, "Second click should un-pick.");
            yield return Click(Counter.Mug.transform);
            var step = Object.FindAnyObjectByType<PickToppingsStep>();
            Assert.That(step.IsComplete, Is.False, "Mug must not serve with zero toppings.");
        }

        /// <summary>Day 20: picking a decoy mug is gentle (0.6), not a fail.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Day20_WrongMug_IsGentle()
        {
            yield return LoadDay(20);
            yield return PlayKettleBoil();
            yield return PlayGrindBeans();
            yield return PlayPourOver();
            yield return PlaySteamMilk();
            yield return PlayChooseMug(hers: false);
            Assert.That(LastQuality, Is.EqualTo(0.6f).Within(0.001f));
            yield return FinishDay();
        }
    }
}
