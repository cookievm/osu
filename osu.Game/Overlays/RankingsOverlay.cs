// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Overlays.Rankings;
using osu.Game.Users;
using osu.Game.Rulesets;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Rankings.Tables;

namespace osu.Game.Overlays
{
    public partial class RankingsOverlay : TabbableOnlineOverlay<RankingsOverlayHeader, RankingsScope>
    {
        protected Bindable<CountryCode> Country => Header.Country;

        private APIRequest lastRequest;

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> parentRuleset { get; set; }

        [Cached]
        private readonly Bindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>();

        public RankingsOverlay()
            : base(OverlayColourScheme.Green)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Header.Ruleset.BindTo(ruleset);

            Country.BindValueChanged(_ =>
            {
                // if a country is requested, force a performance scope (oily: the relax one can filter by country too).
                if (!Country.IsDefault && !isPerformanceScope(Header.Current.Value))
                    Header.Current.Value = RankingsScope.Performance;

                Scheduler.AddOnce(triggerTabChanged);
            });

            ruleset.BindValueChanged(_ => Scheduler.AddOnce(triggerTabChanged));
        }

        private bool requiresRulesetUpdate = true;

        protected override void PopIn()
        {
            if (requiresRulesetUpdate)
            {
                ruleset.Value = parentRuleset.Value;
                requiresRulesetUpdate = false;
            }

            base.PopIn();
        }

        protected override void OnTabChanged(RankingsScope tab)
        {
            // country filtering is only valid for performance scopes.
            if (!isPerformanceScope(Header.Current.Value))
                Country.SetDefault();

            Scheduler.AddOnce(triggerTabChanged);
        }

        private static bool isPerformanceScope(RankingsScope scope) => scope == RankingsScope.Performance || scope == RankingsScope.PerformanceRelax;

        private void triggerTabChanged() => base.OnTabChanged(Header.Current.Value);

        protected override RankingsOverlayHeader CreateHeader() => new RankingsOverlayHeader();

        public void ShowCountry(CountryCode requested)
        {
            if (requested == default)
                return;

            Show();

            Country.Value = requested;
        }

        protected override void CreateDisplayToLoad(RankingsScope tab)
        {
            lastRequest?.Cancel();

            var request = createScopedRequest();
            lastRequest = request;

            if (request == null)
            {
                LoadDisplay(Empty());
                return;
            }

            request.Success += () => Schedule(() => LoadDisplay(createTableFromResponse(request)));
            request.Failure += _ => Schedule(() => LoadDisplay(Empty()));

            api.Queue(request);
        }

        private APIRequest createScopedRequest()
        {
            switch (Header.Current.Value)
            {
                case RankingsScope.Performance:
                    return new GetUserRankingsRequest(ruleset.Value, countryCode: Country.Value);

                case RankingsScope.Country:
                    return new GetCountryRankingsRequest(ruleset.Value);

                case RankingsScope.PerformanceRelax:
                    return new GetUserRankingsRequest(ruleset.Value, UserRankingsType.Relax, countryCode: Country.Value);
            }

            return null;
        }

        private Drawable createTableFromResponse(APIRequest request)
        {
            switch (request)
            {
                case GetUserRankingsRequest userRequest:
                    if (userRequest.Response == null)
                        return null;

                    switch (userRequest.Type)
                    {
                        case UserRankingsType.Performance:
                        case UserRankingsType.Relax:
                            return new PerformanceTable(1, userRequest.Response.Users);

                        case UserRankingsType.Score:
                            return new ScoresTable(1, userRequest.Response.Users);
                    }

                    return null;

                case GetCountryRankingsRequest countryRequest:
                {
                    if (countryRequest.Response == null)
                        return null;

                    return new CountriesTable(1, countryRequest.Response.Countries);
                }
            }

            return null;
        }

        protected override void Dispose(bool isDisposing)
        {
            lastRequest?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
