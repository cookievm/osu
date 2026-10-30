// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD
{
    public partial class DrawableGameplayLeaderboard : CompositeDrawable, ISerialisableDrawable
    {
        protected readonly FillFlowContainer<DrawableGameplayLeaderboardScore> Flow;

        private bool requiresScroll;
        private readonly OsuScrollContainer scroll;

        public DrawableGameplayLeaderboardScore? TrackedScore { get; private set; }

        public bool AlwaysShown { get; init; }

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.CollapseDuringGameplay), nameof(SkinnableComponentStrings.CollapseDuringGameplayDescription))]
        public Bindable<bool> CollapseDuringGameplay { get; } = new BindableBool(true);

        /// <summary>
        /// oily: what the leaderboard is sorted by. Only o!ily renders can sort by pp; lazer's own leaderboards stay sorted by score.
        /// </summary>
        [SettingSource("Sort by", "What the leaderboard is sorted by. Only o!ily renders can sort by pp.")]
        public Bindable<GameplayLeaderboardSort> SortBy { get; } = new Bindable<GameplayLeaderboardSort>();

        /// <summary>
        /// oily: use <see cref="BackgroundColour"/> and <see cref="PlayerColour"/> instead of lazer's colours (blue, gold for first, orange for the player).
        /// </summary>
        [SettingSource("Show mods", "Show each score's mods on the right of its row. Only o!ily renders know the mods.")]
        public Bindable<bool> ShowMods { get; } = new BindableBool(true);

        [SettingSource("Custom colours", "Use your own background colours instead of lazer's.")]
        public Bindable<bool> UseCustomColours { get; } = new BindableBool();

        /// <summary>
        /// oily: the background behind everyone else's scores, with <see cref="UseCustomColours"/>.
        /// </summary>
        [SettingSource("Background colour", "The background behind everyone else's scores, with custom colours on.")]
        public BindableColour4 BackgroundColour { get; } = new BindableColour4(Colour4.FromHex(@"2e576b"));

        /// <summary>
        /// oily: the background behind the player's own score, with <see cref="UseCustomColours"/>.
        /// </summary>
        [SettingSource("Player colour", "The background behind the player's own score, with custom colours on.")]
        public BindableColour4 PlayerColour { get; } = new BindableColour4(Colour4.FromHex(@"ebc247"));

        /// <summary>
        /// oily: how solid the backgrounds are. 0 is see-through, 1 is solid, and the default is lazer's own look.
        /// </summary>
        [SettingSource("Background opacity", "How solid the score backgrounds are. 0 is see-through, 1 is solid.")]
        public BindableFloat BackgroundOpacity { get; } = new BindableFloat(DrawableGameplayLeaderboardScore.DEFAULT_BACKGROUND_OPACITY)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        private readonly Bindable<bool> expanded = new BindableBool();

        [Resolved]
        private Player? player { get; set; }

        [Resolved]
        private IGameplayLeaderboardProvider leaderboardProvider { get; set; } = null!;

        private readonly IBindableList<GameplayLeaderboardScore> scores = new BindableList<GameplayLeaderboardScore>();
        private readonly Bindable<bool> configVisibility = new Bindable<bool>();
        private readonly IBindable<LocalUserPlayingState> userPlayingState = new Bindable<LocalUserPlayingState>();
        private readonly IBindable<bool> holdingForHUD = new Bindable<bool>();

        /// <summary>
        /// Create a new leaderboard.
        /// </summary>
        public DrawableGameplayLeaderboard()
        {
            // Extra lenience is applied so the scores don't get cut off from the left due to elastic easing transforms.
            float xOffset = DrawableGameplayLeaderboardScore.SHEAR_WIDTH + DrawableGameplayLeaderboardScore.ELASTIC_WIDTH_LENIENCE;

            Width = 260 + xOffset;
            Height = 300;

            InternalChildren = new Drawable[]
            {
                scroll = new InputDisabledScrollContainer
                {
                    ClampExtension = 0,
                    RelativeSizeAxes = Axes.Both,
                    Child = Flow = new FillFlowContainer<DrawableGameplayLeaderboardScore>
                    {
                        Alpha = 0f,
                        RelativeSizeAxes = Axes.X,
                        X = xOffset,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(2.5f),
                        LayoutDuration = 450,
                        LayoutEasing = Easing.OutQuint,
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, GameplayState? gameplayState, HUDOverlay? hudOverlay)
        {
            config.BindWith(OsuSetting.GameplayLeaderboard, configVisibility);

            if (gameplayState != null)
                userPlayingState.BindTo(gameplayState.PlayingState);

            if (hudOverlay != null)
                holdingForHUD.BindTo(hudOverlay.HoldingForHUD);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            scores.BindTo(leaderboardProvider.Scores);

            // One way only: binding both ways would make every leaderboard in the layout share one setting.
            if (leaderboardProvider is ISortableGameplayLeaderboardProvider sortable)
                SortBy.BindValueChanged(v => sortable.SortBy.Value = v.NewValue, true);
            scores.BindCollectionChanged((_, _) =>
            {
                Clear();
                foreach (var score in scores)
                    Add(score);
            }, true);

            configVisibility.BindValueChanged(_ => Scheduler.AddOnce(updateState));
            userPlayingState.BindValueChanged(_ => Scheduler.AddOnce(updateState));
            holdingForHUD.BindValueChanged(_ => Scheduler.AddOnce(updateState));
            CollapseDuringGameplay.BindValueChanged(_ => Scheduler.AddOnce(updateState));
            updateState();
        }

        private void updateState()
        {
            // prevents weird delay in the flow correctly appearing when toggling the leaderboard on.
            if (Flow.Alpha < 1)
                scroll.ScrollToStart(false);

            Flow.FadeTo(player?.Configuration.ShowLeaderboard != false && (configVisibility.Value || AlwaysShown) ? 1 : 0, 100, Easing.OutQuint);
            expanded.Value = !CollapseDuringGameplay.Value || userPlayingState.Value != LocalUserPlayingState.Playing || holdingForHUD.Value;
        }

        /// <summary>
        /// Adds a player to the leaderboard.
        /// </summary>
        public void Add(GameplayLeaderboardScore score)
        {
            var drawable = CreateLeaderboardScoreDrawable(score);

            if (score.Tracked)
            {
                if (TrackedScore != null)
                    throw new InvalidOperationException("Cannot track more than one score.");

                TrackedScore = drawable;
            }

            drawable.Expanded.BindTo(expanded);
            drawable.ShowMods.BindTo(ShowMods);
            drawable.UseCustomColours.BindTo(UseCustomColours);
            drawable.CustomColour.BindTo(BackgroundColour);
            drawable.CustomPlayerColour.BindTo(PlayerColour);
            drawable.BackgroundOpacity.BindTo(BackgroundOpacity);

            Flow.Add(drawable);
            drawable.ScorePosition.BindValueChanged(_ => Scheduler.AddOnce(sort));
            drawable.DisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(sort), true);
        }

        public void Clear()
        {
            Flow.Clear();
            TrackedScore = null;
            scroll.ScrollToStart(false);
        }

        protected virtual DrawableGameplayLeaderboardScore CreateLeaderboardScoreDrawable(GameplayLeaderboardScore score) =>
            new DrawableGameplayLeaderboardScore(score);

        protected override void Update()
        {
            base.Update();

            // limit leaderboard dimensions to a sane minimum.
            Width = Math.Max(Width, Flow.X + DrawableGameplayLeaderboardScore.MIN_WIDTH);
            Height = Math.Max(Height, DrawableGameplayLeaderboardScore.PANEL_HEIGHT);

            requiresScroll = Flow.DrawHeight > Height;

            if (requiresScroll && TrackedScore != null)
            {
                double scrollTarget = scroll.GetChildPosInContent(TrackedScore) + TrackedScore.DrawHeight / 2 - scroll.DrawHeight / 2;

                scroll.ScrollTo(scrollTarget);
            }

            const float panel_height = DrawableGameplayLeaderboardScore.PANEL_HEIGHT;

            float fadeBottom = (float)(scroll.Current + scroll.DrawHeight);
            float fadeTop = (float)(scroll.Current + panel_height);

            if (scroll.IsScrolledToStart()) fadeTop -= panel_height;
            if (!scroll.IsScrolledToEnd()) fadeBottom -= panel_height;

            // logic is mostly shared with Leaderboard, copied here for simplicity.
            foreach (var c in Flow)
            {
                float topY = c.ToSpaceOfOtherDrawable(Vector2.Zero, Flow).Y;
                float bottomY = topY + panel_height;

                bool requireTopFade = requiresScroll && topY <= fadeTop;
                bool requireBottomFade = requiresScroll && bottomY >= fadeBottom;

                if (!requireTopFade && !requireBottomFade)
                    c.Colour = Color4.White;
                else if (topY > fadeBottom + panel_height || bottomY < fadeTop - panel_height)
                    c.Colour = Color4.Transparent;
                else
                {
                    if (requireBottomFade)
                    {
                        c.Colour = ColourInfo.GradientVertical(
                            Color4.White.Opacity(Math.Min(1 - (topY - fadeBottom) / panel_height, 1)),
                            Color4.White.Opacity(Math.Min(1 - (bottomY - fadeBottom) / panel_height, 1)));
                    }
                    else if (requiresScroll)
                    {
                        c.Colour = ColourInfo.GradientVertical(
                            Color4.White.Opacity(Math.Min(1 - (fadeTop - topY) / panel_height, 1)),
                            Color4.White.Opacity(Math.Min(1 - (fadeTop - bottomY) / panel_height, 1)));
                    }
                }
            }
        }

        private void sort()
        {
            foreach (var score in Flow.ToArray())
                Flow.SetLayoutPosition(score, score.DisplayOrder.Value);
        }

        private partial class InputDisabledScrollContainer : OsuScrollContainer
        {
            public InputDisabledScrollContainer()
            {
                ScrollbarVisible = false;
            }

            public override bool HandlePositionalInput => false;
            public override bool HandleNonPositionalInput => false;
        }

        public bool UsesFixedAnchor { get; set; }
    }
}
