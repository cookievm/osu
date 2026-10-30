// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.Screens.Play.Leaderboards
{
    /// <summary>
    /// Provides a leaderboard to show during gameplay.
    /// </summary>
    public interface IGameplayLeaderboardProvider
    {
        /// <summary>
        /// List of all scores to display on the leaderboard.
        /// </summary>
        /// <remarks>
        /// Implementors should ensure that this list is only mutated from the update thread.
        /// </remarks>
        IBindableList<GameplayLeaderboardScore> Scores { get; }
    }

    /// <summary>
    /// oily: a leaderboard that can be sorted by something other than score, set by the leaderboard's "Sort by" setting.
    /// </summary>
    public interface ISortableGameplayLeaderboardProvider : IGameplayLeaderboardProvider
    {
        Bindable<GameplayLeaderboardSort> SortBy { get; }
    }

    /// <summary>
    /// oily: what a gameplay leaderboard is sorted by.
    /// </summary>
    public enum GameplayLeaderboardSort
    {
        Score,

        [System.ComponentModel.Description("pp")]
        PerformancePoints,
    }

    public class EmptyGameplayLeaderboardProvider : IGameplayLeaderboardProvider
    {
        public IBindableList<GameplayLeaderboardScore> Scores { get; } = new BindableList<GameplayLeaderboardScore>();
    }
}
