// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Difficulty
{
    /// <summary>
    /// oily: o!ily's own pp, the same rules the o!ily server uses.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Mods that make maps easier, aim for you, change speed mid-map or replace the map give no pp (<see cref="NO_PP_MODS"/>).</item>
    /// <item>A ruleset can have its own pp for some mods (osu! with Relax uses Akatsuki's relax pp), see <see cref="Ruleset.HasOilyPerformance"/>.</item>
    /// <item>Everything else uses lazer's pp, which matches the server's normal pp for these plays.</item>
    /// </list>
    /// </remarks>
    public static class OilyPerformance
    {
        /// <summary>
        /// Mods that give no pp on o!ily (the score still counts): Difficulty Adjust, Magnetised, Adaptive Speed, Wind Up, Wind Down,
        /// Simplified Rhythm, Hold Off, Target Practice, Autoplay and Cinema.
        /// </summary>
        public static readonly IReadOnlySet<string> NO_PP_MODS = new HashSet<string> { "DA", "MG", "AS", "WU", "WD", "SR", "HO", "TP", "AT", "CN" };

        /// <summary>
        /// Whether these mods give no pp on o!ily.
        /// </summary>
        public static bool GivesNoPp(IEnumerable<IMod> mods) => mods.Any(m => NO_PP_MODS.Contains(m.Acronym));

        /// <summary>
        /// The o!ily pp of an SS on a beatmap, and of a pretend score.
        /// </summary>
        /// <param name="ruleset">The ruleset.</param>
        /// <param name="working">The beatmap.</param>
        /// <param name="mods">The mods.</param>
        /// <param name="simulation">The pretend score, or <c>null</c> to only work out the SS.</param>
        /// <param name="lazerAttributes">Lazer's difficulty, if already known (otherwise it is worked out when needed).</param>
        /// <param name="cancellationToken">Stops the work.</param>
        /// <returns>The pp of an SS and of the pretend score (<c>null</c> where the ruleset has no pp).</returns>
        public static (double? SS, double? Simulated) Calculate(Ruleset ruleset, WorkingBeatmap working, Mod[] mods, OilySimulation? simulation,
                                                                DifficultyAttributes? lazerAttributes = null, CancellationToken cancellationToken = default)
        {
            if (GivesNoPp(mods))
                return (0, simulation == null ? null : 0);

            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods, cancellationToken);

            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = mods;
            scoreProcessor.ApplyBeatmap(playable);
            cancellationToken.ThrowIfCancellationRequested();

            var perfect = new ScoreInfo(working.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = 1,
                Mods = mods,
                MaxCombo = scoreProcessor.MaximumCombo,
                Combo = scoreProcessor.MaximumCombo,
                TotalScore = scoreProcessor.MaximumTotalScore,
                Statistics = scoreProcessor.MaximumStatistics,
                MaximumStatistics = scoreProcessor.MaximumStatistics
            };

            var pretend = simulation == null ? null : CreateScore(working.BeatmapInfo, ruleset.RulesetInfo, mods, scoreProcessor, simulation);

            if (ruleset.HasOilyPerformance(mods))
            {
                double? ss = ruleset.CalculateOilyPerformance(playable, mods, null);
                double? simulated = pretend == null ? null : ruleset.CalculateOilyPerformance(playable, mods, playOf(pretend));
                return (ss, simulated);
            }

            var calculator = ruleset.CreatePerformanceCalculator();

            if (calculator == null)
                return (null, null);

            lazerAttributes ??= ruleset.CreateDifficultyCalculator(working).Calculate(mods, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return (calculator.Calculate(perfect, lazerAttributes).Total,
                pretend == null ? null : calculator.Calculate(pretend, lazerAttributes).Total);
        }

        /// <summary>
        /// A pretend score with the simulation's accuracy, misses and combo.
        /// </summary>
        public static ScoreInfo CreateScore(BeatmapInfo beatmap, RulesetInfo ruleset, Mod[] mods, ScoreProcessor scoreProcessor, OilySimulation simulation)
        {
            var maximum = scoreProcessor.MaximumStatistics;
            var statistics = new Dictionary<HitResult, int>(maximum);

            double accuracy = Math.Clamp(simulation.Accuracy / 100, 0, 1);
            int total = maximum.GetValueOrDefault(HitResult.Great);
            int misses = Math.Clamp(simulation.Misses, 0, total);
            int remaining = total - misses;

            if (ruleset.ShortName == "osu")
            {
                // osu! accuracy: (300 * greats + 100 * oks + 50 * mehs) / (300 * objects). Oks make up the missing accuracy.
                int oks = total == 0 ? 0 : (int)Math.Round((6.0 * remaining - 6.0 * accuracy * total) / 4);
                oks = Math.Clamp(oks, 0, remaining);

                statistics[HitResult.Great] = remaining - oks;
                statistics[HitResult.Ok] = oks;
                statistics[HitResult.Meh] = 0;
                statistics[HitResult.Miss] = misses;
            }
            else
            {
                statistics[HitResult.Great] = remaining;
                statistics[HitResult.Miss] = misses;
            }

            int maxCombo = scoreProcessor.MaximumCombo;
            int combo = (int)Math.Round(maxCombo * Math.Clamp(simulation.ComboPercent / 100, 0, 1));

            return new ScoreInfo(beatmap, ruleset)
            {
                Passed = true,
                Accuracy = accuracy,
                Mods = mods,
                MaxCombo = combo,
                Combo = combo,
                Statistics = statistics,
                MaximumStatistics = maximum,
            };
        }

        private static OilyPlay playOf(ScoreInfo score) => new OilyPlay(
            score.Statistics.GetValueOrDefault(HitResult.Great),
            score.Statistics.GetValueOrDefault(HitResult.Ok),
            score.Statistics.GetValueOrDefault(HitResult.Meh),
            score.Statistics.GetValueOrDefault(HitResult.Miss),
            score.MaxCombo);
    }

    /// <summary>
    /// oily: the hits of a play, for <see cref="OilyPerformance"/>.
    /// </summary>
    /// <param name="Count300">Greats.</param>
    /// <param name="Count100">Oks.</param>
    /// <param name="Count50">Mehs.</param>
    /// <param name="Misses">Misses.</param>
    /// <param name="MaxCombo">The play's best combo.</param>
    public record OilyPlay(int Count300, int Count100, int Count50, int Misses, int MaxCombo);

    /// <summary>
    /// oily: a pretend score, for simulated pp.
    /// </summary>
    /// <param name="Accuracy">Accuracy, in percent.</param>
    /// <param name="Misses">Misses.</param>
    /// <param name="ComboPercent">Best combo, in percent of the beatmap's max combo.</param>
    public record OilySimulation(double Accuracy, int Misses, double ComboPercent)
    {
        /// <summary>
        /// The simulation used for beatmaps without their own.
        /// </summary>
        public static readonly OilySimulation DEFAULT = new OilySimulation(98, 0, 100);

        /// <summary>
        /// Short text for cards, like "98.5% 2x".
        /// </summary>
        public string ShortText => $"{Accuracy:0.##}% {Misses}x";
    }
}
