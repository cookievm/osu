// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// oily: the rendered player's global osu! rank, which renders get from the companion app because the game can't look it up.
    /// Null outside of renders that have one.
    /// </summary>
    public static class PlayerGlobalRank
    {
        public static int? Current { get; set; }
    }
}
