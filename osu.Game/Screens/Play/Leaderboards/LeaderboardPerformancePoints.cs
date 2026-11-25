// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens.Play.HUD;

namespace osu.Game.Screens.Play.Leaderboards
{
    /// <summary>
    /// oily: the player's pp so far, worked out the same way as the HUD's pp counter, for the gameplay leaderboard. Never seen.
    /// </summary>
    public partial class LeaderboardPerformancePoints : PerformancePointsCounter
    {
        public LeaderboardPerformancePoints()
        {
            Alpha = 0;
            AlwaysPresent = true;
        }
    }
}
