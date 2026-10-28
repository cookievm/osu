// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Judgements;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// oily: a different pp system for <see cref="PerformancePointsCounter"/>, for example a private server's relax pp.
    /// </summary>
    public interface ILivePerformanceCalculator
    {
        /// <summary>
        /// The play's pp so far, after <paramref name="judgement"/>.
        /// </summary>
        /// <param name="score">The play so far.</param>
        /// <param name="judgement">The latest judgement.</param>
        double Calculate(ScoreInfo score, JudgementResult judgement);
    }

    /// <summary>
    /// oily: where <see cref="PerformancePointsCounter"/> looks for a different pp system. Null outside of renders that ask for one.
    /// </summary>
    public static class LivePerformanceOverride
    {
        /// <summary>
        /// Returns the calculator for a play, or null to use the ruleset's own.
        /// </summary>
        public static Func<GameplayState, ILivePerformanceCalculator?>? Factory { get; set; }
    }
}
