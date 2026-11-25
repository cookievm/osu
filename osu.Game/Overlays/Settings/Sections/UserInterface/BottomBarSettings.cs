// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Footer;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    /// <summary>
    /// oily: the song select bottom bar: shown, buttons only or hidden, which side the back button is on,
    /// and which buttons it has in what order (see <see cref="FooterLayout"/>).
    /// </summary>
    public partial class BottomBarSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Song select bottom bar";

        private readonly Bindable<string> layoutSetting = new Bindable<string>();
        private readonly Bindable<FooterBarMode> barMode = new Bindable<FooterBarMode>();
        private readonly Bindable<FooterBackSide> backSide = new Bindable<FooterBackSide>();

        private FooterLayout layout = FooterLayout.CreateDefault();
        private FillFlowContainer buttonsFlow = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.OilyFooterLayout, layoutSetting);
            layout = FooterLayout.Parse(layoutSetting.Value);
            barMode.Value = layout.Bar;
            backSide.Value = layout.BackButton;

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<FooterBarMode>
                {
                    Caption = "Bottom bar",
                    HintText = "When hidden, Escape still goes back, and F1 to F3 open mods, random and options only if their buttons are shown.",
                    Current = barMode,
                })
                {
                    Keywords = new[] { "footer", "bottom bar", "back button", "mods button" },
                },
                new SettingsItemV2(new FormEnumDropdown<FooterBackSide>
                {
                    Caption = "Back button side",
                    Current = backSide,
                })
                {
                    Keywords = new[] { "footer", "back" },
                },
                buttonsFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                },
                new SettingsButtonV2
                {
                    Text = "Reset bottom bar",
                    Action = () =>
                    {
                        layout = FooterLayout.CreateDefault();
                        barMode.Value = layout.Bar;
                        backSide.Value = layout.BackButton;
                        save();
                        refresh();
                    },
                },
            };

            barMode.BindValueChanged(m =>
            {
                layout.Bar = m.NewValue;
                save();
            });

            backSide.BindValueChanged(s =>
            {
                layout.BackButton = s.NewValue;
                save();
            });

            refresh();
        }

        private void save() => layoutSetting.Value = layout.Serialise();

        private void refresh()
        {
            buttonsFlow.Clear();

            foreach (var button in layout.Buttons)
                buttonsFlow.Add(new ButtonRow(this, button));
        }

        private void move(FooterButtonLayout button, int direction)
        {
            int index = layout.Buttons.IndexOf(button);
            int other = index + direction;

            if (index < 0 || other < 0 || other >= layout.Buttons.Count)
                return;

            (layout.Buttons[index], layout.Buttons[other]) = (layout.Buttons[other], layout.Buttons[index]);
            save();
            refresh();
        }

        private partial class ButtonRow : CompositeDrawable
        {
            public ButtonRow(BottomBarSettings settings, FooterButtonLayout button)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                var visible = new BindableBool(button.Visible);
                visible.BindValueChanged(v =>
                {
                    button.Visible = v.NewValue;
                    settings.save();
                });

                InternalChild = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 40),
                        new Dimension(GridSizeMode.Absolute, 40),
                    },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new SettingsItemV2(new FormCheckBox
                            {
                                Caption = $"Show {button.Item.GetDescription()} button",
                                HintText = "Button changes show the next time you open song select.",
                                Current = visible,
                            })
                            {
                                Keywords = new[] { "footer", "bottom bar", "button", "move" },
                            },
                            new IconButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.ChevronUp,
                                TooltipText = "Move left",
                                Action = () => settings.move(button, -1),
                            },
                            new IconButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.ChevronDown,
                                TooltipText = "Move right",
                                Action = () => settings.move(button, 1),
                            },
                        }
                    }
                };
            }
        }
    }
}
