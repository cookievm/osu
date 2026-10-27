// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;

namespace Oily.Rendering.Tests
{
    [TestFixture]
    public class RenderSettingsTest
    {
        [TestCase(60, 10, 600)]
        [TestCase(120, 10, 1200)]
        [TestCase(240, 1.5, 360)]
        public void TestFrameCount(int fps, double seconds, int expected)
        {
            var settings = new RenderSettings { FramesPerSecond = fps, Duration = TimeSpan.FromSeconds(seconds) };
            Assert.That(settings.FrameCount, Is.EqualTo(expected));
        }

        [Test]
        public void TestNoDurationRendersUntilEnd()
        {
            Assert.That(new RenderSettings { Duration = null }.FrameCount, Is.Null);
        }

        [Test]
        public void TestFrameTimeDoesNotDrift()
        {
            var settings = new RenderSettings { FramesPerSecond = 240 };

            // One hour of frames: the time is computed from the index, so it lands exactly on the hour.
            Assert.That(settings.FrameTime(240 * 3600), Is.EqualTo(3600_000));
        }

        [Test]
        public void TestSourceClockOnlyMovesWhenAdvanced()
        {
            var clock = new RenderSourceClock { ModRate = () => 1.5 };

            clock.Seek(-1000);
            clock.Advance(100);
            Assert.That(clock.CurrentTime, Is.EqualTo(-1000), "stopped clock should not move");

            clock.Start();
            clock.Advance(100);
            Assert.That(clock.CurrentTime, Is.EqualTo(-850), "should move by video time times the mod rate");
        }
    }
}
