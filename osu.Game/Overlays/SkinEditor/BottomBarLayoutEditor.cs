// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Footer;
using osuTK;

namespace osu.Game.Overlays.SkinEditor
{
    /// <summary>
    /// oily: the song select bottom bar: shown, buttons only or hidden, which side the back button is on,
    /// how big it is, and which buttons it has in what order (see <see cref="FooterLayout"/>).
    /// Shown in the skin editor's "Toolbars" tab, next to the toolbars.
    /// </summary>
    public partial class BottomBarLayoutEditor : FillFlowContainer
    {

        private readonly Bindable<string> layoutSetting = new Bindable<string>();
        private readonly Bindable<FooterBarMode> barMode = new Bindable<FooterBarMode>();
        private readonly Bindable<FooterBackSide> backSide = new Bindable<FooterBackSide>();

        private readonly BindableFloat size = new BindableFloat(1)
        {
            MinValue = FooterLayout.MIN_SIZE,
            MaxValue = FooterLayout.MAX_SIZE,
            Precision = 0.05f,
        };

        private FooterLayout layout = FooterLayout.CreateDefault();
        private FillFlowContainer buttonsFlow = null!;

        public BottomBarLayoutEditor()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.OilyFooterLayout, layoutSetting);
            layout = FooterLayout.Parse(layoutSetting.Value);
            barMode.Value = layout.Bar;
            backSide.Value = layout.BackButton;
            size.Value = layout.Size;

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<FooterBarMode>
                {
                    Caption = "Bottom bar",
                    HintText = "Drag the Mods, Random, Options and PP Simulator buttons on the screen to move them. When hidden, Escape still goes back, and F1 to F3 open mods, random and options only if their buttons are shown.",
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
                new PanelOpacitySlider(OilyPanel.BottomBar, "Bottom bar background",
                    "How see-through the bottom bar's background is. 0% makes it fully transparent, so only the buttons show."),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = "Bottom bar size",
                    HintText = "Makes the bar and all its buttons (back, mods, random and so on) bigger or smaller.",
                    Current = size,
                    DisplayAsPercentage = true,
                })
                {
                    Keywords = new[] { "footer", "bottom bar", "size", "scale", "resize" },
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
                    Text = "Put dragged buttons back in place",
                    Action = () =>
                    {
                        foreach (var button in layout.Buttons)
                            button.OffsetX = button.OffsetY = 0;

                        layout.BackOffsetX = layout.BackOffsetY = 0;
                        layout.ToolbarButtonsOffsetX = layout.ToolbarButtonsOffsetY = 0;

                        save();
                    },
                },
                new SettingsButtonV2
                {
                    Text = "Reset bottom bar",
                    Action = () =>
                    {
                        layout = FooterLayout.CreateDefault();
                        barMode.Value = layout.Bar;
                        backSide.Value = layout.BackButton;
                        size.Value = layout.Size;
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

            size.BindValueChanged(s =>
            {
                layout.Size = s.NewValue;
                save();
            });

            refresh();

            // Buttons dragged on screen save the layout themselves; pick that up so it isn't overwritten.
            layoutSetting.BindValueChanged(v =>
            {
                if (v.NewValue == lastSaved)
                    return;

                layout = FooterLayout.Parse(v.NewValue);
                lastSaved = v.NewValue;
                updatingFromSetting = true;
                barMode.Value = layout.Bar;
                backSide.Value = layout.BackButton;
                size.Value = layout.Size;
                updatingFromSetting = false;
                refresh();
            });
        }

        private string? lastSaved;
        private bool updatingFromSetting;

        private void save()
        {
            if (updatingFromSetting)
                return;

            lastSaved = layout.Serialise();
            layoutSetting.Value = lastSaved;
        }

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
            public ButtonRow(BottomBarLayoutEditor settings, FooterButtonLayout button)
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
