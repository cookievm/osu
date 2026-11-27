// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using osu.Game.Resources.Localisation.Web;

namespace osu.Game.Overlays.Rankings
{
    /// <summary>
    /// oily (ilo, 2026-11-26): the rankings pages are performance, performance (relax), country and country (relax).
    /// Score, Spotlights and Kudosu were removed.
    /// </summary>
    public enum RankingsScope
    {
        [LocalisableDescription(typeof(RankingsStrings), nameof(RankingsStrings.StatPerformance))]
        Performance,

        /// <summary>
        /// The o!ily lazer Relax board.
        /// </summary>
        [Description("performance (relax)")]
        PerformanceRelax,

        [LocalisableDescription(typeof(RankingsStrings), nameof(RankingsStrings.TypeCountry))]
        Country,

        /// <summary>
        /// Countries by their players' Lazer Relax pp.
        /// </summary>
        [Description("country (relax)")]
        CountryRelax,
    }
}
