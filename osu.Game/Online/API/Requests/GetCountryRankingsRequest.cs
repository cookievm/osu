// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets;

namespace osu.Game.Online.API.Requests
{
    public class GetCountryRankingsRequest : GetRankingsRequest<GetCountriesResponse>
    {
        /// <summary>
        /// oily: whether this asks for countries ranked on the Lazer Relax board.
        /// </summary>
        public readonly bool Relax;

        public GetCountryRankingsRequest(RulesetInfo ruleset, int page = 1, bool relax = false)
            : base(ruleset, page)
        {
            Relax = relax;
        }

        protected override string TargetPostfix() => Relax ? "country-relax" : "country";
    }
}
