// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Skinning
{
    /// <summary>
    /// oily: the pp the play would be worth so far without any misses, in the skin's score font.
    /// </summary>
    public partial class LegacyIfFcPerformancePointsCounter : LegacyPerformancePointsCounter
    {
        protected override bool IfFullCombo => true;
    }
}
