// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online.API.Requests;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Online
{
    /// <summary>
    /// oily: the rankings pages ask the o!ily server for the right board.
    /// </summary>
    [TestFixture]
    public class TestOilyRankingsRequest
    {
        [Test]
        public void TestRelaxAsksForRelaxBoard()
        {
            var request = new TestRequest(UserRankingsType.Relax);
            Assert.That(request.Path, Is.EqualTo("rankings/osu/relax"));
        }

        [Test]
        public void TestPerformanceAsksForPerformanceBoard()
        {
            var request = new TestRequest(UserRankingsType.Performance);
            Assert.That(request.Path, Is.EqualTo("rankings/osu/performance"));
        }

        [Test]
        public void TestCountryRelaxAsksForRelaxCountries()
        {
            var request = new TestCountryRequest(true);
            Assert.That(request.Path, Is.EqualTo("rankings/osu/country-relax"));
            Assert.That(new TestCountryRequest(false).Path, Is.EqualTo("rankings/osu/country"));
        }

        private class TestCountryRequest : GetCountryRankingsRequest
        {
            public TestCountryRequest(bool relax)
                : base(new OsuRuleset().RulesetInfo, relax: relax)
            {
            }

            public string Path => Target;
        }

        private class TestRequest : GetUserRankingsRequest
        {
            public TestRequest(UserRankingsType type)
                : base(new OsuRuleset().RulesetInfo, type)
            {
            }

            public string Path => Target;
        }
    }
}
