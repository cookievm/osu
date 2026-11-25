// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
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
using osu.Game.Overlays.Toolbar;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    /// <summary>
    /// oily: how many toolbars there are, which edge of the screen each sits on, and where each button goes (see <see cref="ToolbarLayout"/>).
    /// </summary>
    public partial class ToolbarSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Toolbars";

        private readonly Bindable<string> layoutSetting = new Bindable<string>();
        private readonly BindableInt barCount = new BindableInt { MinValue = 0, MaxValue = ToolbarLayout.MAX_BARS };

        private ToolbarLayout layout = ToolbarLayout.CreateDefault();
        private FillFlowContainer barsFlow = null!;
        private FillFlowContainer buttonsFlow = null!;

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
        }

        private void save() => layoutSetting.Value = layout.Serialise();

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
                    HintText = "Left and right make it a sidebar.",
                    Current = edge,
                }));
            }

            buttonsFlow.Clear();

            foreach (var item in Enum.GetValues<ToolbarItem>())
                buttonsFlow.Add(new ButtonRow(this, item));
        }

        private IEnumerable<ToolbarPlace> places()
        {
            yield return ToolbarPlace.HIDDEN;

            for (int i = 0; i < layout.Bars.Count; i++)
            {
                foreach (var side in Enum.GetValues<ToolbarSide>())
                    yield return new ToolbarPlace(i, side);
            }
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
        /// Swaps a button with the one before (-1) or after (1) it on the same part of its toolbar.
        /// </summary>
        private void move(ToolbarItem item, int direction)
        {
            var button = getOrCreate(item);
            var neighbours = layout.Buttons.Where(b => b.Bar == button.Bar && b.Side == button.Side).OrderBy(b => b.Order).ToList();

            int index = neighbours.IndexOf(button);
            int other = index + direction;

            if (index < 0 || other < 0 || other >= neighbours.Count)
                return;

            // Give everything distinct orders first, then swap.
            for (int i = 0; i < neighbours.Count; i++)
                neighbours[i].Order = i;

            (neighbours[index].Order, neighbours[other].Order) = (neighbours[other].Order, neighbours[index].Order);

            save();
        }

        private partial class ButtonRow : CompositeDrawable
        {
            public ButtonRow(ToolbarSettings settings, ToolbarItem item)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                var button = settings.getOrCreate(item);
                var place = new Bindable<ToolbarPlace>(button.Bar >= 0 && button.Bar < settings.layout.Bars.Count ? new ToolbarPlace(button.Bar, button.Side) : ToolbarPlace.HIDDEN);

                place.BindValueChanged(p =>
                {
                    button.Bar = p.NewValue.Bar;
                    button.Side = p.NewValue.Side;
                    // New arrivals go at the end of their part of the bar.
                    button.Order = settings.layout.Buttons.Where(b => b != button && b.Bar == button.Bar && b.Side == button.Side).Select(b => b.Order).DefaultIfEmpty(-1).Max() + 1;
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
                            new SettingsItemV2(new FormDropdown<ToolbarPlace>
                            {
                                Caption = item.GetDescription(),
                                Items = settings.places().ToList(),
                                Current = place,
                            })
                            {
                                Keywords = new[] { "toolbar", "button", "move" },
                            },
                            new IconButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.ChevronUp,
                                TooltipText = "Move earlier (left, or up on a sidebar)",
                                Action = () => settings.move(item, -1),
                            },
                            new IconButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.ChevronDown,
                                TooltipText = "Move later (right, or down on a sidebar)",
                                Action = () => settings.move(item, 1),
                            },
                        }
                    }
                };
            }
        }

        /// <summary>
        /// Where a button goes: a part of one toolbar, or hidden.
        /// </summary>
        public record ToolbarPlace(int Bar, ToolbarSide Side)
        {
            public static readonly ToolbarPlace HIDDEN = new ToolbarPlace(-1, ToolbarSide.Start);

            public override string ToString() => Bar < 0 ? "Hidden" : $"Toolbar {Bar + 1}, {Side.ToString().ToLowerInvariant()}";
        }
    }
}
