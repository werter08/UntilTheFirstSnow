using System.Collections;
using System.Linq;
using Courtyard.Core;
using Courtyard.Kitchen;
using Courtyard.Minigames;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Courtyard.Tests
{
    /// <summary>How a careful player plays each minigame; every helper asserts the step finishes with good quality.</summary>
    public abstract class StepPlayers : KitchenPlaytest
    {
        protected IEnumerator PlayKettleBoil(float stopAt = 93f, float minQuality = 0.9f)
        {
            yield return AdvanceTo<KettleBoilStep>();
            var step = Object.FindAnyObjectByType<KettleBoilStep>();
            var switchT = Counter.KettleSwitch.transform;
            // A click right after the story box closes can land while it's still fading out: click again, like a player.
            for (var attempt = 0; attempt < 3 && GetField<object>(step, "_state").ToString() == "Idle"; attempt++)
            {
                yield return Click(switchT);
                yield return Wait(0.1f);
            }

            Assert.That(GetField<object>(step, "_state").ToString(), Is.EqualTo("Heating"), "Kettle switch click did not start heating.");

            var deadline = Time.realtimeSinceStartup + 20f;
            while (GetField<float>(step, "_heat") < stopAt && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(GetField<float>(Counter.KettleView, "_boil"), Is.EqualTo(1f), "Lid should rattle once the water boils.");
            yield return Click(switchT);
            yield return WaitForCompletion();
            Assert.That(LastQuality, Is.GreaterThanOrEqualTo(minQuality), "KettleBoil quality");
        }

        protected IEnumerator PlayInstantCoffee()
        {
            yield return AdvanceTo<InstantCoffeeStep>();
            var step = Object.FindAnyObjectByType<InstantCoffeeStep>();
            var spoon = step.transform.Find("Spoon");
            var jar = step.transform.Find("Jar");
            for (var i = 0; i < 2; i++)
            {
                yield return Drag(spoon.position, jar.position, Counter.Mug.transform.position + Vector3.up * 0.2f);
                yield return Wait(0.5f);
            }

            Assert.That(GetField<int>(step, "_spoons"), Is.EqualTo(2), "Two spoons should have landed in the mug.");
            yield return CarryKettleAndPour(0.85f);
            yield return WaitForCompletion();
            Assert.That(LastQuality, Is.GreaterThanOrEqualTo(0.85f), "InstantCoffee quality");
        }

        /// <summary>Picks up the kettle, carries it over the cup and pours until <paramref name="level"/>, then lets go.</summary>
        protected IEnumerator CarryKettleAndPour(float level)
        {
            var from = Counter.KettleTarget.transform.position;
            var over = Counter.MugTop + new Vector3(0.05f, 0.7f, 0f);
            yield return Press(from);
            for (var t = 1; t <= 20; t++)
            {
                Move(Mouse.position, ToScreen(Vector3.Lerp(from, over, t / 20f)));
                yield return null;
            }

            var deadline = Time.realtimeSinceStartup + 15f;
            while (Counter.MugLevel < level && Time.realtimeSinceStartup < deadline) yield return null;
            yield return ReleaseMouse();
        }

        /// <summary>Carries a spice jar over the cup and lets go: one portion shakes in.</summary>
        protected IEnumerator AddSpice(Topping topping)
        {
            var jar = Counter.ToppingJars.First(j => j.Topping == topping);
            yield return Drag(jar.transform.position, Counter.MugTop + new Vector3(0f, 0.6f, 0f));
            yield return Wait(0.3f);
            var step = Object.FindAnyObjectByType<PickToppingsStep>();
            var deadline = Time.realtimeSinceStartup + 5f; // shake + jar goes back
            while (step != null && GetField<bool>(step, "_busy") && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Wait(0.1f);
        }

        protected IEnumerator PlayGrindBeans()
        {
            yield return AdvanceTo<GrindBeansStep>();
            var step = Object.FindAnyObjectByType<GrindBeansStep>();
            var meter = GetField<SpriteBar>(step, "meter");
            var axle = Counter.GrinderArm.position;
            var knob = Counter.GrinderHandle.transform.position;
            var radius = Vector2.Distance(axle, knob);
            var angle = Mathf.Atan2(knob.y - axle.y, knob.x - axle.x);

            yield return Press(knob);
            var deadline = Time.realtimeSinceStartup + 25f;
            while (meter.Value < 0.55f && Time.realtimeSinceStartup < deadline)
            {
                angle += 0.25f;
                Move(Mouse.position, ToScreen(axle + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius));
                yield return null;
            }

            yield return ReleaseMouse();
            yield return WaitForCompletion();
            Assert.That(LastQuality, Is.GreaterThanOrEqualTo(0.8f), "GrindBeans quality");
        }

        protected IEnumerator PlayPourOver()
        {
            yield return AdvanceTo<PourOverStep>();
            // Grab the kettle on its base, carry it above the cup (V60), then pour in circles.
            var from = Counter.KettleTarget.transform.position;
            var centre = Counter.MugTop + new Vector3(0f, 0.9f, 0f);
            yield return Press(from);
            for (var t = 1; t <= 20; t++)
            {
                Move(Mouse.position, ToScreen(Vector3.Lerp(from, centre, t / 20f)));
                yield return null;
            }

            var deadline = Time.realtimeSinceStartup + 25f;
            var angle = 0f;
            while (Counter.MugLevel < 0.8f && Time.realtimeSinceStartup < deadline)
            {
                angle += 7f * Time.deltaTime; // ~1.1 turn/s (game time) at 0.45 units ≈ 3 units/s: steady circles
                Move(Mouse.position, ToScreen(centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.45f));
                yield return null;
            }

            yield return ReleaseMouse();
            yield return WaitForCompletion();
            Assert.That(LastQuality, Is.GreaterThanOrEqualTo(0.6f), "PourOver quality");
        }

        protected IEnumerator PlayWaveBack()
        {
            yield return AdvanceTo<WaveBackStep>();
            yield return Click(Counter.Mug.transform);
            yield return WaitForCompletion();
        }

        protected IEnumerator PlaySteamMilk()
        {
            yield return AdvanceTo<SteamMilkStep>();
            var step = Object.FindAnyObjectByType<SteamMilkStep>();
            yield return Press(Counter.MilkJug.transform.position);
            var deadline = Time.realtimeSinceStartup + 20f;
            while (GetField<float>(step, "_temperature") < 64f && Time.realtimeSinceStartup < deadline) yield return null;
            yield return ReleaseMouse();
            yield return WaitForCompletion();
            Assert.That(LastQuality, Is.GreaterThanOrEqualTo(0.9f), "SteamMilk quality");
        }

        protected IEnumerator PlayPickToppings<T>(params Topping[] toppings) where T : MinigameStep
        {
            yield return AdvanceTo<PickToppingsStep>();
            foreach (var topping in toppings)
            {
                yield return AddSpice(topping);
                Assert.That(Counter.ToppingJars.First(j => j.Topping == topping).IsPicked, Is.True, $"{topping} should be in the cup.");
            }

            yield return Click(Counter.Mug.transform);
            yield return WaitForCompletion();
        }

        protected IEnumerator PlayChooseMug(bool hers = true)
        {
            yield return AdvanceTo<ChooseMugStep>();
            var rack = Object.FindAnyObjectByType<KitchenDirector>().RackMugs;
            Assert.That(rack.Count, Is.GreaterThan(1), "ChooseMug needs mugs on the wall rack.");
            var pick = rack.First(m => (m.style == MugStyle.Hers) == hers).slot;
            yield return Click(pick);
            yield return WaitForCompletion();
        }

        protected IEnumerator FinishDay(float timeoutSeconds = 40f)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != SceneNames.Ending && Time.realtimeSinceStartup < deadline)
            {
                yield return ClickScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.25f));
                yield return Wait(0.2f);
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Ending), "Day did not finish.");
        }
    }
}
