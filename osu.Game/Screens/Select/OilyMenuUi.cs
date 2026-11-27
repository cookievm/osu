// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily (ilo, 2026-11-27): how song select looks: lazer's UI (every part can be moved and styled in the skin editor),
    /// or osu!stable's, read from the skin and pinned in place so a skin can't break it.
    /// </summary>
    public enum OilyMenuUi
    {
        [Description("Lazer")]
        Lazer,

        [Description("Stable")]
        Stable,
    }
}
