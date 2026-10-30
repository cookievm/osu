// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// oily: the pp the play would be worth so far without any misses.
    /// </summary>
    public partial class ArgonIfFcPerformancePointsCounter : ArgonPerformancePointsCounter
    {
        protected override bool IfFullCombo => true;

        protected override LocalisableString Label => @"PP IF FC";
    }
}
