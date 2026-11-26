// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Overlays.Rankings;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Users;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneRankingsOverlay : OsuTestScene
    {
        protected override bool UseOnlineAPI => true;

        private TestRankingsOverlay rankingsOverlay;

        private readonly Bindable<CountryCode> countryBindable = new Bindable<CountryCode>();
        private readonly Bindable<RankingsScope> scope = new Bindable<RankingsScope>();

        [SetUp]
        public void SetUp() => Schedule(loadRankingsOverlay);

        [Test]
        public void TestParentRulesetDecoupledAfterInitialShow()
        {
            AddStep("enable global ruleset", () => Ruleset.Disabled = false);
            AddStep("set global ruleset to osu!catch", () => Ruleset.Value = new CatchRuleset().RulesetInfo);
            AddStep("reload rankings overlay", loadRankingsOverlay);
            AddAssert("rankings ruleset set to osu!catch", () => rankingsOverlay.Header.Ruleset.Value.ShortName == CatchRuleset.SHORT_NAME);

            AddStep("set global ruleset to osu!", () => Ruleset.Value = new OsuRuleset().RulesetInfo);
            AddAssert("rankings ruleset still osu!catch", () => rankingsOverlay.Header.Ruleset.Value.ShortName == CatchRuleset.SHORT_NAME);

            AddStep("disable global ruleset", () => Ruleset.Disabled = true);
            AddAssert("rankings ruleset still enabled", () => rankingsOverlay.Header.Ruleset.Disabled == false);
            AddStep("set rankings ruleset to osu!mania", () => rankingsOverlay.Header.Ruleset.Value = new ManiaRuleset().RulesetInfo);
            AddAssert("rankings ruleset set to osu!mania", () => rankingsOverlay.Header.Ruleset.Value.ShortName == ManiaRuleset.SHORT_NAME);
        }

        [Test]
        public void TestFlagScopeDependency()
        {
            AddStep("Set scope to Country", () => scope.Value = RankingsScope.Country);
            AddAssert("Check country is default", () => countryBindable.IsDefault);
            AddStep("Set country", () => countryBindable.Value = CountryCode.US);
            AddAssert("Check scope is Performance", () => scope.Value == RankingsScope.Performance);
        }

        [Test]
        public void TestRelaxKeepsCountry()
        {
            // oily: the relax board can be filtered by country without jumping back to normal performance.
            AddStep("Set scope to relax", () => scope.Value = RankingsScope.PerformanceRelax);
            AddStep("Set country", () => countryBindable.Value = CountryCode.US);
            AddAssert("Check scope is still relax", () => scope.Value == RankingsScope.PerformanceRelax);
            AddAssert("Check country is kept", () => countryBindable.Value == CountryCode.US);
        }

        [Test]
        public void TestOnlyOilyPages()
        {
            // oily (ilo, 2026-11-26): no score, spotlights or kudosu pages.
            AddAssert("pages are performance, relax, country and country relax", () => System.Enum.GetValues<RankingsScope>(),
                () => Is.EqualTo(new[] { RankingsScope.Performance, RankingsScope.PerformanceRelax, RankingsScope.Country, RankingsScope.CountryRelax }));
        }

        [Test]
        public void TestRelaxPagesOnlyForOsu()
        {
            // oily (ilo, 2026-11-26): taiko, catch and mania have no relax pages.
            AddStep("set rankings ruleset to osu!", () => rankingsOverlay.Header.Ruleset.Value = new OsuRuleset().RulesetInfo);
            AddStep("Set scope to country relax", () => scope.Value = RankingsScope.CountryRelax);
            AddAssert("relax tabs shown", () => relaxTabs().Count() == 2);

            AddStep("set rankings ruleset to osu!mania", () => rankingsOverlay.Header.Ruleset.Value = new ManiaRuleset().RulesetInfo);
            AddUntilStep("relax tabs hidden", () => !relaxTabs().Any());
            AddUntilStep("moved to country", () => scope.Value == RankingsScope.Country);

            AddStep("set rankings ruleset to osu!", () => rankingsOverlay.Header.Ruleset.Value = new OsuRuleset().RulesetInfo);
            AddUntilStep("relax tabs back", () => relaxTabs().Count() == 2);

            IEnumerable<TabItem<RankingsScope>> relaxTabs() => rankingsOverlay.Header.ChildrenOfType<TabItem<RankingsScope>>()
                                                                                .Where(t => RankingsOverlayHeader.IsRelaxScope(t.Value));
        }

        [Test]
        public void TestShowCountry()
        {
            AddStep("Show US", () => rankingsOverlay.ShowCountry(CountryCode.US));
        }

        private void loadRankingsOverlay()
        {
            Child = rankingsOverlay = new TestRankingsOverlay
            {
                Country = { BindTarget = countryBindable },
                Header = { Current = { BindTarget = scope } },
                State = { Value = Visibility.Visible },
            };
        }

        private partial class TestRankingsOverlay : RankingsOverlay
        {
            public new Bindable<CountryCode> Country => base.Country;
        }
    }
}
