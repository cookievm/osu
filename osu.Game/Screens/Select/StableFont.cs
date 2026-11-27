// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily (ilo, 2026-11-27): osu!stable's font (Aller), and the text sizes stable's song select uses (in stable's 768 high space,
    /// measured from stable at 2560x1440).
    /// </summary>
    public static class StableFont
    {
        public const string FAMILY = "Aller";

        public const float TITLE_SIZE = 23;
        public const float MAPPER_SIZE = 16;
        public const float LENGTH_SIZE = 15.3f;
        public const float COUNTS_SIZE = 14.2f;
        public const float DIFFICULTY_SIZE = 9.8f;
        public const float RANKING_SIZE = 18;
        public const float DROPDOWN_SIZE = 17;
        public const float SEARCH_SIZE = 16;
        public const float NO_RECORDS_SIZE = 26;

        // Baselines (where the letters sit) in stable's space, measured from stable.
        public const float TITLE_BASELINE = 17.07f;
        public const float MAPPER_BASELINE = 33.6f;
        public const float LENGTH_BASELINE = 52.8f;
        public const float COUNTS_BASELINE = 72.0f;
        public const float DIFFICULTY_BASELINE = 85.87f;
        public const float SEARCH_BASELINE = 101.87f;

        /// <summary>
        /// Whether o!ily was built with Aller (osu.Game/Resources/Oily/Fonts/Aller, made with oily/tools/make_bmfont.py). Without it, lazer's font stands in.
        /// </summary>
        public static readonly bool AVAILABLE = typeof(StableFont).Assembly.GetManifestResourceNames().Any(n => n.EndsWith("Aller-Regular.bin", StringComparison.Ordinal));

        public static FontUsage Get(float size, bool bold = false, bool light = false)
            => AVAILABLE
                ? new FontUsage(FAMILY, size, bold ? "Bold" : light ? "Light" : "Regular")
                : OsuFont.GetFont(size: size, weight: bold ? FontWeight.Bold : light ? FontWeight.Light : FontWeight.Regular);
    }

    /// <summary>
    /// oily: text placed by its baseline (where the letters sit), so lines land where stable's do whatever the font.
    /// </summary>
    public partial class StableBaselineText : OsuSpriteText
    {
        /// <summary>
        /// Where the baseline goes, in the parent's space.
        /// </summary>
        public float Baseline { get; set; }

        public StableBaselineText()
        {
            Shadow = false;
        }

        protected override void Update()
        {
            base.Update();
            Y = Baseline - LineBaseHeight;
        }
    }
}
