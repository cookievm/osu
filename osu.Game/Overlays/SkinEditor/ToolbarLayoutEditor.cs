// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Toolbar;
using osuTK;

namespace osu.Game.Overlays.SkinEditor
{
    /// <summary>
    /// oily: how many toolbars there are, which edge of the screen each sits on, and where each button goes (see <see cref="ToolbarLayout"/>).
    /// Shown in the skin editor's "Toolbars" tab.
    /// </summary>
    public partial class ToolbarLayoutEditor : FillFlowContainer
    {

        private readonly Bindable<string> layoutSetting = new Bindable<string>();
        private readonly BindableInt barCount = new BindableInt { MinValue = 0, MaxValue = ToolbarLayout.MAX_BARS };

        private ToolbarLayout layout = ToolbarLayout.CreateDefault();
        private FillFlowContainer barsFlow = null!;
        private FillFlowContainer buttonsFlow = null!;

        public ToolbarLayoutEditor()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.OilyToolbarLayout, layoutSetting);
            layout = ToolbarLayout.Parse(layoutSetting.Value);
            barCount.Value = layout.Bars.Count;

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = "Number of toolbars",
                    HintText = "0 hides the toolbar completely. Each toolbar can go on any edge of the screen.",
                    Current = barCount,
                    KeyboardStep = 1,
                })
                {
                    Keywords = new[] { "toolbar", "bar", "sidebar", "top bar" },
                },
                new PanelOpacitySlider(OilyPanel.Toolbar, "Toolbar background",
                    "How see-through the toolbars' background is. 0% makes it fully transparent, so only the buttons show."),
                barsFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
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
                    Text = "Reset toolbars",
                    Action = () =>
                    {
                        layout = ToolbarLayout.CreateDefault();
                        barCount.Value = layout.Bars.Count;
                        save();
                        refresh();
                    },
                },
            };

            barCount.BindValueChanged(c =>
            {
                while (layout.Bars.Count < c.NewValue)
                    layout.Bars.Add(new ToolbarBarLayout { Edge = layout.Bars.Count == 0 ? ToolbarEdge.Top : ToolbarEdge.Left });

                while (layout.Bars.Count > c.NewValue)
                    layout.Bars.RemoveAt(layout.Bars.Count - 1);

                save();
                refresh();
            });

            refresh();

            // Bars dragged on screen save the layout themselves; pick that up so it isn't overwritten.
            layoutSetting.BindValueChanged(v =>
            {
                if (v.NewValue == lastSaved)
                    return;

                layout = ToolbarLayout.Parse(v.NewValue);
                lastSaved = v.NewValue;
                updatingFromSetting = true;
                barCount.Value = layout.Bars.Count;
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
            barsFlow.Clear();

            for (int i = 0; i < layout.Bars.Count; i++)
            {
                var bar = layout.Bars[i];
                var edge = new Bindable<ToolbarEdge>(bar.Edge);
                edge.BindValueChanged(e =>
                {
                    bar.Edge = e.NewValue;
                    save();
                });

                barsFlow.Add(new SettingsItemV2(new FormEnumDropdown<ToolbarEdge>
                {
                    Caption = $"Toolbar {i + 1} position",
                    HintText = "Left and right make it a sidebar. You can also drag the toolbar anywhere on the screen, even past the edges.",
                    Current = edge,
                }));

                var length = new BindableFloat(bar.Length)
                {
                    MinValue = ToolbarBarLayout.MIN_LENGTH,
                    MaxValue = 1,
                    Precision = 0.01f,
                };
                length.BindValueChanged(l =>
                {
                    bar.Length = l.NewValue;
                    save();
                });

                barsFlow.Add(new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = $"Toolbar {i + 1} length",
                    HintText = "Crops the toolbar to part of its edge. Buttons that don't fit are cut off.",
                    Current = length,
                    DisplayAsPercentage = true,
                }));

                barsFlow.Add(new SettingsButtonV2
                {
                    Text = $"Put toolbar {i + 1} back on its edge",
                    Action = () =>
                    {
                        bar.OffsetX = 0;
                        bar.OffsetY = 0;
                        save();
                    },
                });
            }

            buttonsFlow.Clear();

            foreach (var item in Enum.GetValues<ToolbarItem>())
                buttonsFlow.Add(new ButtonRow(this, item));
        }

        private ToolbarButtonLayout getOrCreate(ToolbarItem item)
        {
            var button = layout.Buttons.FirstOrDefault(b => b.Item == item);

            if (button == null)
            {
                button = new ToolbarButtonLayout { Item = item, Bar = -1, Order = layout.Buttons.Count == 0 ? 0 : layout.Buttons.Max(b => b.Order) + 1 };
                layout.Buttons.Add(button);
            }

            return button;
        }

        /// <summary>
        /// Shows a hidden button again, in lazer's place for it (on the first toolbar), at the end of its part of the bar.
        /// </summary>
        private void show(ToolbarButtonLayout button)
        {
            var original = ToolbarLayout.CreateDefault().Buttons.FirstOrDefault(b => b.Item == button.Item);

            button.Bar = 0;
            button.Side = original?.Side ?? ToolbarSide.End;
            button.Order = layout.Buttons.Where(b => b != button && b.Bar == button.Bar && b.Side == button.Side).Select(b => b.Order).DefaultIfEmpty(-1).Max() + 1;
        }

        /// <summary>
        /// oily (ilo, 2026-11-26): each button is just its name with a tick box to show it, and where its popup slides in from.
        /// </summary>
        private partial class ButtonRow : CompositeDrawable
        {
            private static readonly ToolbarPopupDirection[] sides =
            {
                ToolbarPopupDirection.Left,
                ToolbarPopupDirection.Right,
                ToolbarPopupDirection.Top,
                ToolbarPopupDirection.Bottom,
            };

            public ButtonRow(ToolbarLayoutEditor settings, ToolbarItem item)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                var button = settings.getOrCreate(item);

                var shown = new BindableBool(button.Bar != -1);
                shown.BindValueChanged(v =>
                {
                    if (v.NewValue)
                        settings.show(button);
                    else
                        button.Bar = -1;

                    settings.save();
                });

                // Only the four sides (no "lazer's default"). A button that was never changed shows the side lazer uses for it.
                var popupFrom = new Bindable<ToolbarPopupDirection>(ToolbarPopupContainer.DirectionOf(button, item));
                popupFrom.BindValueChanged(d =>
                {
                    button.PopupFrom = d.NewValue;
                    settings.save();
                });

                var checkBox = new SettingsItemV2(new FormCheckBox
                {
                    Caption = item.GetDescription(),
                    Current = shown,
                })
                {
                    Keywords = new[] { "toolbar", "button", "show", "hide" },
                };

                if (!ToolbarLayout.HasPopup(item))
                {
                    InternalChild = checkBox;
                    return;
                }

                // oily (ilo, 2026-11-26): the button's name and tick box, with "Popup comes from" underneath it.
                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(4),
                    Children = new Drawable[]
                    {
                        checkBox,
                        new SettingsItemV2(new FormEnumDropdown<ToolbarPopupDirection>
                        {
                            Caption = "Popup comes from",
                            HintText = "Which side of the screen this button's popup slides in from. It lines up with the button you pressed.",
                            Items = sides,
                            Current = popupFrom,
                        })
                        {
                            Keywords = new[] { "toolbar", "popup", "direction", "slide" },
                        },
                    }
                };
            }
        }
    }
}
