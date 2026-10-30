// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Layout;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD
{
    public partial class DrawableGameplayLeaderboardScore : CompositeDrawable
    {
        public const float MIN_WIDTH = extended_left_panel_width + avatar_size / 2 + 5;

        private const float left_panel_extension_width = 20;

        private const float regular_left_panel_width = avatar_size + avatar_size / 2;
        private const float extended_left_panel_width = regular_left_panel_width + left_panel_extension_width;

        private const float accuracy_combo_width_cutoff = 150;
        private const float username_score_width_cutoff = 50;

        private const float avatar_size = PANEL_HEIGHT;

        public const float PANEL_HEIGHT = 38f;

        public static readonly float SHEAR_WIDTH = PANEL_HEIGHT * OsuGame.SHEAR.X;

        /// <summary>
        /// Extra width lenience to account for the out-of-range values produced by elastic easing when the score panel becomes extended (due to earning first score position or is a tracked score).
        /// </summary>
        public const float ELASTIC_WIDTH_LENIENCE = 10f;

        private const double panel_transition_duration = 500;
        private const double text_transition_duration = 200;

        public Bindable<bool> Expanded { get; } = new BindableBool();

        public BindableLong TotalScore { get; } = new BindableLong();
        public BindableDouble Accuracy { get; } = new BindableDouble(1);
        public BindableInt Combo { get; } = new BindableInt();
        public BindableBool HasQuit { get; } = new BindableBool();
        public Bindable<int?> ScorePosition { get; } = new Bindable<int?>();
        public Bindable<long> DisplayOrder { get; } = new Bindable<long>();

        /// <summary>
        /// oily: the <see cref="BackgroundOpacity"/> that gives lazer's own look.
        /// </summary>
        public const float DEFAULT_BACKGROUND_OPACITY = 0.33f;

        // oily: the score's mods, shown when the leaderboard's "Show mods" setting is on.
        public IReadOnlyList<Mod> Mods { get; }
        public BindableBool ShowMods { get; } = new BindableBool();
        private FillFlowContainer modsFlow = null!;

        // oily: the leaderboard's own colours and background opacity, from its skin settings.
        public BindableBool UseCustomColours { get; } = new BindableBool();
        public BindableColour4 CustomColour { get; } = new BindableColour4();
        public BindableColour4 CustomPlayerColour { get; } = new BindableColour4();
        public BindableFloat BackgroundOpacity { get; } = new BindableFloat(DEFAULT_BACKGROUND_OPACITY);

        // oily: leaderboards sorted by pp show it instead of the score.
        public BindableDouble PerformancePoints { get; } = new BindableDouble();
        public BindableBool ShowPerformancePoints { get; } = new BindableBool();

        private Func<ScoringMode, long>? getDisplayScoreFunction;

        public Func<ScoringMode, long> GetDisplayScore
        {
            set => getDisplayScoreFunction = value;
        }

        public Color4? BackgroundColour { get; }

        public IUser? User { get; }

        /// <summary>
        /// Whether this score is the local user or a replay player (and should be focused / always visible).
        /// </summary>
        public readonly bool Tracked;

        private FillFlowContainer scorePanel = null!;
        private Container leftLayer = null!;
        private Box leftLayerGradient = null!;
        private Container rightLayer = null!;
        private Box rightLayerGradient = null!;
        private Container scoreComponents = null!;
        private OsuSpriteText usernameText = null!;
        private OsuSpriteText positionText = null!;
        private OsuSpriteText accuracyText = null!;
        private OsuSpriteText scoreText = null!;
        private OsuSpriteText comboText = null!;

        private IBindable<ScoringMode> scoreDisplayMode = null!;

        private bool isFriend;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly LayoutValue drawSizeLayout = new LayoutValue(Invalidation.DrawSize);

        /// <summary>
        /// Creates a new <see cref="DrawableGameplayLeaderboardScore"/>.
        /// </summary>
        public DrawableGameplayLeaderboardScore(GameplayLeaderboardScore score)
        {
            User = score.User;
            Tracked = score.Tracked;
            TotalScore.BindTo(score.TotalScore);
            Accuracy.BindTo(score.Accuracy);
            Combo.BindTo(score.Combo);
            HasQuit.BindTo(score.HasQuit);
            ScorePosition.BindTo(score.Position);
            DisplayOrder.BindTo(score.DisplayOrder);
            PerformancePoints.BindTo(score.PerformancePoints);
            ShowPerformancePoints.BindTo(score.ShowPerformancePoints);
            GetDisplayScore = score.GetDisplayScore;
            Mods = score.Mods;

            if (score.TeamColour != null)
                BackgroundColour = score.TeamColour.Value;

            RelativeSizeAxes = Axes.X;
            Height = PANEL_HEIGHT;

            Shear = OsuGame.SHEAR;

            AddLayout(drawSizeLayout);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            const float corner_radius = 10;

            Container avatarLayer;

            InternalChild = scorePanel = new FillFlowContainer
            {
                CornerRadius = corner_radius,
                BorderThickness = 2f,
                Masking = true,
                AutoSizeAxes = Axes.X,
                RelativeSizeAxes = Axes.Y,
                Children = new[]
                {
                    // Apparently this whole dual layer thing is here because the design apparently called
                    // for a different colour to the left opposed to the right.
                    //
                    // I don't know this makes much visual sense. If it ever becomes an issue, rip it out
                    // and replace with a single gradient instead.
                    leftLayer = new Container
                    {
                        Width = regular_left_panel_width,
                        RelativeSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            leftLayerGradient = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            new Container
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Width = regular_left_panel_width,
                                // This may not be mathematically accurate but the position text looks best aligned with it.
                                Padding = new MarginPadding { Right = avatar_size / 2 - SHEAR_WIDTH / 2 },
                                RelativeSizeAxes = Axes.Y,
                                Child = positionText = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                    Shear = -OsuGame.SHEAR,
                                }
                            }
                        },
                    },
                    // this is placed here between the left and right layer for layout purposes,
                    // but it's proxied below to render in front of them.
                    avatarLayer = new Container
                    {
                        Size = new Vector2(avatar_size),
                        // precise padding so the avatar's top and bottom sides land as close to the panel borders as possible.
                        Padding = new MarginPadding(1.3f),
                        // negative left margin to place the avatar's center directly at the edge of the left layer.
                        Margin = new MarginPadding { Left = -avatar_size / 2 },
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = corner_radius,
                            Masking = true,
                            Child = new ScoreAvatar(User)
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Shear = -OsuGame.SHEAR,
                                // extra scaling to cover the entire sheared area.
                                Scale = new Vector2(1.1f),
                            },
                        },
                    },
                    rightLayer = new Container
                    {
                        RelativeSizeAxes = Axes.Y,
                        // negative left margin to make the X position of the right layer directly at the avatar center (rendered behind it).
                        Margin = new MarginPadding { Left = -avatar_size / 2 },
                        Children = new Drawable[]
                        {
                            rightLayerGradient = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            scoreComponents = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Left = avatar_size / 2 + 4, Right = 20, Vertical = 5 },
                                Shear = -OsuGame.SHEAR,
                                Children = new Drawable[]
                                {
                                    new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        ColumnDimensions = new[]
                                        {
                                            new Dimension(),
                                            new Dimension(GridSizeMode.AutoSize),
                                        },
                                        RowDimensions = new[]
                                        {
                                            new Dimension(GridSizeMode.AutoSize),
                                        },
                                        Content = new[]
                                        {
                                            new[]
                                            {
                                                usernameText = new TruncatingSpriteText
                                                {
                                                    Anchor = Anchor.BottomLeft,
                                                    Origin = Anchor.BottomLeft,
                                                    Text = User?.Username ?? string.Empty,
                                                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                                    RelativeSizeAxes = Axes.X,
                                                },
                                                accuracyText = new OsuSpriteText
                                                {
                                                    Anchor = Anchor.BottomLeft,
                                                    Origin = Anchor.BottomLeft,
                                                    Font = OsuFont.Style.Caption2.With(weight: FontWeight.SemiBold),
                                                },
                                            }
                                        },
                                    },
                                    new GridContainer
                                    {
                                        Anchor = Anchor.BottomLeft,
                                        Origin = Anchor.BottomLeft,
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        ColumnDimensions = new[]
                                        {
                                            new Dimension(),
                                            new Dimension(GridSizeMode.AutoSize),
                                            new Dimension(GridSizeMode.AutoSize),
                                        },
                                        RowDimensions = new[]
                                        {
                                            new Dimension(GridSizeMode.AutoSize),
                                        },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                scoreText = new TruncatingSpriteText
                                                {
                                                    Anchor = Anchor.BottomLeft,
                                                    Origin = Anchor.BottomLeft,
                                                    Font = OsuFont.Style.Body.With(weight: FontWeight.Regular),
                                                    RelativeSizeAxes = Axes.X,
                                                },
                                                // oily: the score's mods, as small badges.
                                                modsFlow = new FillFlowContainer
                                                {
                                                    Anchor = Anchor.BottomRight,
                                                    Origin = Anchor.BottomRight,
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(1),
                                                    Margin = new MarginPadding { Horizontal = 4, Bottom = 1 },
                                                    ChildrenEnumerable = Mods.Select(m => new ModSwitchTiny(m)
                                                    {
                                                        Active = { Value = true },
                                                        Scale = new Vector2(0.4f),
                                                    }),
                                                },
                                                comboText = new OsuSpriteText
                                                {
                                                    Anchor = Anchor.BottomRight,
                                                    Origin = Anchor.BottomRight,
                                                    Font = OsuFont.Style.Caption2.With(weight: FontWeight.SemiBold),
                                                },
                                            }
                                        },
                                    },
                                },
                            }
                        }
                    },
                    avatarLayer.CreateProxy(),
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            isFriend = User != null && api.LocalUserState.Friends.Any(u => User.OnlineID == u.TargetID);

            scoreDisplayMode = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode);
            scoreDisplayMode.BindValueChanged(_ => updateScore());
            TotalScore.BindValueChanged(_ => updateScore(), true);
            PerformancePoints.BindValueChanged(_ => updateScore());
            ShowPerformancePoints.BindValueChanged(_ => updateScore(), true);

            Accuracy.BindValueChanged(v => accuracyText.Text = v.NewValue.FormatAccuracy(), true);

            Combo.BindValueChanged(v => comboText.Text = $@"{v.NewValue}x", true);

            Expanded.BindValueChanged(onExpanded, true);

            HasQuit.BindValueChanged(_ => updatePanelState());
            ScorePosition.BindValueChanged(_ => updatePanelState(), true);

            ShowMods.BindValueChanged(v => modsFlow.Alpha = v.NewValue ? 1 : 0, true);

            UseCustomColours.BindValueChanged(_ => updatePanelState());
            CustomColour.BindValueChanged(_ => updatePanelState());
            CustomPlayerColour.BindValueChanged(_ => updatePanelState());
            BackgroundOpacity.BindValueChanged(_ => updatePanelState(), true);

            FinishTransforms(true);
        }

        private void updateScore() => scoreText.Text = ShowPerformancePoints.Value
            ? $@"{Math.Round(PerformancePoints.Value):N0}pp"
            : (getDisplayScoreFunction?.Invoke(scoreDisplayMode.Value) ?? TotalScore.Value).ToString("N0");

        private void onExpanded(ValueChangedEvent<bool> expanded)
        {
            if (expanded.NewValue)
            {
                rightLayer.ResizeWidthTo(computeRightLayerWidth(), panel_transition_duration, Easing.OutQuint);
                scoreComponents.FadeIn(panel_transition_duration, Easing.OutQuint);
            }
            else
            {
                rightLayer.ResizeWidthTo(avatar_size / 2, panel_transition_duration, Easing.OutQuint);
                scoreComponents.FadeOut(text_transition_duration, Easing.OutQuint);
            }
        }

        private void updatePanelState()
        {
            positionText.Text = ScorePosition.Value.HasValue ? $"#{ScorePosition.Value.Value.FormatRank()}" : "-";

            Color4 usernameColour = Color4.White;
            bool widthExtension = false;

            if (HasQuit.Value)
            {
                setPanelColour(Color4.Gray);
                usernameColour = colours.Red2;
            }
            else if (UseCustomColours.Value)
            {
                // oily: the leaderboard's own colours. First place keeps its wider panel.
                widthExtension = Tracked || ScorePosition.Value == 1;
                setPanelColour(Tracked ? CustomPlayerColour.Value : CustomColour.Value);
            }
            else if (ScorePosition.Value == 1)
            {
                widthExtension = true;
                setPanelColour(BackgroundColour ?? colours.Lime2);
            }
            else if (Tracked)
            {
                widthExtension = true;
                setPanelColour(BackgroundColour ?? colours.Orange2);
            }
            else if (isFriend)
            {
                setPanelColour(BackgroundColour ?? colours.Pink1);
                usernameColour = colours.Pink1;
            }
            else
                setPanelColour(BackgroundColour ?? colours.Blue4);

            usernameText.FadeColour(usernameColour, text_transition_duration, Easing.OutQuint);

            scorePanel.MoveToX(widthExtension ? 0 : left_panel_extension_width, panel_transition_duration, Easing.OutElastic);
            leftLayer.ResizeWidthTo(widthExtension ? extended_left_panel_width : regular_left_panel_width, panel_transition_duration, Easing.OutElastic);
        }

        private void setPanelColour(Color4 baseColour)
        {
            // oily: the background opacity setting scales lazer's own opacities; 1 makes the panel solid.
            float scale = BackgroundOpacity.Value / DEFAULT_BACKGROUND_OPACITY;
            float opacity(float lazer) => Math.Clamp(lazer * scale, 0, 1);

            leftLayerGradient.Colour = ColourInfo.GradientVertical(baseColour.Opacity(opacity(0.2f)), baseColour.Opacity(opacity(0.5f)));
            rightLayerGradient.Colour = ColourInfo.GradientVertical(baseColour.Opacity(opacity(0.1f)), baseColour.Opacity(opacity(0.3f)));
            scorePanel.BorderColour = ColourInfo.GradientVertical(baseColour.Opacity(opacity(0.2f)), baseColour.Opacity(opacity(1)));
        }

        protected override void Update()
        {
            base.Update();

            if (!drawSizeLayout.IsValid)
            {
                if (Expanded.Value)
                {
                    rightLayer.ClearTransforms(targetMember: nameof(Width));
                    rightLayer.Width = computeRightLayerWidth();
                }

                drawSizeLayout.Validate();
            }

            bool showAccuracyAndCombo = rightLayer.Width >= accuracy_combo_width_cutoff;

            accuracyText.Alpha = showAccuracyAndCombo ? 1 : 0;
            comboText.Alpha = showAccuracyAndCombo ? 1 : 0;

            bool showUsernameAndScore = rightLayer.Width >= username_score_width_cutoff;

            usernameText.Alpha = showUsernameAndScore ? 1 : 0;
            scoreText.Alpha = showUsernameAndScore ? 1 : 0;
        }

        private float computeRightLayerWidth() => Math.Max(0, DrawWidth - extended_left_panel_width - avatar_size / 2);

        private partial class ScoreAvatar : CompositeDrawable
        {
            private readonly IUser? user;

            private Box placeholder = null!;

            public ScoreAvatar(IUser? user)
            {
                this.user = user;

                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = placeholder = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(0.1f),
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                LoadComponentAsync(new DrawableAvatar(user), a =>
                {
                    placeholder.FadeOut(300, Easing.InQuint);
                    AddInternal(a);
                });
            }
        }
    }
}
