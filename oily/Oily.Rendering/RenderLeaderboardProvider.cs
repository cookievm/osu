// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;

namespace Oily.Rendering
{
    /// <summary>
    /// One score on a render's leaderboard, from the companion app (the engine can't reach osu! itself).
    /// </summary>
    public record RenderLeaderboardEntry
    {
        public string Username { get; init; } = string.Empty;
        public int UserId { get; init; }
        public long TotalScore { get; init; }

        /// <summary>Null on maps that give no pp.</summary>
        public double? Pp { get; init; }

        /// <summary>0 to 1.</summary>
        public double Accuracy { get; init; }

        public int MaxCombo { get; init; }

        /// <summary>Mod acronyms, like "HD" and "DT". Null for none.</summary>
        public string[]? Mods { get; init; }
    }

    /// <summary>
    /// The leaderboard in renders: the scores the companion app passed in, plus the replay's player climbing it live,
    /// sorted by score or by pp as the skin's leaderboard asks.
    /// </summary>
    public partial class RenderLeaderboardProvider : CompositeDrawable, ISortableGameplayLeaderboardProvider
    {
        /// <summary>
        /// How many scores the companion app sends: the top of the map's leaderboard. A player below all of them has no known position.
        /// </summary>
        public const int MAX_ENTRIES = 50;

        public IBindableList<GameplayLeaderboardScore> Scores => scores;
        private readonly BindableList<GameplayLeaderboardScore> scores = new BindableList<GameplayLeaderboardScore>();

        public Bindable<GameplayLeaderboardSort> SortBy { get; } = new Bindable<GameplayLeaderboardSort>();

        /// <summary>The other scores. Set before the provider loads.</summary>
        public IReadOnlyList<RenderLeaderboardEntry> Entries { get; set; } = Array.Empty<RenderLeaderboardEntry>();

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        private GameplayLeaderboardScore? tracked;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var newScores = Entries.Select(e =>
            {
                var score = new GameplayLeaderboardScore(new ScoreInfo
                {
                    User = new APIUser { Id = e.UserId, Username = e.Username },
                    TotalScore = e.TotalScore,
                    Accuracy = e.Accuracy,
                    MaxCombo = e.MaxCombo,
                }, false, GameplayLeaderboardScore.ComboDisplayMode.Highest);

                score.PerformancePoints.Value = e.Pp ?? 0;
                score.Mods = modsFor(e);
                return score;
            }).ToList();

            if (gameplayState != null)
            {
                tracked = new GameplayLeaderboardScore(gameplayState, tracked: true, GameplayLeaderboardScore.ComboDisplayMode.Highest)
                {
                    Mods = gameplayState.Mods.Where(m => m.UserPlayable).ToArray(),
                };
                newScores.Add(tracked);

                // The player's pp so far, worked out the same way as the HUD's pp counter.
                var pp = new LivePerformancePoints();
                pp.Current.BindValueChanged(v => tracked.PerformancePoints.Value = v.NewValue);
                AddInternal(pp);
            }

            scores.AddRange(newScores);

            SortBy.BindValueChanged(_ => Sort(), true);
            Scheduler.AddDelayed(Sort, 1000, true);
        }

        private IReadOnlyList<Mod> modsFor(RenderLeaderboardEntry entry)
        {
            if (entry.Mods == null || gameplayState == null)
                return Array.Empty<Mod>();

            var ruleset = gameplayState.Ruleset;
            return entry.Mods.Select(a => ruleset.CreateModFromAcronym(a)).OfType<Mod>().ToArray();
        }

        /// <summary>
        /// Puts the scores in order, best first. Ties go to the other score, as on osu!'s own leaderboards.
        /// </summary>
        public void Sort()
        {
            bool byPp = SortBy.Value == GameplayLeaderboardSort.PerformancePoints;

            var ordered = scores.OrderByDescending(s => byPp ? s.PerformancePoints.Value : s.TotalScore.Value)
                                .ThenBy(s => s.Tracked)
                                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                var score = ordered[i];

                score.ShowPerformancePoints.Value = byPp;
                score.DisplayOrder.Value = i + 1;
                score.Position.Value = score.Tracked && i == ordered.Count - 1 && Entries.Count >= MAX_ENTRIES ? null : i + 1;
            }
        }

        private partial class LivePerformancePoints : PerformancePointsCounter
        {
            public LivePerformancePoints()
            {
                // Never seen, but it has to keep updating.
                Alpha = 0;
                AlwaysPresent = true;
            }
        }
    }
}
