// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Moq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Menus
{
    [TestFixture]
    public partial class TestSceneToolbar : OsuManualInputManagerTestScene
    {
        private TestToolbar toolbar;

        [Resolved]
        private IRulesetStore rulesets { get; set; }

        [Resolved]
        private OsuConfigManager config { get; set; }

        [Cached]
        private readonly NowPlayingOverlay nowPlayingOverlay = new NowPlayingOverlay
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = Toolbar.HEIGHT,
        };

        [Cached]
        private readonly VolumeOverlay volumeOverlay = new VolumeOverlay
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
        };

        private readonly Mock<TestNotificationOverlay> notifications = new Mock<TestNotificationOverlay>();

        private readonly BindableInt unreadNotificationCount = new BindableInt();

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.CacheAs<INotificationOverlay>(notifications.Object);
            notifications.SetupGet(n => n.UnreadCount).Returns(unreadNotificationCount);
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            // oily: every test starts from the fixed toolbar, whatever the last test changed it to.
            config.SetValue(OsuSetting.OilyToolbarLayout, ToolbarLayout.FIXED);

            Remove(nowPlayingOverlay, false);
            Remove(volumeOverlay, false);

            Children = new Drawable[]
            {
                nowPlayingOverlay,
                volumeOverlay,
                toolbar = new TestToolbar { State = { Value = Visibility.Visible } },
            };
        });

        [Test]
        public void TestNotificationCounter()
        {
            setNotifications(1);
            setNotifications(2);
            setNotifications(3);
            setNotifications(0);
            setNotifications(144);

            void setNotifications(int count)
                => AddStep($"set notification count to {count}",
                    () => unreadNotificationCount.Value = count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRulesetSwitchingShortcut(bool toolbarHidden)
        {
            ToolbarRulesetSelector rulesetSelector = null;

            if (toolbarHidden)
                AddStep("hide toolbar", () => toolbar.Hide());

            AddStep("retrieve ruleset selector", () => rulesetSelector = toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single());

            for (int i = 0; i < 4; i++)
            {
                var expected = rulesets.AvailableRulesets.ElementAt(i);
                var numberKey = Key.Number1 + i;

                AddStep($"switch to ruleset {i} via shortcut", () =>
                {
                    InputManager.PressKey(Key.ControlLeft);
                    InputManager.Key(numberKey);
                    InputManager.ReleaseKey(Key.ControlLeft);
                });

                AddUntilStep("ruleset switched", () => rulesetSelector.Current.Value.Equals(expected));
            }
        }

        [TestCase(OverlayActivation.All)]
        [TestCase(OverlayActivation.Disabled)]
        public void TestButtonKeyboardInputRespectsOverlayActivation(OverlayActivation mode)
        {
            AddStep($"set activation mode to {mode}", () => toolbar.OverlayActivationMode.Value = mode);
            AddStep("hide toolbar", () => toolbar.Hide());

            if (mode == OverlayActivation.Disabled)
                AddAssert("check buttons not accepting input", () => InputManager.NonPositionalInputQueue.OfType<ToolbarButton>().Count(), () => Is.Zero);
            else
                AddAssert("check buttons accepting input", () => InputManager.NonPositionalInputQueue.OfType<ToolbarButton>().Count(), () => Is.Not.Zero);
        }

        [TestCase(OverlayActivation.All)]
        [TestCase(OverlayActivation.Disabled)]
        public void TestRespectsOverlayActivation(OverlayActivation mode)
        {
            AddStep($"set activation mode to {mode}", () => toolbar.OverlayActivationMode.Value = mode);
            AddStep("hide toolbar", () => toolbar.Hide());
            AddStep("try to show toolbar", () => toolbar.Show());

            if (mode == OverlayActivation.Disabled)
                AddAssert("toolbar still hidden", () => toolbar.State.Value == Visibility.Hidden);
            else
                AddAssert("toolbar is visible", () => toolbar.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestScrollInput()
        {
            OsuScrollContainer scroll = null;

            AddStep("add scroll layer", () => Add(scroll = new OsuScrollContainer
            {
                Depth = 1f,
                RelativeSizeAxes = Axes.Both,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = DrawHeight * 2,
                    Colour = ColourInfo.GradientVertical(Color4.Gray, Color4.DarkGray),
                }
            }));

            AddStep("hover toolbar", () => InputManager.MoveMouseTo(toolbar.ChildrenOfType<Toolbar.ToolbarStrip>().First()));
            AddStep("perform scroll", () => InputManager.ScrollVerticalBy(500));
            AddAssert("not scrolled", () => scroll.Current == 0);
        }

        [Test]
        public void TestVolumeControlViaMusicButtonScroll()
        {
            AddStep("hover toolbar music button", () => InputManager.MoveMouseTo(this.ChildrenOfType<ToolbarMusicButton>().Single()));

            AddStep("reset volume", () => Audio.Volume.Value = 1);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("scroll down", () => InputManager.ScrollVerticalBy(-10), 5);
            AddAssert("volume lowered down", () => Audio.Volume.Value < 1);
            AddRepeatStep("scroll up", () => InputManager.ScrollVerticalBy(10), 5);
            AddAssert("volume raised up", () => Audio.Volume.Value == 1);

            AddStep("move mouse away", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddAssert("button not hovered", () => !this.ChildrenOfType<ToolbarMusicButton>().Single().IsHovered);

            AddStep("set volume to 0.5", () => Audio.Volume.Value = 0.5);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("scroll down", () => InputManager.ScrollVerticalBy(-10), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
            AddRepeatStep("scroll up", () => InputManager.ScrollVerticalBy(10), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
        }

        [Test]
        public void TestVolumeControlViaMusicButtonArrowKeys()
        {
            AddStep("hover toolbar music button", () => InputManager.MoveMouseTo(this.ChildrenOfType<ToolbarMusicButton>().Single()));

            AddStep("reset volume", () => Audio.Volume.Value = 1);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("arrow down", () => InputManager.Key(Key.Down), 5);
            AddAssert("volume lowered down", () => Audio.Volume.Value < 1);
            AddRepeatStep("arrow up", () => InputManager.Key(Key.Up), 5);
            AddAssert("volume raised up", () => Audio.Volume.Value == 1);

            AddStep("hide volume overlay", () => volumeOverlay.Hide());
            AddStep("move mouse away", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddAssert("button not hovered", () => !this.ChildrenOfType<ToolbarMusicButton>().Single().IsHovered);

            AddStep("set volume", () => Audio.Volume.Value = 0.5);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("arrow down", () => InputManager.Key(Key.Down), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
            AddRepeatStep("arrow up", () => InputManager.Key(Key.Up), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
        }

        [Test]
        public void TestDiscordButton()
        {
            // oily (ilo, 2026-11-26): a Discord button straight after Rankings.
            string opened = null;

            AddUntilStep("discord button shown", () => toolbar.ChildrenOfType<ToolbarDiscordButton>().SingleOrDefault()?.IsPresent == true);
            AddUntilStep("straight after rankings", () =>
            {
                float discord = toolbar.ChildrenOfType<ToolbarDiscordButton>().Single().ScreenSpaceDrawQuad.Centre.X;
                float rankings = toolbar.ChildrenOfType<ToolbarRankingsButton>().Single().ScreenSpaceDrawQuad.Centre.X;
                return rankings < discord && toolbar.ChildrenOfType<ToolbarButton>().Where(b => b.IsPresent)
                                                   .All(b => b.ScreenSpaceDrawQuad.Centre.X <= rankings + 1 || b.ScreenSpaceDrawQuad.Centre.X >= discord - 1);
            });

            AddAssert("icon is the outlined logo", () => toolbar.ChildrenOfType<ToolbarDiscordButton>().Single().ChildrenOfType<Sprite>()
                                                                 .Any(s => s.GetType() == typeof(Sprite) && s.Texture != null && s.Texture.Width > s.Texture.Height));

            AddStep("click with the app installed", () =>
            {
                var button = toolbar.ChildrenOfType<ToolbarDiscordButton>().Single();
                button.DiscordInstalled = () => true;
                button.OpenLink = l => opened = l;
                button.TriggerClick();
            });
            AddAssert("opened in the app", () => opened, () => Is.EqualTo(ToolbarDiscordButton.APP_LINK));

            AddStep("click without the app", () =>
            {
                var button = toolbar.ChildrenOfType<ToolbarDiscordButton>().Single();
                button.DiscordInstalled = () => false;
                button.TriggerClick();
            });
            AddAssert("opened in the browser", () => opened, () => Is.EqualTo("https://discord.gg/ilystudios"));
        }

        [Test]
        public void TestFixedToolbarSpreadInOrder()
        {
            // oily (ilo, 2026-11-26): rulesets, beatmap listing, chat, dashboard, rankings, Discord, notifications, now playing, clock, profile,
            // spread evenly from left to right.
            AddStep("use the fixed layout", () => config.SetValue(OsuSetting.OilyToolbarLayout, ToolbarLayout.FIXED));
            AddUntilStep("bar is spread", () => toolbar.ChildrenOfType<Toolbar.ToolbarStrip>().SingleOrDefault()?.Spread == true);

            AddAssert("icons in order", () => iconsLeftToRight().Select(d => d.GetType()).ToArray(), () => Is.EqualTo(new[]
            {
                typeof(ToolbarRulesetTabButton), typeof(ToolbarRulesetTabButton), typeof(ToolbarRulesetTabButton), typeof(ToolbarRulesetTabButton),
                typeof(ToolbarBeatmapListingButton), typeof(ToolbarChatButton), typeof(ToolbarSocialButton), typeof(ToolbarRankingsButton),
                typeof(ToolbarDiscordButton), typeof(ToolbarNotificationButton), typeof(ToolbarMusicButton), typeof(ToolbarClock), typeof(ToolbarUserButton),
            }));

            // the fixed bar is narrow, so check the gaps on a full-width copy of it.
            AddStep("make the bar full width", () =>
            {
                var layout = ToolbarLayout.Parse(ToolbarLayout.FIXED);
                layout.Bars[0].Length = 1;
                layout.Bars[0].OffsetX = layout.Bars[0].OffsetY = 0;
                config.SetValue(OsuSetting.OilyToolbarLayout, layout.Serialise());
            });

            AddUntilStep("gaps are equal", () =>
            {
                var icons = iconsLeftToRight().ToArray();
                float[] gaps = icons.Zip(icons.Skip(1), (a, b) => b.ScreenSpaceDrawQuad.TopLeft.X - a.ScreenSpaceDrawQuad.TopRight.X).ToArray();
                return gaps.Max() - gaps.Min() < 1 && gaps.Min() > 0;
            });

            AddAssert("spread from end to end", () =>
            {
                var strip = toolbar.ChildrenOfType<Toolbar.ToolbarStrip>().Single().ScreenSpaceDrawQuad;
                var icons = iconsLeftToRight().ToArray();
                return Math.Abs(icons.First().ScreenSpaceDrawQuad.TopLeft.X - strip.TopLeft.X) < 1
                       && Math.Abs(icons.Last().ScreenSpaceDrawQuad.TopRight.X - strip.TopRight.X) < 1;
            });

            IEnumerable<Drawable> iconsLeftToRight()
                => toolbar.ChildrenOfType<ToolbarRulesetTabButton>().Cast<Drawable>()
                          .Concat(toolbar.ChildrenOfType<ToolbarButton>().Where(b => b.IsPresent && b.FindClosestParent<ToolbarRulesetSelector>() == null))
                          .Concat(toolbar.ChildrenOfType<ToolbarClock>())
                          .OrderBy(d => d.ScreenSpaceDrawQuad.Centre.X);
        }

        [Test]
        public void TestBeatmapListingFocus()
        {
            // oily (ilo, 2026-11-26): with the beatmap listing open, only its button and the profile button stay,
            // sliding to the bottom right corner of the listing.
            TestListing listing = null;

            AddStep("add a listing", () =>
            {
                Add(listing = new TestListing());
                toolbar.ChildrenOfType<ToolbarBeatmapListingButton>().Single().StateContainer = listing;
            });

            var rights = new List<float>();

            AddStep("open the listing", () =>
            {
                listing.Show();
                toolbar.OnUpdate += _ => rights.Add(toolbar.ChildrenOfType<ToolbarUserButton>().Single().ScreenSpaceDrawQuad.TopRight.X);
            });

            AddUntilStep("other buttons gone", () => toolbar.ChildrenOfType<ToolbarButton>()
                                                           .Where(b => b is not ToolbarBeatmapListingButton && b is not ToolbarUserButton && b.FindClosestParent<ToolbarRulesetSelector>() == null)
                                                           .All(b => !b.IsPresent)
                                                       && !toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single().IsPresent
                                                       && !toolbar.ChildrenOfType<ToolbarClock>().Single().IsPresent);

            AddUntilStep("listing then profile, at the listing's bottom right", () =>
            {
                var listingButton = toolbar.ChildrenOfType<ToolbarBeatmapListingButton>().Single().ScreenSpaceDrawQuad;
                var user = toolbar.ChildrenOfType<ToolbarUserButton>().Single().ScreenSpaceDrawQuad;
                float right = listing.ScreenSpaceDrawQuad.BottomRight.X;

                return listingButton.TopRight.X <= user.TopLeft.X && user.TopRight.X <= right && right - user.TopRight.X < 30;
            });

            AddWaitStep("let it settle", 10);
            AddAssert("profile button moved one way only (no overshoot)", () =>
            {
                float end = rights.Last();
                float start = rights.First();
                return rights.All(r => r >= Math.Min(start, end) - 1 && r <= Math.Max(start, end) + 1);
            });

            AddStep("close the listing", () => listing.Hide());
            AddUntilStep("other buttons back", () => toolbar.ChildrenOfType<ToolbarClock>().Single().Alpha == 1
                                                     && toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single().Alpha == 1);
        }

        [Test]
        public void TestChatFocus()
        {
            // oily (ilo, 2026-11-26): with chat open, only its button and the profile button stay,
            // sliding to the bottom right corner of the chat box.
            TestListing chat = null;

            AddStep("add a chat box", () =>
            {
                Add(chat = new TestListing { Anchor = Anchor.BottomCentre, Origin = Anchor.BottomCentre, Height = 0.4f });
                toolbar.ChildrenOfType<ToolbarChatButton>().Single().StateContainer = chat;
            });

            AddStep("open chat", () => chat.Show());

            AddUntilStep("other buttons gone", () => toolbar.ChildrenOfType<ToolbarButton>()
                                                           .Where(b => b is not ToolbarChatButton && b is not ToolbarUserButton && b.FindClosestParent<ToolbarRulesetSelector>() == null)
                                                           .All(b => !b.IsPresent)
                                                       && !toolbar.ChildrenOfType<ToolbarClock>().Single().IsPresent);

            AddUntilStep("chat then profile, at the chat box's bottom right", () =>
            {
                var chatButton = toolbar.ChildrenOfType<ToolbarChatButton>().Single().ScreenSpaceDrawQuad;
                var user = toolbar.ChildrenOfType<ToolbarUserButton>().Single().ScreenSpaceDrawQuad;
                float right = chat.ScreenSpaceDrawQuad.BottomRight.X;

                return chatButton.TopRight.X <= user.TopLeft.X && user.TopLeft.X - chatButton.TopRight.X < 30
                       && user.TopRight.X <= right && right - user.TopRight.X < 30;
            });

            AddStep("close chat", () => chat.Hide());
            AddUntilStep("other buttons back", () => toolbar.ChildrenOfType<ToolbarClock>().Single().Alpha == 1
                                                     && toolbar.ChildrenOfType<ToolbarBeatmapListingButton>().Single().Alpha == 1);
        }

        private partial class TestListing : OverlayContainer
        {
            public TestListing()
            {
                RelativeSizeAxes = Axes.Both;
                Width = 0.85f;
                Anchor = Origin = Anchor.TopCentre;
            }

            protected override void PopIn() => this.FadeIn();

            protected override void PopOut() => this.FadeOut();
        }

        [Test]
        public void TestRulesetSelectorOverflow()
        {
            AddStep("set toolbar width", () =>
            {
                toolbar.RelativeSizeAxes = Axes.None;
                toolbar.Width = 400;
            });
            AddStep("move mouse over rankings toggle button", () =>
            {
                var button = toolbar.ChildrenOfType<ToolbarRankingsButton>().Single();
                InputManager.MoveMouseTo(button);
            });
            AddAssert("no ruleset toggle buttons hovered", () => !toolbar.ChildrenOfType<ToolbarRulesetTabButton>().Any(button => button.IsHovered));
            // oily: the fixed toolbar is see-through, so no gradient shows behind it.
            AddUntilStep("toolbar stays see-through", () => toolbar.ChildrenOfType<Toolbar.ToolbarBackground>().Single().Children.All(d => d.Alpha == 0));
        }

        [Test]
        public void TestToolbarLayout()
        {
            AddStep("left sidebar and bottom bar", () =>
            {
                var layout = ToolbarLayout.CreateDefault();
                layout.Bars.Clear();
                layout.Bars.Add(new ToolbarBarLayout { Edge = ToolbarEdge.Left });
                layout.Bars.Add(new ToolbarBarLayout { Edge = ToolbarEdge.Bottom });

                foreach (var button in layout.Buttons.Where(b => b.Item is ToolbarItem.Rulesets or ToolbarItem.Clock))
                    button.Bar = 1;

                config.SetValue(OsuSetting.OilyToolbarLayout, layout.Serialise());
            });

            AddUntilStep("two bars", () => toolbar.ChildrenOfType<Toolbar.ToolbarStrip>().Count(), () => Is.EqualTo(2));
            AddAssert("no news, changelog or wiki", () => !toolbar.ChildrenOfType<ToolbarNewsButton>().Any()
                                                          && !toolbar.ChildrenOfType<ToolbarChangelogButton>().Any()
                                                          && !toolbar.ChildrenOfType<ToolbarWikiButton>().Any());
            AddUntilStep("screen kept clear on the left and bottom", () => toolbar.ScreenPadding.Left >= Toolbar.HEIGHT && toolbar.ScreenPadding.Bottom >= Toolbar.HEIGHT && toolbar.ScreenPadding.Top == 0);
            AddAssert("profile on the sidebar", () => toolbar.Find(ToolbarItem.User)?.Edge, () => Is.EqualTo(ToolbarEdge.Left));
            AddAssert("rulesets stacked on the bottom bar", () => toolbar.Find(ToolbarItem.Rulesets)?.Edge, () => Is.EqualTo(ToolbarEdge.Bottom));
            AddAssert("buttons not rotated", () => toolbar.ChildrenOfType<ToolbarButton>().All(b => b.Rotation == 0));

            AddStep("hide toolbar", () => toolbar.Hide());
            AddUntilStep("screen padding gone", () => toolbar.ScreenPadding.Left < 1 && toolbar.ScreenPadding.Bottom < 1);

            AddStep("show toolbar", () => toolbar.Show());
            AddStep("no toolbars", () =>
            {
                var layout = ToolbarLayout.CreateDefault();
                layout.Bars.Clear();
                config.SetValue(OsuSetting.OilyToolbarLayout, layout.Serialise());
            });
            AddUntilStep("no bars", () => !toolbar.ChildrenOfType<Toolbar.ToolbarStrip>().Any());
            AddAssert("no screen padding", () => toolbar.ScreenPadding.Left < 1 && toolbar.ScreenPadding.Top < 1);
        }

        [Test]
        public void TestSidebarModesStacked()
        {
            AddStep("rulesets on a right sidebar", () =>
            {
                var layout = ToolbarLayout.CreateDefault();
                layout.Bars[0].Edge = ToolbarEdge.Right;
                config.SetValue(OsuSetting.OilyToolbarLayout, layout.Serialise());
            });

            AddUntilStep("modes one above another", () =>
            {
                var tabs = toolbar.ChildrenOfType<ToolbarRulesetTabButton>().ToArray();
                return tabs.Length > 1 && tabs.Select(t => t.ScreenSpaceDrawQuad.Centre.X).Distinct().Count() == 1
                                       && tabs.Select(t => t.ScreenSpaceDrawQuad.Centre.Y).Distinct().Count() == tabs.Length;
            });
            AddAssert("modes inside the sidebar", () =>
            {
                var strip = toolbar.ChildrenOfType<Toolbar.ToolbarStrip>().Single();
                return toolbar.ChildrenOfType<ToolbarRulesetTabButton>().All(t => strip.ScreenSpaceDrawQuad.AABBFloat.Contains(t.ScreenSpaceDrawQuad.Centre));
            });

            AddStep("reset", () => config.SetValue(OsuSetting.OilyToolbarLayout, string.Empty));
        }

        [Test]
        public void TestCroppedAndDraggedBars()
        {
            AddStep("half-length top bar", () =>
            {
                var layout = ToolbarLayout.CreateDefault();
                layout.Bars[0].Length = 0.5f;
                config.SetValue(OsuSetting.OilyToolbarLayout, layout.Serialise());
            });

            AddUntilStep("bar half as wide", () => toolbar.ChildrenOfType<Toolbar.ToolbarStrip>().Single().DrawWidth, () => Is.EqualTo(toolbar.DrawWidth / 2).Within(1));
            AddAssert("bar in the middle", () => toolbar.ChildrenOfType<Toolbar.ToolbarStrip>().Single().X, () => Is.EqualTo(toolbar.DrawWidth / 4).Within(1));
            AddAssert("screen still kept clear", () => toolbar.ScreenPadding.Top, () => Is.EqualTo(Toolbar.HEIGHT).Within(1));

            AddStep("drag bar past the bottom of the screen", () =>
            {
                var layout = ToolbarLayout.CreateDefault();
                layout.Bars[0].OffsetY = 0.99f;
                config.SetValue(OsuSetting.OilyToolbarLayout, layout.Serialise());
            });

            AddUntilStep("bar near the bottom", () => toolbar.ChildrenOfType<Toolbar.ToolbarStrip>().Single().Y, () => Is.EqualTo(toolbar.DrawHeight * 0.99f).Within(1));
            AddAssert("floating bar leaves the screen alone", () => toolbar.ScreenPadding.Top, () => Is.Zero);

            AddStep("reset", () => config.SetValue(OsuSetting.OilyToolbarLayout, string.Empty));
        }

        [Test]
        public void TestTransparentToolbar()
        {
            AddStep("toolbar background 0%", () =>
            {
                var colours = new OilyPanelColours();
                colours.Panels[OilyPanel.Toolbar] = new OilyPanelStyle { Opacity = 0 };
                config.SetValue(OsuSetting.OilyPanelColours, colours.Serialise());
            });

            // Every background box of the bar (not the buttons' own hover boxes) is see-through.
            AddUntilStep("no background shows", () => toolbar.Strips.SelectMany(s => s.ChildrenOfType<Box>())
                                                              .Where(b => b.FindClosestParent<ToolbarButton>() == null && b.FindClosestParent<ToolbarRulesetSelector>() == null
                                                                          && b.FindClosestParent<ToolbarClock>() == null && b.Parent?.GetType().Name != "OsuScrollbar" && b.Alpha != 0)
                                                              .Select(b => $"{b.Parent?.Name} {b.Parent?.GetType().Name}/{b.Parent?.Parent?.GetType().Name}"),
                () => Is.Empty);

            AddStep("reset colours", () => config.SetValue(OsuSetting.OilyPanelColours, string.Empty));
        }

        [Test]
        public void TestToolbarColours()
        {
            AddStep("red icons, half see-through bar, gold gamemode", () =>
            {
                var colours = new OilyPanelColours();
                colours.Colours[OilyColour.ToolbarIcons] = "#ff0000";
                colours.Colours[OilyColour.GamemodeSelected] = "#ffcc00";
                colours.Panels[OilyPanel.Toolbar] = new OilyPanelStyle { Colour = "#0000ff", Opacity = 0.5f };
                config.SetValue(OsuSetting.OilyPanelColours, colours.Serialise());
            });

            AddUntilStep("chat icon red", () => toolbar.ChildrenOfType<ToolbarChatButton>().Single().ChildrenOfType<osu.Game.Graphics.Containers.ConstrainedIconContainer>().Single().Colour.TopLeft.SRGB.R,
                () => Is.EqualTo(1).Within(0.01f));
            // oily (ilo, 2026-11-26): the toolbar is fixed and always see-through, so its colour setting does nothing.
            AddUntilStep("bar stays see-through", () => toolbar.ChildrenOfType<Toolbar.ToolbarBackground>().First().Children.First().Alpha, () => Is.EqualTo(0));
            AddUntilStep("selected gamemode gold", () => toolbar.ChildrenOfType<ToolbarRulesetTabButton>().First(t => t.Active.Value)
                                                                .ChildrenOfType<osu.Game.Graphics.Containers.ConstrainedIconContainer>().Single().Colour.TopLeft.SRGB.G,
                () => Is.EqualTo(0.8f).Within(0.01f));

            AddStep("reset colours", () => config.SetValue(OsuSetting.OilyPanelColours, string.Empty));
            AddUntilStep("bar still see-through", () => toolbar.ChildrenOfType<Toolbar.ToolbarBackground>().First().Children.First().Alpha, () => Is.EqualTo(0));
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("reset toolbar layout", () =>
        {
            config.SetValue(OsuSetting.OilyToolbarLayout, string.Empty);
            config.SetValue(OsuSetting.OilyPanelColours, string.Empty);
        });

        public partial class TestToolbar : Toolbar
        {
            public new Bindable<OverlayActivation> OverlayActivationMode => base.OverlayActivationMode as Bindable<OverlayActivation>;
        }

        // interface mocks break hot reload, mocking this stub implementation instead works around it.
        // see: https://github.com/moq/moq4/issues/1252
        [UsedImplicitly]
        public class TestNotificationOverlay : INotificationOverlay
        {
            public virtual void Post(Notification notification)
            {
            }

            public virtual void Hide()
            {
            }

            public virtual IBindable<int> UnreadCount { get; } = new Bindable<int>();

            public IEnumerable<Notification> AllNotifications => Enumerable.Empty<Notification>();
        }
    }
}
