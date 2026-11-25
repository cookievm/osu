// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play.Leaderboards
{
    public partial class SoloGameplayLeaderboardProvider : CompositeDrawable, ISortableGameplayLeaderboardProvider
    {
        public IBindableList<GameplayLeaderboardScore> Scores => scores;
        private readonly BindableList<GameplayLeaderboardScore> scores = new BindableList<GameplayLeaderboardScore>();

        [Resolved]
        private LeaderboardManager? leaderboardManager { get; set; }

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        [Resolved]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        [Resolved]
        private Rulesets.Scoring.ScoreProcessor? scoreProcessor { get; set; }

        /// <summary>
        /// oily: sort by score or by o!ily pp, from the leaderboard's "Sort by" setting.
        /// </summary>
        public Bindable<GameplayLeaderboardSort> SortBy { get; } = new Bindable<GameplayLeaderboardSort>();

        private readonly CancellationTokenSource ppCancellation = new CancellationTokenSource();

        private readonly Cached sorting = new Cached();
        private bool isPartial;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var globalScores = leaderboardManager?.Scores.Value;

            isPartial = globalScores == null || globalScores.IsPartial;

            List<GameplayLeaderboardScore> newScores = new List<GameplayLeaderboardScore>();
            var toWorkOut = new List<(ScoreInfo, GameplayLeaderboardScore)>();

            if (globalScores != null)
            {
                foreach (var topScore in globalScores.AllScores.OrderByTotalScore())
                {
                    var score = new GameplayLeaderboardScore(topScore, false, GameplayLeaderboardScore.ComboDisplayMode.Highest);
                    newScores.Add(score);
                    score.PerformancePoints.Value = topScore.PP ?? 0;

                    // Lazer's pp is already o!ily pp, except for Relax and the mods that give none.
                    var ruleset = gameplayState?.Ruleset;
                    var mods = topScore.Mods;

                    if (topScore.PP == null || OilyPerformance.GivesNoPp(mods) || ruleset?.HasOilyPerformance(mods) == true)
                        toWorkOut.Add((topScore, score));
                }
            }

            if (gameplayState != null)
            {
                var localScore = new GameplayLeaderboardScore(gameplayState, tracked: true, GameplayLeaderboardScore.ComboDisplayMode.Highest)
                {
                    // Local score should always show lower than any existing scores in cases of ties.
                    TotalScoreTiebreaker = long.MaxValue
                };
                localScore.TotalScore.BindValueChanged(_ => sorting.Invalidate());
                localScore.Mods = gameplayState.Mods.Where(m => m.UserPlayable).ToArray();
                newScores.Add(localScore);

                // oily: the player's o!ily pp so far, for sorting by pp (the pp counter needs the play's score processor).
                if (scoreProcessor != null)
                {
                    var pp = new LeaderboardPerformancePoints();
                    pp.Current.BindValueChanged(v =>
                    {
                        localScore.PerformancePoints.Value = v.NewValue;

                        if (SortBy.Value == GameplayLeaderboardSort.PerformancePoints)
                            sorting.Invalidate();
                    });
                    AddInternal(pp);
                }
            }

            scores.AddRange(newScores);
            workOutPerformance(toWorkOut);

            SortBy.BindValueChanged(_ =>
            {
                sorting.Invalidate();
                sort();
            });

            sort();
            Scheduler.AddDelayed(sort, 1000, true);
        }

        /// <summary>
        /// oily: each score's o!ily pp (scores from the leaderboard often have none, or have official pp), one after another in the background.
        /// </summary>
        private void workOutPerformance(List<(ScoreInfo Info, GameplayLeaderboardScore Score)> toWorkOut)
        {
            var working = workingBeatmap?.Value;
            var ruleset = gameplayState?.Ruleset;

            if (working == null || ruleset == null || toWorkOut.Count == 0)
                return;

            var token = ppCancellation.Token;

            Task.Run(() =>
            {
                var attributesCache = new Dictionary<string, DifficultyAttributes>();

                foreach (var (info, score) in toWorkOut)
                {
                    if (token.IsCancellationRequested)
                        return;

                    try
                    {
                        double? pp = OilyPerformance.CalculateForScore(ruleset, working, info, attributesCache, token);

                        if (pp != null)
                        {
                            Schedule(() =>
                            {
                                score.PerformancePoints.Value = pp.Value;
                                sorting.Invalidate();
                            });
                        }
                    }
                    catch (Exception)
                    {
                        // Keep the score's own pp.
                    }
                }
            }, token);
        }

        protected override void Dispose(bool isDisposing)
        {
            ppCancellation.Cancel();
            base.Dispose(isDisposing);
        }

        // logic shared with PlaylistsGameplayLeaderboardProvider
        private void sort()
        {
            if (sorting.IsValid)
                return;

            bool byPp = SortBy.Value == GameplayLeaderboardSort.PerformancePoints;

            var orderedByScore = scores
                                 .OrderByDescending(i => byPp ? i.PerformancePoints.Value : i.TotalScore.Value)
                                 .ThenBy(i => i.TotalScoreTiebreaker)
                                 .ToList();

            foreach (var s in orderedByScore)
                s.ShowPerformancePoints.Value = byPp;

            int delta = 0;

            for (int i = 0; i < orderedByScore.Count; i++)
            {
                var score = orderedByScore[i];

                // see `SoloResultsScreen.FetchScores()` for another place that does the same thing with slight deviations
                // if this code is changed, that code should probably be changed as well

                score.DisplayOrder.Value = i + 1;

                // if we know we have all scores there can ever be, we can do the simple and obvious thing.
                if (!isPartial || byPp)
                    score.Position.Value = i + 1;
                else
                {
                    // we have a partial leaderboard, with potential gaps.
                    // we have initial score positions which were valid at the point of starting play.
                    // the assumption here is that non-tracked scores here cannot move around, only tracked ones can.
                    if (score.Tracked)
                    {
                        int? previousScorePosition = i > 0 ? orderedByScore[i - 1].InitialPosition : 0;
                        int? nextScorePosition = i < orderedByScore.Count - 1 ? orderedByScore[i + 1].InitialPosition : null;

                        // if the tracked score is perfectly between two scores which have known neighbouring initial positions,
                        // we can assign it the position of the previous score plus one...
                        if (previousScorePosition != null && nextScorePosition != null && previousScorePosition + 1 == nextScorePosition)
                        {
                            score.Position.Value = previousScorePosition + 1;
                            // but we also need to ensure all subsequent scores get shifted down one position, too.
                            delta++;
                        }
                        // conversely, if the tracked score is not between neighbouring two scores and the leaderboard is partial,
                        // we can't really assign a valid position at all. it could be any number between the two neighbours.
                        else
                            score.Position.Value = null;
                    }
                    // for non-tracked scores, we just need to apply any delta that might have come from the tracked scores
                    // which might have been encountered and assigned a position earlier.
                    else
                        score.Position.Value = score.InitialPosition + delta;
                }
            }

            sorting.Validate();
        }
    }
}
