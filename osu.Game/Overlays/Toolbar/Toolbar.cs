// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Configuration;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input.Events;
using osu.Game.Rulesets;
using osu.Framework.Input.Bindings;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;

namespace osu.Game.Overlays.Toolbar
{
    public partial class Toolbar : OverlayContainer, IKeyBindingHandler<GlobalAction>
    {
        public const float HEIGHT = 40;
        public const float TOOLTIP_HEIGHT = 30;

        /// <summary>
        /// Whether the user hid this <see cref="Toolbar"/> with <see cref="GlobalAction.ToggleToolbar"/>.
        /// In this state, automatic toggles should not occur, respecting the user's preference to have no toolbar.
        /// </summary>
        private bool hiddenByUser;

        public Action OnHome;

        private ToolbarUserButton userButton;

        private const double transition_time = 500;

        protected readonly IBindable<OverlayActivation> OverlayActivationMode = new Bindable<OverlayActivation>(OverlayActivation.All);

        // Toolbar and its components need keyboard input even when hidden.
        public override bool PropagateNonPositionalInputSubTree => OverlayActivationMode.Value != OverlayActivation.Disabled;

        // oily: the toolbar covers the whole screen (so its bars can sit on any edge), but only its bars take input.
        // Clicks on the bars stop there, so an open overlay doesn't close on mouse down and get reopened by the button's click.
        protected override bool BlockPositionalInput => true;

        public override bool ReceivePositionalInputAt(osuTK.Vector2 screenSpacePos) => strips.Any(s => s.ReceivePositionalInputAt(screenSpacePos));

        /// <summary>
        /// oily: the bars (see <see cref="ToolbarLayout"/>).
        /// </summary>
        private readonly List<ToolbarStrip> strips = new List<ToolbarStrip>();

        private Container stripContainer;

        private readonly Bindable<string> layoutSetting = new Bindable<string>();

        /// <summary>
        /// oily: the current layout of the bars and buttons.
        /// </summary>
        public ToolbarLayout Layout { get; private set; } = ToolbarLayout.CreateDefault();

        /// <summary>
        /// oily: how much of the screen the bars cover on each edge (less while they slide away). The game keeps screens and overlays out of it.
        /// </summary>
        public MarginPadding ScreenPadding { get; private set; }

        /// <summary>
        /// oily: how far the bars are shown, from 0 (hidden) to 1.
        /// </summary>
        private float reveal = 1;

        public Toolbar()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // this only needed to be set for the initial LoadComplete/Update, so layout completes and gets buttons in a state they can correctly handle keyboard input for hotkeys.
            AlwaysPresent = false;
        }

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load(OsuGame osuGame, OsuConfigManager config)
        {
            Child = stripContainer = new Container { RelativeSizeAxes = Axes.Both };

            if (config != null)
                config.BindWith(OsuSetting.OilyToolbarLayout, layoutSetting);

            rebuild();

            if (osuGame != null)
                OverlayActivationMode.BindTo(osuGame.OverlayActivationMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            layoutSetting.BindValueChanged(_ => Scheduler.AddOnce(rebuild));
        }

        /// <summary>
        /// oily: makes the bars again from the saved layout.
        /// </summary>
        private void rebuild()
        {
            Layout = ToolbarLayout.Parse(layoutSetting.Value);

            stripContainer.Clear();
            strips.Clear();
            userButton = null;

            for (int i = 0; i < Layout.Bars.Count; i++)
            {
                var strip = new ToolbarStrip(Layout.Bars[i].Edge, i);

                foreach (var side in new[] { ToolbarSide.Start, ToolbarSide.Middle, ToolbarSide.End })
                {
                    foreach (var item in Layout.ItemsOn(i, side))
                    {
                        var drawable = createItem(item, strip.Vertical);

                        if (drawable != null)
                            strip.Add(item, drawable, side);
                    }
                }

                strips.Add(strip);
                stripContainer.Add(strip);
            }
        }

        private Drawable createItem(ToolbarItem item, bool vertical)
        {
            switch (item)
            {
                case ToolbarItem.Settings:
                    return new ToolbarSettingsButton();

                case ToolbarItem.Home:
                    return new ToolbarHomeButton { Action = () => OnHome?.Invoke() };

                case ToolbarItem.Rulesets:
                    var selector = new ToolbarRulesetSelector(vertical);
                    selector.OnLoadComplete += _ => selector.Current.BindTo(ruleset);
                    return selector;

                case ToolbarItem.Rankings:
                    return new ToolbarRankingsButton();

                case ToolbarItem.BeatmapListing:
                    return new ToolbarBeatmapListingButton();

                case ToolbarItem.Chat:
                    return new ToolbarChatButton();

                case ToolbarItem.Social:
                    return new ToolbarSocialButton();

                case ToolbarItem.Music:
                    return new ToolbarMusicButton();

                case ToolbarItem.User:
                    return userButton = new ToolbarUserButton();

                case ToolbarItem.Clock:
                    return new ToolbarClock();

                case ToolbarItem.Notifications:
                    return new ToolbarNotificationButton();

                default:
                    return null;
            }
        }

        /// <summary>
        /// oily: the edge of the bar a button is on, and the button itself (or <c>null</c> if it's hidden).
        /// </summary>
        public (ToolbarEdge Edge, Drawable Button)? Find(ToolbarItem item)
        {
            foreach (var strip in strips)
            {
                if (strip.Items.TryGetValue(item, out var drawable))
                    return (strip.Edge, drawable);
            }

            return null;
        }

        protected override void Update()
        {
            base.Update();

            // oily: lays the bars out. Bars on the same edge stack outwards in; side bars fit between the top and bottom bars.
            float top = 0, bottom = 0, left = 0, right = 0;

            foreach (var strip in strips.Where(s => !s.Vertical))
            {
                float thickness = HEIGHT;
                float shown = thickness * reveal;

                strip.Width = DrawWidth;
                strip.Height = thickness;

                if (strip.Edge == ToolbarEdge.Top)
                {
                    strip.Anchor = strip.Origin = Anchor.TopLeft;
                    strip.Position = new osuTK.Vector2(0, top - thickness + shown);
                    top += shown;
                }
                else
                {
                    strip.Anchor = strip.Origin = Anchor.BottomLeft;
                    strip.Position = new osuTK.Vector2(0, -(bottom - thickness + shown));
                    bottom += shown;
                }
            }

            foreach (var strip in strips.Where(s => s.Vertical))
            {
                float thickness = strip.Thickness;
                float shown = thickness * reveal;

                strip.Height = Math.Max(0, DrawHeight - top - bottom);

                if (strip.Edge == ToolbarEdge.Left)
                {
                    strip.Anchor = strip.Origin = Anchor.TopLeft;
                    strip.Position = new osuTK.Vector2(left - thickness + shown, top);
                    left += shown;
                }
                else
                {
                    strip.Anchor = strip.Origin = Anchor.TopRight;
                    strip.Position = new osuTK.Vector2(-(right - thickness + shown), top);
                    right += shown;
                }
            }

            ScreenPadding = new MarginPadding { Top = top, Bottom = bottom, Left = left, Right = right };
        }

        /// <summary>
        /// oily: one bar, on one edge of the screen. Its buttons go at the start, the middle (which fills the rest) and the end.
        /// </summary>
        public partial class ToolbarStrip : Container
        {
            public readonly ToolbarEdge Edge;
            public readonly int Index;

            public bool Vertical => Edge is ToolbarEdge.Left or ToolbarEdge.Right;

            public readonly Dictionary<ToolbarItem, Drawable> Items = new Dictionary<ToolbarItem, Drawable>();

            /// <summary>
            /// How thick the bar is (its height, or its width on a side bar).
            /// </summary>
            public float Thickness => Vertical ? Math.Max(HEIGHT, content.DrawWidth) : HEIGHT;

            private readonly FillFlowContainer startFlow;
            private readonly FillFlowContainer middleFlow;
            private readonly FillFlowContainer endFlow;
            private readonly CompositeDrawable content;

            public ToolbarStrip(ToolbarEdge edge, int index)
            {
                Edge = edge;
                Index = index;

                ToolbarBackground background;
                HoverInterceptor interceptor;

                var direction = Vertical ? FillDirection.Vertical : FillDirection.Horizontal;

                startFlow = new FillFlowContainer { Direction = direction };
                middleFlow = new FillFlowContainer { Direction = direction };
                endFlow = new FillFlowContainer { Direction = direction };

                if (Vertical)
                {
                    foreach (var flow in new[] { startFlow, middleFlow, endFlow })
                        flow.AutoSizeAxes = Axes.Both;

                    startFlow.Anchor = startFlow.Origin = Anchor.TopCentre;
                    middleFlow.Anchor = middleFlow.Origin = Anchor.Centre;
                    endFlow.Anchor = endFlow.Origin = Anchor.BottomCentre;

                    InternalChildren = new Drawable[]
                    {
                        background = new ToolbarBackground(),
                        content = new Container
                        {
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Children = new Drawable[] { middleFlow, startFlow, endFlow }
                        },
                        interceptor = new HoverInterceptor { RelativeSizeAxes = Axes.Both },
                    };

                    Width = HEIGHT;
                }
                else
                {
                    foreach (var flow in new[] { startFlow, middleFlow, endFlow })
                    {
                        flow.RelativeSizeAxes = Axes.Y;
                        flow.AutoSizeAxes = Axes.X;
                    }

                    endFlow.Anchor = endFlow.Origin = Anchor.TopRight;

                    InternalChildren = new Drawable[]
                    {
                        background = new ToolbarBackground(),
                        content = new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize)
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new Container
                                    {
                                        Name = "Start buttons",
                                        RelativeSizeAxes = Axes.Y,
                                        AutoSizeAxes = Axes.X,
                                        Depth = float.MinValue,
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                Colour = OsuColour.Gray(0.1f),
                                                RelativeSizeAxes = Axes.Both,
                                            },
                                            startFlow,
                                        }
                                    },
                                    new Container
                                    {
                                        Name = "Middle",
                                        RelativeSizeAxes = Axes.Both,
                                        Children = new Drawable[]
                                        {
                                            new OsuScrollContainer(Direction.Horizontal)
                                            {
                                                ScrollbarVisible = false,
                                                RelativeSizeAxes = Axes.Both,
                                                Masking = false,
                                                Child = middleFlow,
                                            },
                                            new Box
                                            {
                                                Colour = ColourInfo.GradientHorizontal(OsuColour.Gray(0.1f).Opacity(0), OsuColour.Gray(0.1f)),
                                                Width = 50,
                                                RelativeSizeAxes = Axes.Y,
                                                Anchor = Anchor.TopRight,
                                                Origin = Anchor.TopRight,
                                            },
                                        }
                                    },
                                    new Container
                                    {
                                        Name = "End buttons",
                                        RelativeSizeAxes = Axes.Y,
                                        AutoSizeAxes = Axes.X,
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                Colour = OsuColour.Gray(0.1f),
                                                RelativeSizeAxes = Axes.Both,
                                            },
                                            endFlow,
                                        }
                                    },
                                },
                            }
                        },
                        interceptor = new HoverInterceptor { RelativeSizeAxes = Axes.Both },
                    };
                }

                ((IBindable<bool>)background.ShowGradient).BindTo(interceptor.ReceivedHover);
            }

            /// <summary>
            /// Adds a button to one side of the bar.
            /// </summary>
            public void Add(ToolbarItem item, Drawable drawable, ToolbarSide side)
            {
                Items[item] = drawable;

                var flow = side == ToolbarSide.Start ? startFlow : side == ToolbarSide.Middle ? middleFlow : endFlow;

                if (Vertical && drawable is not ToolbarRulesetSelector)
                {
                    // Buttons fill the bar's height, so on a side bar each one gets a square-height cell.
                    drawable.Anchor = drawable.Origin = Anchor.TopCentre;
                    flow.Add(new Container
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Height = HEIGHT,
                        AutoSizeAxes = Axes.X,
                        Child = drawable,
                    });
                }
                else
                {
                    if (Vertical)
                        drawable.Anchor = drawable.Origin = Anchor.TopCentre;

                    flow.Add(drawable);
                }
            }

            protected override void Update()
            {
                base.Update();

                if (Vertical)
                    Width = Thickness;
            }
        }

        public partial class ToolbarBackground : Container
        {
            public Bindable<bool> ShowGradient { get; } = new BindableBool();

            private readonly Box gradientBackground;
            private readonly Box solidBackground;
            private IBindable<string> panelSetting;

            public ToolbarBackground()
            {
                RelativeSizeAxes = Axes.Both;
                Children = new Drawable[]
                {
                    solidBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = OsuColour.Gray(0.1f),
                    },
                    gradientBackground = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.BottomLeft,
                        Alpha = 0,
                        Height = 80,
                        Colour = ColourInfo.GradientVertical(
                            OsuColour.Gray(0f).Opacity(0.7f), OsuColour.Gray(0).Opacity(0)),
                    },
                };
            }

            [BackgroundDependencyLoader(true)]
            private void load(OsuConfigManager config)
            {
                // oily: the toolbars' colour and opacity from settings.
                if (config != null)
                {
                    panelSetting = OilyPanelColours.Track(config, OilyPanel.Toolbar, style =>
                    {
                        solidBackground.Colour = style.CustomColour ?? OsuColour.Gray(0.1f);
                        solidBackground.Alpha = Math.Clamp(style.Opacity, 0, 1);
                    });
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                ShowGradient.BindValueChanged(_ => updateState(), true);
            }

            private void updateState()
            {
                if (ShowGradient.Value)
                    gradientBackground.FadeIn(2500, Easing.OutQuint);
                else
                    gradientBackground.FadeOut(200, Easing.OutQuint);
            }
        }

        /// <summary>
        /// Whenever the mouse cursor is within the bounds of the toolbar, we want the background gradient to show, for toolbar button descriptions to be legible.
        /// Unfortunately we also need to ensure that the toolbar buttons handle hover, to prevent the possibility of multiple descriptions being shown
        /// due to hover events passing through multiple buttons.
        /// This drawable is a workaround, that when placed front-most in the toolbar, allows to see whether hover events have been propagated through it without handling them.
        /// </summary>
        private partial class HoverInterceptor : Drawable
        {
            public IBindable<bool> ReceivedHover => receivedHover;
            private readonly Bindable<bool> receivedHover = new BindableBool();

            protected override bool OnHover(HoverEvent e)
            {
                receivedHover.Value = true;
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                receivedHover.Value = false;
                base.OnHoverLost(e);
            }
        }

        protected override void UpdateState(ValueChangedEvent<Visibility> state)
        {
            bool blockShow = hiddenByUser || OverlayActivationMode.Value == OverlayActivation.Disabled;

            if (state.NewValue == Visibility.Visible && blockShow)
            {
                State.Value = Visibility.Hidden;
                return;
            }

            base.UpdateState(state);
        }

        protected override void PopIn()
        {
            this.TransformTo(nameof(reveal), 1f, transition_time, Easing.OutQuint);
            this.FadeIn(transition_time / 4, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            userButton?.StateContainer?.Hide();

            this.TransformTo(nameof(reveal), 0f, transition_time, Easing.OutQuint);
            this.FadeOut(transition_time, Easing.InQuint);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (OverlayActivationMode.Value == OverlayActivation.Disabled)
                return false;

            switch (e.Action)
            {
                case GlobalAction.ToggleToolbar:
                    hiddenByUser = State.Value == Visibility.Visible; // set before toggling to allow the operation to always succeed.
                    ToggleVisibility();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }
}
