// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily (ilo, 2026-11-27): osu!stable's font (Aller), and the text sizes stable's song select uses (in stable's 768 high space,
    /// measured from stable at 2560x1440).
    /// </summary>
    public static class StableFont
    {
        public const string FAMILY = "Aller";

        public const float TITLE_SIZE = 25;
        public const float MAPPER_SIZE = 19;
        public const float LENGTH_SIZE = 19;
        public const float COUNTS_SIZE = 19;
        public const float DIFFICULTY_SIZE = 13;
        public const float RANKING_SIZE = 21;
        public const float DROPDOWN_SIZE = 19;
        public const float SEARCH_SIZE = 19;
        public const float NO_RECORDS_SIZE = 24;

        // Baselines (where the letters sit) in stable's space, measured from stable.
        public const float TITLE_BASELINE = 17.07f;
        public const float MAPPER_BASELINE = 33.6f;
        public const float LENGTH_BASELINE = 52.8f;
        public const float COUNTS_BASELINE = 72.0f;
        public const float DIFFICULTY_BASELINE = 85.87f;
        public const float SEARCH_BASELINE = 101.87f;

        /// <summary>
        /// How far the baseline is below the top of a line of text (the font's ascent), for each weight, as a part of the font size.
        /// </summary>
        private const float ascent_regular = 0.8f;

        private const float ascent_bold = 0.8f;

        public static float Ascent(float size, bool bold = false) => size * (bold ? ascent_bold : ascent_regular);

        public static FontUsage Get(float size, bool bold = false, bool light = false)
            => new FontUsage(FAMILY, size, bold ? "Bold" : light ? "Light" : "Regular");
    }
}
