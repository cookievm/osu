// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    /// <summary>
    /// oily: the colour and opacity of each panel, one by one (see <see cref="OilyPanelColours"/>).
    /// </summary>
    public partial class PanelColourSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Colours";

        private readonly Bindable<string> setting = new Bindable<string>();
        private readonly List<Action> resets = new List<Action>();

        private OilyPanelColours colours = new OilyPanelColours();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.OilyPanelColours, setting);
            colours = OilyPanelColours.Parse(setting.Value);

            var children = new List<Drawable>();

            foreach (var panel in Enum.GetValues<OilyPanel>().Where(OilyPanelColours.IsEditable))
            {
                var style = colours.Get(panel);
                colours.Panels[panel] = style;

                string name = panel.GetDescription();
                var custom = new BindableBool(style.Colour != null);
                var colour = new Bindable<Colour4>(style.CustomColour ?? Colour4.FromHex("#2a2a3a"));
                var opacity = new BindableFloat(style.Opacity) { MinValue = 0, MaxValue = 1, Precision = 0.01f };

                custom.BindValueChanged(c =>
                {
                    style.Colour = c.NewValue ? colour.Value.ToHex() : null;
                    save();
                });
                colour.BindValueChanged(c =>
                {
                    if (!custom.Value)
                        custom.Value = true;

                    style.Colour = c.NewValue.ToHex();
                    save();
                });
                opacity.BindValueChanged(o =>
                {
                    style.Opacity = o.NewValue;
                    save();
                });

                resets.Add(() =>
                {
                    custom.Value = false;
                    opacity.Value = 1;
                });

                children.Add(new SettingsItemV2(new FormCheckBox
                {
                    Caption = $"{name}: own colour",
                    HintText = "Off keeps lazer's colour.",
                    Current = custom,
                })
                {
                    Keywords = new[] { "colour", "color", "panel", "background", "transparency", name },
                });
                children.Add(new SettingsColour
                {
                    LabelText = $"{name} colour",
                    Current = colour,
                    Keywords = new[] { "colour", "color", "panel", name },
                });
                children.Add(new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = $"{name} opacity",
                    HintText = "Lower is more see-through.",
                    Current = opacity,
                    KeyboardStep = 0.05f,
                    DisplayAsPercentage = true,
                })
                {
                    Keywords = new[] { "transparency", "opacity", "alpha", "panel", name },
                });
            }

            // oily: single buttons and icons.
            var defaults = new Dictionary<OilyColour, Colour4>
            {
                [OilyColour.BackButton] = Colour4.FromHex("#DE31AE"),
                [OilyColour.ModsIcon] = Colour4.FromHex("#B2FF66"),
                [OilyColour.ModsBar] = Colour4.FromHex("#B2FF66"),
                [OilyColour.RandomIcon] = Colour4.FromHex("#66CCFF"),
                [OilyColour.RandomBar] = Colour4.FromHex("#66CCFF"),
                [OilyColour.OptionsIcon] = Colour4.FromHex("#B266FF"),
                [OilyColour.OptionsBar] = Colour4.FromHex("#B266FF"),
                [OilyColour.PpSimulatorIcon] = Colour4.FromHex("#FFCC22"),
                [OilyColour.PpSimulatorBar] = Colour4.FromHex("#FFCC22"),
                [OilyColour.ToolbarIcons] = Colour4.White,
                [OilyColour.GamemodeSelected] = Colour4.FromHex("#00FFAA"),
                [OilyColour.GamemodeOthers] = Colour4.White,
            };

            foreach (var item in Enum.GetValues<OilyColour>())
            {
                string name = item.GetDescription();
                var custom = new BindableBool(colours.Colours.ContainsKey(item));
                var colour = new Bindable<Colour4>(colours.GetColour(item) ?? defaults[item]);

                custom.BindValueChanged(c =>
                {
                    if (c.NewValue)
                        colours.Colours[item] = colour.Value.ToHex();
                    else
                        colours.Colours.Remove(item);

                    save();
                });
                colour.BindValueChanged(c =>
                {
                    colours.Colours[item] = c.NewValue.ToHex();

                    if (!custom.Value)
                        custom.Value = true;
                    else
                        save();
                });

                resets.Add(() =>
                {
                    custom.Value = false;
                    colour.Value = defaults[item];
                    custom.Value = false;
                });

                children.Add(new SettingsItemV2(new FormCheckBox
                {
                    Caption = $"{name}: own colour",
                    HintText = "Off keeps lazer's colour.",
                    Current = custom,
                })
                {
                    Keywords = new[] { "colour", "color", "icon", "button", name },
                });
                children.Add(new SettingsColour
                {
                    LabelText = $"{name} colour",
                    Current = colour,
                    Keywords = new[] { "colour", "color", "icon", "button", name },
                });
            }

            children.Add(new SettingsButtonV2
            {
                Text = "Reset all colours",
                Action = () =>
                {
                    foreach (var reset in resets)
                        reset();

                    save();
                },
            });

            Children = children;
        }

        private void save() => setting.Value = colours.Serialise();
    }
}
