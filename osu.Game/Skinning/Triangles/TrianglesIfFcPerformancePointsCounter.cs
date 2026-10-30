// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Skinning.Triangles
{
    /// <summary>
    /// oily: the pp the play would be worth so far without any misses.
    /// </summary>
    public partial class TrianglesIfFcPerformancePointsCounter : TrianglesPerformancePointsCounter
    {
        protected override bool IfFullCombo => true;

        protected override LocalisableString Suffix => @"pp if FC";
    }
}
