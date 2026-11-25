// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty.Akatsuki;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// oily: o!ily's own pp in song select (Akatsuki relax pp with Relax, no pp for DA and assist mods, lazer's pp otherwise).
    /// </summary>
    [TestFixture]
    public class OilyPerformanceTest
    {
        private static (double? SS, double? Simulated) calculate(Mod[] mods, OilySimulation? simulation = null)
        {
            var ruleset = new OsuRuleset();
            var working = new TestWorkingBeatmap(new TestBeatmap(ruleset.RulesetInfo));
            return OilyPerformance.Calculate(ruleset, working, mods, simulation ?? OilySimulation.DEFAULT);
        }

        [Test]
        public void TestRelaxUsesAkatsukiRelaxPerformance()
        {
            var ruleset = new OsuRuleset();
            var working = new TestWorkingBeatmap(new TestBeatmap(ruleset.RulesetInfo));
            var mods = new Mod[] { new OsuModRelax(), new OsuModHidden() };

            var difficulty = new AkatsukiRelaxDifficulty(working.GetPlayableBeatmap(ruleset.RulesetInfo, mods), 1);
            var attributes = difficulty.AfterObject[^1];
            double expected = AkatsukiRelaxPerformance.Calculate(attributes, new AkatsukiRelaxPlay
            {
                Count300 = attributes.ObjectCount,
                MaxCombo = attributes.MaxCombo,
                Hidden = true,
            });

            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(calculate(mods).SS, Is.EqualTo(expected).Within(1e-6));
        }

        [Test]
        public void TestRelaxFollowsSpeed()
            => Assert.That(calculate(new Mod[] { new OsuModRelax(), new OsuModDoubleTime() }).SS, Is.GreaterThan(calculate(new Mod[] { new OsuModRelax() }).SS!.Value));

        [Test]
        public void TestRelaxSimulationGivesLess()
        {
            var mods = new Mod[] { new OsuModRelax() };
            var (ss, simulated) = calculate(mods, new OilySimulation(95, 3, 60));

            Assert.That(simulated, Is.GreaterThan(0));
            Assert.That(simulated, Is.LessThan(ss!.Value));
        }

        [Test]
        public void TestNoPerformanceForAssistMods()
        {
            Assert.That(calculate(new Mod[] { new OsuModDifficultyAdjust() }), Is.EqualTo(((double?)0, (double?)0)));
            Assert.That(calculate(new Mod[] { new OsuModRelax(), new OsuModMagnetised() }).SS, Is.Zero);
            Assert.That(calculate(new Mod[] { new ModWindUp() }).SS, Is.Zero);
        }

        [Test]
        public void TestWithoutRelaxUsesLazerPerformance()
        {
            var ruleset = new OsuRuleset();
            var working = new TestWorkingBeatmap(new TestBeatmap(ruleset.RulesetInfo));
            var attributes = ruleset.CreateDifficultyCalculator(working).Calculate(Array.Empty<Mod>());

            var (ss, _) = calculate(Array.Empty<Mod>());

            Assert.That(ss, Is.GreaterThan(0));
            Assert.That(ss, Is.Not.EqualTo(calculate(new Mod[] { new OsuModRelax() }).SS));
            Assert.That(ss, Is.EqualTo(OilyPerformance.Calculate(ruleset, working, Array.Empty<Mod>(), null, attributes).SS));
        }
    }
}
