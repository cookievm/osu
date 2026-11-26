// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: a leaderboard row drawn like osu!stable's: grade, name, "Score: 1,234,567 (999x)", then mods and accuracy on the right.
    /// The grade uses the skin's ranking-X-small pictures when it has them.
    /// </summary>
    public partial class StableLeaderboardScore : CompositeDrawable
    {
        private readonly ScoreInfo score;
        private readonly int? position;
        private readonly bool own;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        private readonly Container gradeContainer;
        private readonly Box background;
        private readonly OsuSpriteText scoreText;

        private Bindable<string> totalScore = null!;

        public StableLeaderboardScore(ScoreInfo score, int? position, bool own, bool sheared)
        {
            this.score = score;
            this.position = position;
            this.own = own;

            RelativeSizeAxes = Axes.Both;
            Shear = sheared ? -OsuGame.SHEAR : Vector2.Zero;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = own ? new Color4(80, 60, 0, 255) : Color4.Black,
                    Alpha = 0.5f,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Padding = new MarginPadding { Left = 6 },
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Width = 24,
                            Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                            Text = position?.ToString() ?? string.Empty,
                            Colour = Color4.White.Opacity(0.6f),
                        },
                        gradeContainer = new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(34, 36),
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = score.User.Username,
                                    Font = OsuFont.GetFont(size: 17, weight: FontWeight.Bold),
                                },
                                scoreText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 14),
                                },
                            },
                        },
                    },
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Right = 8 },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            ChildrenEnumerable = score.Mods.AsOrdered().Select(m => new ModIcon(m, showTooltip: false)
                            {
                                Scale = new Vector2(0.25f),
                            }),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Text = score.DisplayAccuracy,
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            totalScore = scoreManager.GetBindableTotalScoreString(score);
            totalScore.BindValueChanged(s => scoreText.Text = $"Score: {s.NewValue} ({score.MaxCombo:N0}x)", true);

            skin.SourceChanged += onSkinChanged;
            onSkinChanged();
        }

        public void SetHovered(bool hovered) => background.FadeTo(hovered ? 0.7f : 0.5f, 200, Easing.OutQuint);

        private void onSkinChanged()
        {
            var texture = skin.GetTexture($"ranking-{score.Rank}-small");

            gradeContainer.Child = texture != null
                ? new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Texture = texture,
                }
                : new DrawableRank(score.Rank)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    Height = 18,
                };
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }
    }
}
