using System.Collections;
using Courtyard.Core;
using Courtyard.Kitchen;
using Courtyard.Minigames;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Courtyard.Tests
{
    /// <summary>The kitchen reacts: clickable details, Mira waving back, idle nudges.</summary>
    public sealed class LifePlaytests : StepPlayers
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator Day05_ClickingHerWindow_SheWavesBack()
        {
            yield return LoadDay(5);
            yield return AdvanceTo<KettleBoilStep>();
            var director = Object.FindAnyObjectByType<KitchenDirector>();
            Assert.That(director.WindowState, Is.EqualTo(MiraWindowState.LitWarmSilhouette));
            yield return Click(FindDeep("MiraWindow"));
            Assert.That(director.WindowState, Is.EqualTo(MiraWindowState.Waving), "Mira should wave back on Day 5.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Day01_HerWindowDoesNotWaveYet()
        {
            yield return LoadDay(1);
            yield return AdvanceTo<KettleBoilStep>();
            var director = Object.FindAnyObjectByType<KitchenDirector>();
            yield return Click(FindDeep("MiraWindow"));
            Assert.That(director.WindowState, Is.Not.EqualTo(MiraWindowState.Waving), "They haven't met yet on Day 1.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ClickingTheCalendar_MakesItSwing()
        {
            yield return LoadDay(1);
            yield return AdvanceTo<KettleBoilStep>();
            var group = FindDeep("CalendarGroup");
            yield return Click(FindDeep("Calendar"));
            // The swing is a damped spring (crosses zero), so look at the largest deflection over a short while.
            var maxAngle = 0f;
            var until = Time.time + 0.4f;
            while (Time.time < until)
            {
                maxAngle = Mathf.Max(maxAngle, Mathf.Abs(Mathf.DeltaAngle(0f, group.localEulerAngles.z)));
                yield return null;
            }

            Assert.That(maxAngle, Is.GreaterThan(0.5f), "Calendar should swing when clicked.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator IdlePlayer_GetsANudgeOnTheRightTool()
        {
            yield return LoadDay(1);
            yield return AdvanceTo<KettleBoilStep>();
            var target = Object.FindAnyObjectByType<KettleBoilStep>().NudgeTarget;
            Assert.That(target, Is.Not.Null);
            var baseScale = target.localScale;
            var nudged = false;
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!nudged && Time.realtimeSinceStartup < deadline)
            {
                nudged = (target.localScale - baseScale).sqrMagnitude > 1e-4f;
                yield return null;
            }

            Assert.That(nudged, Is.True, "The kettle switch should bounce after the player idles.");
        }
    }
}
