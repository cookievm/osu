// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Database;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// oily: turns a play so far into the same play without any misses, for "pp if FC" counters.
    /// Every miss becomes the best hit, and the combo becomes the most the play could have had so far.
    /// </summary>
    public static class FullComboScore
    {
        public static void Apply(ScoreInfo score, ScoreProcessor scoreProcessor)
        {
            var statistics = score.Statistics;

            HitResult bestHit = score.MaximumStatistics.Where(s => s.Value > 0 && s.Key.IsBasic() && s.Key.IsHit())
                                     .Select(s => s.Key)
                                     .DefaultIfEmpty(HitResult.Great)
                                     .Max();

            moveCount(statistics, HitResult.Miss, bestHit);
            moveCount(statistics, HitResult.LargeTickMiss, HitResult.LargeTickHit);
            moveCount(statistics, HitResult.SmallTickMiss, HitResult.SmallTickHit);

            score.MaxCombo = score.MaximumStatistics.Where(s => s.Key.AffectsCombo() && s.Key.IsHit()).Sum(s => s.Value);
            score.Accuracy = StandardisedScoreMigrationTools.ComputeAccuracy(score, scoreProcessor);
        }

        private static void moveCount(Dictionary<HitResult, int> statistics, HitResult from, HitResult to)
        {
            if (!statistics.TryGetValue(from, out int count) || count == 0)
                return;

            statistics[from] = 0;
            statistics[to] = statistics.GetValueOrDefault(to) + count;
        }
    }
}
