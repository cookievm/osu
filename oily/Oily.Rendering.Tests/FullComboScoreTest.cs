// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;

namespace Oily.Rendering.Tests
{
    [TestFixture]
    public class FullComboScoreTest
    {
        [Test]
        public void TestMissesBecomeBestHits()
        {
            var ruleset = new OsuRuleset();
            var score = new ScoreInfo(ruleset: ruleset.RulesetInfo)
            {
                MaxCombo = 40,
                Accuracy = 0.8,
            };

            score.Statistics[HitResult.Great] = 6;
            score.Statistics[HitResult.Ok] = 2;
            score.Statistics[HitResult.Miss] = 2;
            score.Statistics[HitResult.LargeTickHit] = 3;
            score.Statistics[HitResult.LargeTickMiss] = 1;
            score.MaximumStatistics[HitResult.Great] = 10;
            score.MaximumStatistics[HitResult.LargeTickHit] = 4;
            score.MaximumStatistics[HitResult.Miss] = 0;

            FullComboScore.Apply(score, ruleset.CreateScoreProcessor());

            Assert.That(score.Statistics[HitResult.Miss], Is.Zero);
            Assert.That(score.Statistics[HitResult.Great], Is.EqualTo(8));
            Assert.That(score.Statistics[HitResult.Ok], Is.EqualTo(2), "100s stay 100s");
            Assert.That(score.Statistics[HitResult.LargeTickMiss], Is.Zero);
            Assert.That(score.Statistics[HitResult.LargeTickHit], Is.EqualTo(4));
            Assert.That(score.MaxCombo, Is.EqualTo(14), "every hit object and tick so far");
            Assert.That(score.Accuracy, Is.GreaterThan(0.8).And.LessThan(1));
        }

        [Test]
        public void TestFullComboStaysTheSame()
        {
            var ruleset = new OsuRuleset();
            var score = new ScoreInfo(ruleset: ruleset.RulesetInfo);

            score.Statistics[HitResult.Great] = 10;
            score.MaximumStatistics[HitResult.Great] = 10;

            FullComboScore.Apply(score, ruleset.CreateScoreProcessor());

            Assert.That(score.Statistics[HitResult.Great], Is.EqualTo(10));
            Assert.That(score.MaxCombo, Is.EqualTo(10));
            Assert.That(score.Accuracy, Is.EqualTo(1));
        }
    }
}
