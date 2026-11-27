// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Screens.Select;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.General;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.CursorRotation,
                    Current = config.GetBindable<bool>(OsuSetting.CursorRotation)
                })
                {
                    Keywords = [@"spin"],
                },
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = UserInterfaceStrings.MenuCursorSize,
                    Current = config.GetBindable<float>(OsuSetting.MenuCursorSize),
                    KeyboardStep = 0.01f,
                    LabelFormat = v => $"{v:0.##}x"
                }),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = UserInterfaceStrings.Parallax,
                    Current = config.GetBindable<float>(OsuSetting.MenuParallaxScale),
                    DisplayAsPercentage = true,
                    LabelFormat = v => v == 0 ? CommonStrings.Disabled : FormSliderBar<float>.DefaultLabelFormat(v, true),
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = UserInterfaceStrings.HoldToConfirmActivationTime,
                    Current = config.GetBindable<double>(OsuSetting.UIHoldActivationDelay),
                    KeyboardStep = 50,
                    LabelFormat = v => $"{v:N0} ms",
                })
                {
                    Keywords = [@"delay"],
                    ApplyClassicDefault = c => ((IHasCurrentValue<double>)c).Current.Value = 0,
                },
                // oily (ilo, 2026-11-27): lazer's song select, or osu!stable's read from the skin.
                new SettingsItemV2(new FormEnumDropdown<OilyMenuUi>
                {
                    Caption = "Menu UI",
                    HintText = "Lazer: song select as lazer draws it, and every part can be moved and styled in the skin editor. "
                               + "Stable: song select as osu!stable draws it from your skin, and its parts stay in place so a skin can't break them. "
                               + "The beatmap cards and the back button keep their own Lazer/Stable style either way.",
                    Current = config.GetBindable<OilyMenuUi>(OsuSetting.OilyMenuUi),
                })
                {
                    Keywords = [@"stable", @"lazer", @"skin", @"song select", @"menu"],
                },
            };
        }
    }
}
