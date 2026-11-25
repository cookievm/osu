// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Skinning.Components;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// oily: the "Simulated pp" skin component's maths.
    /// </summary>
    [TestFixture]
    public class SimulatedPerformanceTest
    {
        private static double calculate(double accuracy, int misses, double combo = 1, Mod[]? mods = null)
        {
            var ruleset = new OsuRuleset();
            var working = new TestWorkingBeatmap(new TestBeatmap(ruleset.RulesetInfo));
            mods ??= Array.Empty<Mod>();

            var attributes = ruleset.CreateDifficultyCalculator(working).Calculate(mods);
            return SimulatedPerformanceText.Calculate(working, ruleset, mods, attributes, accuracy, misses, combo) ?? 0;
        }

        [Test]
        public void TestLowerAccuracyGivesLessPerformance()
            => Assert.That(calculate(0.95, 0), Is.LessThan(calculate(1, 0)));

        [Test]
        public void TestMissesGiveLessPerformance()
            => Assert.That(calculate(1, 5, 0.5), Is.LessThan(calculate(1, 0)));

        [Test]
        public void TestModsChangePerformance()
            => Assert.That(calculate(1, 0, mods: new Mod[] { new OsuModDoubleTime() }), Is.GreaterThan(calculate(1, 0)));

        [Test]
        public void TestPerfectScoreIsPositive()
            => Assert.That(calculate(1, 0), Is.GreaterThan(0));
    }
}
