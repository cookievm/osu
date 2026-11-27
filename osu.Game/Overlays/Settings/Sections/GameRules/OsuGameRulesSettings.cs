// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.Settings.Sections.GameRules
{
    /// <summary>
    /// oily: rules for osu! standard gameplay.
    /// </summary>
    public partial class OsuGameRulesSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "osu!";

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Notelock",
                    HintText = "Like stable: you can't hit a circle until the ones before it are hit or missed. Turn off for lazer's notelock.",
                    Current = config.GetBindable<bool>(OsuSetting.GameRulesNoteLock),
                })
                {
                    Keywords = new[] { "note lock", "stable", "classic" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Slider accuracy",
                    HintText = "Like lazer: hitting a slider's start early or late lowers accuracy. Turn off to judge sliders like stable.",
                    Current = config.GetBindable<bool>(OsuSetting.GameRulesSliderAccuracy),
                })
                {
                    Keywords = new[] { "slider head", "stable", "classic" },
                },
                new SettingsItemV2(new FormEnumDropdown<OilyHitTiming>
                {
                    Caption = "Hit timing",
                    HintText = "Lazer judges hits to fractions of a millisecond. Stable judges them in whole milliseconds, like stable's clock. The hit windows are the same.",
                    Current = config.GetBindable<OilyHitTiming>(OsuSetting.GameRulesHitTiming),
                })
                {
                    Keywords = new[] { "timing", "hit window", "stable", "lazer", "accuracy" },
                },
            };
        }
    }
}
