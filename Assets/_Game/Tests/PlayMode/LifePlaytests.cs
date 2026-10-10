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
            Assert.That(director.WindowState, Is.EqualTo(MiraWindowState.Lit), "Day 5 starts with her light on.");
            yield return Click(FindDeep("MiraWindow"));
            Assert.That(director.WindowState, Is.EqualTo(MiraWindowState.Wave), "Mira should wave back on Day 5.");
            yield return Wait(3f);
            Assert.That(director.WindowState, Is.EqualTo(MiraWindowState.Lit), "…then her window is as it was.");
        }

        /// <summary>Day 5: yesterday's note hangs on the fridge — click it to read, click again to put it back.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator Day05_ClickTheFridgeNote_ToReadIt()
        {
            yield return LoadDay(5);
            yield return AdvanceTo<KettleBoilStep>();
            var director = Object.FindAnyObjectByType<KitchenDirector>();
            var note = FindDeep("FridgeNote0");
            Assert.That(note.gameObject.activeInHierarchy, Is.True, "Day 1's note should be on the fridge on Day 5.");
            Assert.That(FindDeep("FridgeNote1").gameObject.activeInHierarchy, Is.False, "Only earlier days' notes are pinned.");

            yield return Click(note);
            yield return Wait(0.3f);
            Assert.That(director.NoteCard.IsOpen, Is.True, "Clicking the note should open it.");
            StringAssert.Contains("coffee", director.NoteCard.GetComponentInChildren<TMPro.TMP_Text>().text);

            yield return ClickScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return Wait(0.3f);
            Assert.That(director.NoteCard.IsOpen, Is.False, "Another click puts the note back.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Day01_HerWindowDoesNotWaveYet()
        {
            yield return LoadDay(1);
            yield return AdvanceTo<KettleBoilStep>();
            var director = Object.FindAnyObjectByType<KitchenDirector>();
            yield return Click(FindDeep("MiraWindow"));
            Assert.That(director.WindowState, Is.Not.EqualTo(MiraWindowState.Wave), "They haven't met yet on Day 1.");
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
