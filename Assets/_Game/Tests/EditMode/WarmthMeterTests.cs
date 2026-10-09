using Courtyard.Core;
using NUnit.Framework;

namespace Courtyard.Tests
{
    public sealed class WarmthMeterTests
    {
        [Test]
        public void Tick_DrainsByRateTimesDeltaTime()
        {
            var meter = new WarmthMeter(70f, 2.5f);
            meter.Tick(2f);
            Assert.That(meter.Value, Is.EqualTo(65f).Within(0.001f));
        }

        [Test]
        public void Restore_ClampsAtMax()
        {
            var meter = new WarmthMeter(90f, 1f);
            meter.Restore(50f);
            Assert.That(meter.Value, Is.EqualTo(WarmthMeter.Max));
        }

        [Test]
        public void Constructor_ClampsStartValue()
        {
            Assert.That(new WarmthMeter(150f, 1f).Value, Is.EqualTo(WarmthMeter.Max));
            Assert.That(new WarmthMeter(-5f, 1f).IsEmpty, Is.True);
        }

        [Test]
        public void Tick_ClampsAtZero_AndRaisesDepletedOnce()
        {
            var meter = new WarmthMeter(1f, 10f);
            var depleted = 0;
            meter.Depleted += () => depleted++;

            meter.Tick(1f);
            meter.Tick(1f);

            Assert.That(meter.Value, Is.EqualTo(0f));
            Assert.That(depleted, Is.EqualTo(1));
        }

        [Test]
        public void Empty_MeterCannotBeRestored()
        {
            var meter = new WarmthMeter(1f, 10f);
            meter.Tick(1f);
            meter.Restore(50f);
            Assert.That(meter.IsEmpty, Is.True);
        }

        [Test]
        public void ZeroDrain_IsLockedAtFull()
        {
            var meter = new WarmthMeter(40f, 0f);
            meter.Tick(100f);

            Assert.That(meter.IsLocked, Is.True);
            Assert.That(meter.Value, Is.EqualTo(WarmthMeter.Max));
        }

        [Test]
        public void Changed_IsRaisedWithNewValue()
        {
            var meter = new WarmthMeter(50f, 1f);
            var last = -1f;
            meter.Changed += v => last = v;

            meter.Restore(10f);

            Assert.That(last, Is.EqualTo(60f).Within(0.001f));
        }
    }
}
