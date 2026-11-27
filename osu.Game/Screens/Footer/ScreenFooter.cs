// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets;
using osu.Game.Screens.Menu;
using osuTK;

namespace osu.Game.Screens.Footer
{
    public partial class ScreenFooter : OverlayContainer
    {
        public ScreenBackButton BackButton { get; private set; } = null!;

        private StableScreenBackButton stableBackButton = null!;
        private bool stableBack;

        /// <summary>
        /// oily: shows osu!stable's back button instead of lazer's (set by song select).
        /// </summary>
        public bool StableBackButton
        {
            set
            {
                if (stableBack == value)
                    return;

                stableBack = value;
                BackButton.Alpha = value ? 0 : 1;
                stableBackButton.Alpha = value ? 1 : 0;
            }
        }

        /// <summary>
        /// oily (ilo, 2026-11-27): where osu!stable's back button's bottom left corner goes, in screen space (song select's "Back button" skin component). Null keeps it in the corner.
        /// </summary>
        public Vector2? StableBackButtonScreenPosition { get; set; }

        /// <summary>
        /// oily: osu!stable's back button's size on screen.
        /// </summary>
        public Vector2 StableBackButtonScreenSize => stableBackButton.ScreenSpaceDrawQuad.AABBFloat.Size;

        /// <summary>
        /// Called when logo tracking begins, intended to bring the osu! logo to the frontmost visually.
        /// </summary>
        public Action<bool>? RequestLogoInFront { private get; init; }

        /// <summary>
        /// The back button was pressed.
        /// </summary>
        public Action? BackButtonPressed { private get; init; }

        public bool HasButtons => leftButtonsFlow.Count > 0 || rightButtonsFlow.Count > 0;

        public const int HEIGHT = 50;

        private const int padding = 60;
        private const float delay_per_button = 30;
        private const double transition_duration = 500;

        // Disable masking because it breaks due to the height of this container being less than the displayed content.
        // The height being set as it is is required for transition purposes.
        public override bool UpdateSubTreeMasking() => false;

        private readonly List<OverlayContainer> overlays = new List<OverlayContainer>();

        private Box background = null!;
        private FillFlowContainer<ScreenFooterButton> leftButtonsFlow = null!;
        private FillFlowContainer<ShearedButton> rightButtonsFlow = null!;
        private Container overlayContentContainer = null!;
        private Container<ScreenFooterButton> hiddenLeftButtonsContainer = null!;
        private Container<ShearedButton> hiddenRightButtonsContainer = null!;

        private GridContainer buttonsGrid = null!;

        private readonly Bindable<string> layoutSetting = new Bindable<string>();

        // oily: top toolbar buttons moved onto this bar.
        private readonly Bindable<string> toolbarLayoutSetting = new Bindable<string>();
        private FillFlowContainer toolbarButtonsFlow = null!;
        private ToolbarItem[] shownToolbarItems = Array.Empty<ToolbarItem>();
        private Bindable<RulesetInfo>? ruleset;
        private OsuGame? game;

        private IBindable<string>? panelSetting;
        private OilyPanelStyle panelStyle = new OilyPanelStyle();

        /// <summary>
        /// oily: the saved bottom bar layout (see <see cref="FooterLayout"/>).
        /// </summary>
        public FooterLayout Layout { get; private set; } = FooterLayout.CreateDefault();

        /// <summary>
        /// oily: room left on the right for the osu! logo when the back button is on the right.
        /// </summary>
        private const float logo_space = 150;

        private LogoTrackingContainer logoTrackingContainer = null!;
        private IDisposable? logoTracking;

        // TODO: This has some weird update logic local in this class, but it only works for overlay containers.
        // This is not what we want. The footer is to be displayed on *screens* with different colour schemes.
        // It needs to update on screen switch.
        //
        // For now it's locked to Blue to match song select (the most prominent usage).
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        public ScreenFooter(BackReceptor? receptor = null)
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            if (receptor == null)
                Add(receptor = new BackReceptor());

            receptor.OnBackPressed = () => BackButton.TriggerClick();
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager? config, Bindable<RulesetInfo>? ruleset, OsuGame? game)
        {
            this.ruleset = ruleset;
            this.game = game;

            if (config != null)
            {
                config.BindWith(OsuSetting.OilyFooterLayout, layoutSetting);
                config.BindWith(OsuSetting.OilyToolbarLayout, toolbarLayoutSetting);

                panelSetting = OilyPanelColours.Track(config, OilyPanel.BottomBar, style =>
                {
                    panelStyle = style;

                    if (background != null)
                        updateBackground(0);
                });
            }

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5
                },
                buttonsGrid = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding
                    {
                        Left = OsuGame.SCREEN_EDGE_MARGIN + ScreenBackButton.BUTTON_WIDTH + padding,
                        Right = OsuGame.SCREEN_EDGE_MARGIN,
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            leftButtonsFlow = new FillFlowContainer<ScreenFooterButton>
                            {
                                Name = @"Left footer buttons",
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Y = ScreenFooterButton.CORNER_RADIUS,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(7, 0),
                                AutoSizeAxes = Axes.Both,
                            },
                            overlayContentContainer = new Container
                            {
                                Name = @"Overlay-provided content",
                                RelativeSizeAxes = Axes.Both,
                                Y = -OsuGame.SCREEN_EDGE_MARGIN,
                            },
                            rightButtonsFlow = new FillFlowContainer<ShearedButton>
                            {
                                Name = @"Right footer buttons",
                                Anchor = Anchor.BottomRight,
                                Origin = Anchor.BottomRight,
                                Y = -OsuGame.SCREEN_EDGE_MARGIN,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(7, 0),
                                AutoSizeAxes = Axes.Both,
                            }
                        },
                    }
                },
                toolbarButtonsFlow = new FillFlowContainer
                {
                    Name = @"Toolbar buttons",
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Direction = FillDirection.Horizontal,
                    AutoSizeAxes = Axes.Both,
                },
                BackButton = new ScreenBackButton
                {
                    Margin = new MarginPadding { Bottom = OsuGame.SCREEN_EDGE_MARGIN, Left = OsuGame.SCREEN_EDGE_MARGIN },
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Action = onBackPressed,
                },
                // oily: osu!stable's back button (song select's "Back button" skin component), in the screen's bottom left corner.
                stableBackButton = new StableScreenBackButton
                {
                    Alpha = 0,
                    Action = onBackPressed,
                },
                hiddenLeftButtonsContainer = new Container<ScreenFooterButton>
                {
                    Name = "Hidden left footer buttons",
                    Margin = new MarginPadding { Left = OsuGame.SCREEN_EDGE_MARGIN + ScreenBackButton.BUTTON_WIDTH + padding },
                    Y = ScreenFooterButton.CORNER_RADIUS,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    AutoSizeAxes = Axes.Both,
                },
                hiddenRightButtonsContainer = new Container<ShearedButton>
                {
                    Name = "Hidden right footer buttons",
                    Margin = new MarginPadding { Bottom = OsuGame.SCREEN_EDGE_MARGIN, Right = OsuGame.SCREEN_EDGE_MARGIN },
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    AutoSizeAxes = Axes.Both,
                },
                (logoTrackingContainer = new LogoTrackingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                }).WithChild(logoTrackingContainer.LogoFacade.With(f =>
                {
                    f.Anchor = Anchor.BottomRight;
                    f.Origin = Anchor.Centre;
                    f.Position = new Vector2(-92, -46); // oily: nudged up-left so the whole o!ily lettering stays on screen
                })),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            layoutSetting.BindValueChanged(_ => applyLayout(), true);
            toolbarLayoutSetting.BindValueChanged(_ => updateToolbarButtons(), true);
        }

        /// <summary>
        /// oily: shows the top toolbar buttons that were moved onto this bar (in the skin editor's "Toolbars" tab).
        /// </summary>
        private void updateToolbarButtons()
        {
            var items = ToolbarLayout.Parse(toolbarLayoutSetting.Value).ItemsOnBottomBar().ToArray();

            if (items.SequenceEqual(shownToolbarItems))
                return;

            shownToolbarItems = items;
            toolbarButtonsFlow.Clear();

            if (ruleset == null)
                return;

            foreach (var item in items)
            {
                toolbarButtonsFlow.Add(new Container
                {
                    AutoSizeAxes = Axes.X,
                    Height = Toolbar.HEIGHT,
                    Child = Toolbar.CreateItem(item, false, () => game?.Toolbar.OnHome?.Invoke(), ruleset),
                });
            }
        }

        /// <summary>
        /// oily: the top toolbar buttons moved onto this bar, for dragging them in the skin editor.
        /// </summary>
        public Drawable ToolbarButtons => toolbarButtonsFlow;

        /// <summary>
        /// oily: the button for a toolbar item on this bar, if it's here.
        /// </summary>
        public Drawable? FindToolbarButton(ToolbarItem item)
        {
            int index = Array.IndexOf(shownToolbarItems, item);
            var children = toolbarButtonsFlow.Children;
            return index >= 0 && index < children.Count ? children[index] : null;
        }

        /// <summary>
        /// oily: which top toolbar buttons are on this bar now.
        /// </summary>
        public IReadOnlyList<ToolbarItem> ShownToolbarItems => shownToolbarItems;

        /// <summary>
        /// oily: shows or hides the bar and its background, and puts the back button on its side.
        /// </summary>
        private void applyLayout()
        {
            Layout = FooterLayout.Parse(layoutSetting.Value);

            updateBackground(0);

            // oily: the bar and its buttons grow or shrink together, still filling the screen's width.
            Scale = new Vector2(Layout.Size);
            Width = 1 / Layout.Size;

            bool backOnRight = Layout.BackButton == FooterBackSide.Right;
            float backSpace = ScreenBackButton.BUTTON_WIDTH + padding;

            buttonsGrid.Padding = backOnRight
                ? new MarginPadding { Left = OsuGame.SCREEN_EDGE_MARGIN, Right = OsuGame.SCREEN_EDGE_MARGIN + logo_space + backSpace }
                : new MarginPadding { Left = OsuGame.SCREEN_EDGE_MARGIN + backSpace, Right = OsuGame.SCREEN_EDGE_MARGIN };

            hiddenLeftButtonsContainer.Margin = new MarginPadding { Left = buttonsGrid.Padding.Left };
            hiddenRightButtonsContainer.Margin = new MarginPadding { Bottom = OsuGame.SCREEN_EDGE_MARGIN, Right = buttonsGrid.Padding.Right };

            BackButton.Anchor = BackButton.Origin = backOnRight ? Anchor.BottomRight : Anchor.BottomLeft;
            BackButton.Margin = backOnRight
                ? new MarginPadding { Bottom = OsuGame.SCREEN_EDGE_MARGIN, Right = OsuGame.SCREEN_EDGE_MARGIN + logo_space }
                : new MarginPadding { Bottom = OsuGame.SCREEN_EDGE_MARGIN, Left = OsuGame.SCREEN_EDGE_MARGIN };

            toolbarButtonsFlow.Margin = new MarginPadding
            {
                Bottom = (HEIGHT - Toolbar.HEIGHT) / 2f,
                Right = OsuGame.SCREEN_EDGE_MARGIN + logo_space + (backOnRight ? backSpace : 0),
            };

            if (State.Value == Visibility.Visible)
                PopIn();
        }

        /// <summary>
        /// oily: the bar's colour and opacity from settings (hidden in "buttons only" mode).
        /// </summary>
        private void updateBackground(double duration)
        {
            background.FadeColour(panelStyle.CustomColour ?? colourProvider.Background5, duration, Easing.OutQuint);
            background.Alpha = Layout.Bar == FooterBarMode.Shown ? Math.Clamp(panelStyle.Opacity, 0, 1) : 0;
        }

        /// <summary>
        /// oily: the buttons on the bar now, for dragging them in the skin editor.
        /// </summary>
        public IEnumerable<ScreenFooterButton> ShownButtons => leftButtonsFlow;

        /// <summary>
        /// oily: saves <see cref="Layout"/> after it was changed in place (by dragging a button).
        /// </summary>
        public void SaveLayout() => layoutSetting.Value = Layout.Serialise();

        protected override void Update()
        {
            base.Update();

            // oily: buttons dragged in the skin editor sit that far from their place, without moving the others.
            foreach (var button in leftButtonsFlow)
            {
                var saved = FooterLayout.ItemOf(button) is FooterItem item ? Layout.Buttons.FirstOrDefault(b => b.Item == item) : null;
                float x = saved?.OffsetX ?? 0;
                float y = saved?.OffsetY ?? 0;

                button.Margin = new MarginPadding { Left = x, Right = -x, Top = y, Bottom = -y };
            }

            // oily: the back button and the toolbar buttons can be dragged too.
            BackButton.Position = new Vector2(Layout.BackOffsetX, Layout.BackOffsetY);

            // oily: stable's back button keeps its own size whatever the bar's size is.
            stableBackButton.Scale = new Vector2(1 / Layout.Size);

            if (StableBackButtonScreenPosition is Vector2 stablePosition)
            {
                stableBackButton.Anchor = Anchor.TopLeft;
                stableBackButton.Position = ToLocalSpace(stablePosition);
            }
            else
            {
                stableBackButton.Anchor = Anchor.BottomLeft;
                stableBackButton.Position = Vector2.Zero;
            }
            toolbarButtonsFlow.Position = new Vector2(Layout.ToolbarButtonsOffsetX, Layout.ToolbarButtonsOffsetY);
        }

        /// <summary>
        /// oily: whether the bar is hidden in settings (the back key still works).
        /// </summary>
        public bool BarHidden => Layout.Bar == FooterBarMode.Hidden;

        private ScheduledDelegate? changeLogoDepthDelegate;

        public void StartTrackingLogo(OsuLogo logo, float duration = 0, Easing easing = Easing.None)
        {
            changeLogoDepthDelegate?.Cancel();
            changeLogoDepthDelegate = null;

            logoTracking = logoTrackingContainer.StartTracking(logo, duration, easing);
            RequestLogoInFront?.Invoke(true);
        }

        public void StopTrackingLogo()
        {
            logoTracking?.Dispose();
            logoTracking = null;

            changeLogoDepthDelegate = Scheduler.AddDelayed(() => RequestLogoInFront?.Invoke(false), transition_duration);
        }

        protected override void PopIn()
        {
            if (BarHidden)
            {
                this.MoveToY(ScreenFooterButton.HEIGHT * Layout.Size, transition_duration, Easing.OutQuint)
                    .Then()
                    .FadeOut();
                return;
            }

            leftButtonsFlow.FadeIn(transition_duration / 4, Easing.OutQuint);

            this.MoveToY(0, transition_duration, Easing.OutQuint)
                .FadeIn();
        }

        protected override void PopOut()
        {
            // Really we shouldn't need to do this, but some buttons protrude vertically more than expected
            // (see FooterButtonMods).
            leftButtonsFlow.FadeOut(transition_duration, Easing.OutQuint);

            this.MoveToY(ScreenFooterButton.HEIGHT * Layout.Size, transition_duration, Easing.OutQuint)
                .Then()
                .FadeOut();
        }

        public void SetButtons(IReadOnlyList<ScreenFooterButton> leftButtons, IReadOnlyList<ShearedButton> rightButtons, Direction direction)
        {
            overlays.Clear();

            // oily: the saved order, without the buttons hidden in settings.
            var arranged = Layout.Arrange(leftButtons);

            foreach (var unused in leftButtons.Except(arranged))
                unused.Dispose();

            leftButtons = arranged;

            this.HidePopover();
            clearActiveOverlayContainer();

            var oldLeftButtons = leftButtonsFlow.ToArray();

            for (int i = 0; i < oldLeftButtons.Length; i++)
            {
                var oldButton = oldLeftButtons[i];
                oldButton.State.Value = Visibility.Hidden;

                leftButtonsFlow.Remove(oldButton, false);
                hiddenLeftButtonsContainer.Add(oldButton);

                if (direction == Direction.Horizontal)
                    makeButtonDisappearToRight(oldButton, i, oldLeftButtons.Length, true);
                else
                    makeButtonDisappearToBottom(oldButton, i, oldLeftButtons.Length, true);
            }

            for (int i = 0; i < leftButtons.Count; i++)
            {
                var newButton = leftButtons[i];

                if (newButton.Overlay != null)
                {
                    newButton.Action = () => showOverlay(newButton.Overlay);
                    overlays.Add(newButton.Overlay);
                }

                Debug.Assert(!newButton.IsLoaded);
                leftButtonsFlow.Add(newButton);

                int index = i;

                // ensure transforms are added after LoadComplete to not be aborted by the FinishTransforms call.
                newButton.OnLoadComplete += _ =>
                {
                    if (direction == Direction.Horizontal)
                        makeButtonAppearFromLeft(newButton, index, leftButtons.Count, 240);
                    else
                        makeButtonAppearFromBottom(newButton, index);
                };
            }

            var oldRightButtons = rightButtonsFlow.ToArray();

            for (int i = 0; i < oldRightButtons.Length; i++)
            {
                var oldButton = oldRightButtons[i];

                rightButtonsFlow.Remove(oldButton, false);
                hiddenRightButtonsContainer.Add(oldButton);

                if (direction == Direction.Horizontal)
                    makeButtonDisappearToRight(oldButton, i, oldRightButtons.Length, true);
                else
                    makeButtonDisappearToBottom(oldButton, i, oldRightButtons.Length, true);
            }

            for (int i = 0; i < rightButtons.Count; i++)
            {
                var newButton = rightButtons[i];

                Debug.Assert(!newButton.IsLoaded);
                rightButtonsFlow.Add(newButton);

                int index = i;

                // ensure transforms are added after LoadComplete to not be aborted by the FinishTransforms call.
                newButton.OnLoadComplete += _ =>
                {
                    if (direction == Direction.Horizontal)
                        makeButtonAppearFromLeft(newButton, index, leftButtons.Count, 240);
                    else
                        makeButtonAppearFromBottom(newButton, index);
                };
            }
        }

        public ShearedOverlayContainer? ActiveOverlay { get; private set; }

        private VisibilityContainer? activeOverlayContent;

        private readonly List<ScreenFooterButton> temporarilyHiddenLeftButtons = new List<ScreenFooterButton>();

        public IDisposable RegisterActiveOverlayContainer(ShearedOverlayContainer overlay, out VisibilityContainer? overlayContent)
        {
            if (ActiveOverlay != null)
            {
                throw new InvalidOperationException(@"Cannot set overlay content while one is already present. " +
                                                    $@"The previous overlay ({ActiveOverlay.GetType().Name}) should be hidden first.");
            }

            ActiveOverlay = overlay;

            Debug.Assert(temporarilyHiddenLeftButtons.Count == 0);

            var targetButton = leftButtonsFlow.SingleOrDefault(b => b.Overlay == overlay);

            temporarilyHiddenLeftButtons.AddRange(targetButton != null
                ? leftButtonsFlow.SkipWhile(b => b != targetButton).Skip(1)
                : leftButtonsFlow);

            for (int i = temporarilyHiddenLeftButtons.Count - 1; i >= 0; i--)
            {
                var button = temporarilyHiddenLeftButtons[i];
                button.State.Value = Visibility.Hidden;
                leftButtonsFlow.Remove(button, false);
                hiddenLeftButtonsContainer.Add(button);

                makeButtonDisappearToBottom(button, 0, 0, false);
            }

            for (int i = rightButtonsFlow.Count - 1; i >= 0; i--)
            {
                var button = rightButtonsFlow[i];
                makeButtonDisappearToBottom(button, 0, 0, false);
            }

            updateColourScheme(overlay.ColourProvider.Hue);

            overlayContent = overlay.CreateFooterContent();
            activeOverlayContent = overlayContent;
            var content = overlayContent;

            if (content != null)
                overlayContentContainer.Child = content;

            if (temporarilyHiddenLeftButtons.Count > 0)
                this.Delay(60).Schedule(() => content?.Show());
            else
                content?.Show();

            return new InvokeOnDisposal(clearActiveOverlayContainer);
        }

        private void clearActiveOverlayContainer()
        {
            if (ActiveOverlay == null)
                return;

            Debug.Assert(activeOverlayContent != null);

            activeOverlayContent.Hide();
            activeOverlayContent.Expire();

            double timeUntilRun = activeOverlayContent.LatestTransformEndTime - Time.Current;

            for (int i = 0; i < temporarilyHiddenLeftButtons.Count; i++)
            {
                var button = temporarilyHiddenLeftButtons[i];
                button.State.Value = Visibility.Visible;
                hiddenLeftButtonsContainer.Remove(button, false);

                // temporarily bypass autosize on the X axis to prevent the buttons taking space
                // immediately upon being moved back to the flow.
                // this prevents the overlay content jumping to the right during its fade-out.
                button.BypassAutoSizeAxes = Axes.X;
                leftButtonsFlow.Add(button);

                makeButtonAppearFromBottom(button, 0);
            }

            temporarilyHiddenLeftButtons.Clear();

            for (int i = rightButtonsFlow.Count - 1; i >= 0; i--)
            {
                var button = rightButtonsFlow[i];
                makeButtonAppearFromBottom(button, 0);
            }

            updateColourScheme(OverlayColourScheme.Aquamarine.GetHue());

            Scheduler.AddDelayed(() =>
            {
                // overlay content is done displaying, re-enable autosize on all active buttons
                foreach (var button in leftButtonsFlow)
                    button.BypassAutoSizeAxes = Axes.None;
            }, timeUntilRun);

            activeOverlayContent = null;
            ActiveOverlay = null;
        }

        private void updateColourScheme(int hue)
        {
            colourProvider.ChangeColourScheme(hue);

            updateBackground(150);

            foreach (var button in leftButtonsFlow)
                button.UpdateDisplay();
        }

        private void makeButtonAppearFromLeft(IScreenFooterButton button, int index, int count, float startDelay)
            => button.AppearFromLeft(startDelay + (count - index) * delay_per_button);

        private void makeButtonAppearFromBottom(IScreenFooterButton button, int index)
            => button.AppearFromBottom(index * delay_per_button);

        private void makeButtonDisappearToRight(IScreenFooterButton button, int index, int count, bool expire)
            => button.DisappearToRight((count - index) * delay_per_button, expire);

        private void makeButtonDisappearToBottom(IScreenFooterButton button, int index, int count, bool expire)
            => button.DisappearToBottom((count - index) * delay_per_button, expire);

        private void showOverlay(OverlayContainer overlay)
        {
            this.HidePopover();

            foreach (var o in overlays.Where(o => o != overlay))
                o.Hide();

            overlay.ToggleVisibility();
        }

        private void onBackPressed()
        {
            if (ActiveOverlay != null)
            {
                if (ActiveOverlay.OnBackButton())
                    return;

                ActiveOverlay.Hide();
                return;
            }

            BackButtonPressed?.Invoke();
        }

        public partial class BackReceptor : Drawable, IKeyBindingHandler<GlobalAction>
        {
            public Action? OnBackPressed;

            public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
            {
                if (e.Repeat)
                    return false;

                switch (e.Action)
                {
                    case GlobalAction.Back:
                        OnBackPressed?.Invoke();
                        return true;
                }

                return false;
            }

            public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
            {
            }
        }
    }
}
