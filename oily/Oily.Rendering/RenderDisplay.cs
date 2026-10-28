// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Configuration;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Osu.Configuration;

namespace Oily.Rendering
{
    /// <summary>
    /// How the gameplay looks in the video: lazer's own display settings, set for the render.
    /// </summary>
    public record RenderDisplay
    {
        /// <summary>How dark the background is, 0 (not at all) to 1 (black).</summary>
        public double BackgroundDim { get; init; } = 0.7;

        /// <summary>How blurred the background is, 0 to 1.</summary>
        public double BackgroundBlur { get; init; }

        /// <summary>Whether the beatmap's storyboard and video play.</summary>
        public bool Storyboard { get; init; } = true;

        /// <summary>Whether the interface (score, combo, health bar, PP counter...) shows.</summary>
        public bool Interface { get; init; } = true;

        /// <summary>Whether hit circles light up when hit.</summary>
        public bool HitLighting { get; init; } = true;

        /// <summary>Whether the cursor leaves a trail, in osu! plays.</summary>
        public bool CursorTrail { get; init; } = true;

        /// <summary>Whether the beatmap's own hitsounds play, rather than only the skin's.</summary>
        public bool BeatmapHitsounds { get; init; } = true;

        /// <summary>The cursor's size, 0.1 to 2 times the skin's.</summary>
        public float CursorSize { get; init; } = 1;

        /// <summary>
        /// Sets these in the game's settings. Call before the player loads, as it reads them then.
        /// </summary>
        /// <param name="config">The game's settings.</param>
        /// <param name="rulesetConfig">The settings of the replay's ruleset, which hold the cursor trail for osu! plays.</param>
        public void Apply(OsuConfigManager config, IRulesetConfigManager? rulesetConfig)
        {
            config.SetValue(OsuSetting.DimLevel, BackgroundDim);
            config.SetValue(OsuSetting.BlurLevel, BackgroundBlur);
            config.SetValue(OsuSetting.ShowStoryboard, Storyboard);
            config.SetValue(OsuSetting.HUDVisibilityMode, Interface ? HUDVisibilityMode.Always : HUDVisibilityMode.Never);
            config.SetValue(OsuSetting.HitLighting, HitLighting);
            config.SetValue(OsuSetting.GameplayCursorSize, CursorSize);
            config.SetValue(OsuSetting.BeatmapHitsounds, BeatmapHitsounds);

            // A beatmap may ask for its own cursor size, which would win over the one picked here.
            config.SetValue(OsuSetting.AutoCursorSize, false);

            if (rulesetConfig is OsuRulesetConfigManager osuConfig)
                osuConfig.SetValue(OsuRulesetSetting.ShowCursorTrail, CursorTrail);
        }
    }
}
