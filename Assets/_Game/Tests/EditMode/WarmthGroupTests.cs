using Courtyard.Core;
using NUnit.Framework;

namespace Courtyard.Tests
{
    public sealed class WarmthGroupTests
    {
        [Test]
        public void TwoMeters_FailWhenEitherEmpties()
        {
            var group = new WarmthGroup(2, 10f, 1f);
            var depleted = 0;
            group.Depleted += () => depleted++;

            group.Restore(WarmthTarget.Him, 50f); // only Mira stays low
            group.Tick(10f);

            Assert.That(group.Meters[WarmthGroup.HimIndex].IsEmpty, Is.False);
            Assert.That(group.Meters[WarmthGroup.MiraIndex].IsEmpty, Is.True);
            Assert.That(group.IsDepleted, Is.True);
            Assert.That(depleted, Is.EqualTo(1));
        }

        [Test]
        public void Depleted_IsRaisedOnce_WhenBothEmpty()
        {
            var group = new WarmthGroup(2, 5f, 1f);
            var depleted = 0;
            group.Depleted += () => depleted++;

            group.Tick(10f);

            Assert.That(depleted, Is.EqualTo(1));
        }

        [Test]
        public void MiraTarget_RestoresOnlyHerMeter()
        {
            var group = new WarmthGroup(2, 50f, 1f);
            group.Restore(WarmthTarget.Mira, 20f);

            Assert.That(group.Meters[WarmthGroup.HimIndex].Value, Is.EqualTo(50f));
            Assert.That(group.Meters[WarmthGroup.MiraIndex].Value, Is.EqualTo(70f));
        }

        [Test]
        public void MiraTarget_OnOneMeterDay_RestoresTheOnlyMeter()
        {
            var group = new WarmthGroup(1, 50f, 1f);
            group.Restore(WarmthTarget.Mira, 20f);

            Assert.That(group.Meters[0].Value, Is.EqualTo(70f));
        }

        [Test]
        public void BothTarget_RestoresAllMeters()
        {
            var group = new WarmthGroup(2, 50f, 1f);
            group.Restore(WarmthTarget.Both, 10f);

            Assert.That(group.Meters[0].Value, Is.EqualTo(60f));
            Assert.That(group.Meters[1].Value, Is.EqualTo(60f));
        }

        [Test]
        public void ZeroDrain_NeverDepletes()
        {
            var group = new WarmthGroup(1, 70f, 0f);
            group.Tick(1000f);

            Assert.That(group.IsDepleted, Is.False);
            Assert.That(group.Meters[0].Value, Is.EqualTo(70f), "Time alone doesn't change warmth.");
        }
    }
}
