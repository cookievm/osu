// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Users;

namespace osu.Game.Overlays.Rankings
{
    public partial class RankingsOverlayHeader : TabControlOverlayHeader<RankingsScope>
    {
        public Bindable<RulesetInfo> Ruleset => rulesetSelector.Current;

        public Bindable<CountryCode> Country => countryFilter.Current;

        private OverlayRulesetSelector rulesetSelector = null!;
        private CountryFilter countryFilter = null!;

        protected override OverlayTitle CreateTitle() => new RankingsTitle();

        protected override Drawable CreateTabControlContent() => rulesetSelector = new OverlayRulesetSelector();

        protected override Drawable CreateContent() => countryFilter = new CountryFilter();

        protected override Drawable CreateBackground() => new OverlayHeaderBackground("Headers/rankings");

        /// <summary>
        /// oily (ilo, 2026-11-26): the relax pages only exist for osu!, so taiko, catch and mania only show the normal pages.
        /// </summary>
        public static bool HasRelaxPages(RulesetInfo? ruleset) => ruleset == null || ruleset.OnlineID == 0;

        public static bool IsRelaxScope(RankingsScope scope) => scope == RankingsScope.PerformanceRelax || scope == RankingsScope.CountryRelax;

        private void updateRelaxTabs()
        {
            bool relaxShown = HasRelaxPages(Ruleset.Value);

            var scope = Current.Value;

            // a relax page moves to its normal page.
            if (!relaxShown && IsRelaxScope(scope))
                scope = scope == RankingsScope.CountryRelax ? RankingsScope.Country : RankingsScope.Performance;

            var items = Enum.GetValues<RankingsScope>().Where(s => relaxShown || !IsRelaxScope(s)).ToArray();

            if (!TabControl.Items.SequenceEqual(items))
            {
                TabControl.Items = items;

                // new tabs don't get the colour on their own, which would leave their text invisible until clicked.
                TabControl.AccentColour = TabControl.AccentColour;
            }

            // replacing the tabs can reset the current page, so put the right one back.
            Current.Value = scope;
        }

        private partial class RankingsTitle : OverlayTitle
        {
            public RankingsTitle()
            {
                Title = PageTitleStrings.MainRankingControllerDefault;
                Description = NamedOverlayComponentStrings.RankingsDescription;
                Icon = OsuIcon.Ranking;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(scope =>
            {
                rulesetSelector.FadeTo(showRulesetSelector(scope.NewValue) ? 1 : 0, 200, Easing.OutQuint);
            }, true);

            Ruleset.BindValueChanged(_ => updateRelaxTabs(), true);

            bool showRulesetSelector(RankingsScope scope)
            {
                switch (scope)
                {
                    case RankingsScope.Performance:
                    case RankingsScope.PerformanceRelax:
                    case RankingsScope.Country:
                    case RankingsScope.CountryRelax:
                        return true;

                    default:
                        return false;
                }
            }
        }
    }
}
