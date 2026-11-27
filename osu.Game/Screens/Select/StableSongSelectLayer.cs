// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily (ilo, 2026-11-27): the parts of osu!stable's song select on the left of the screen, placed exactly where stable puts them
    /// (measured from stable at 2560x1440 and drawn in stable's 768 high space): the ranked status icon and beatmap info, the "Global Ranking" box,
    /// the beatmap page button and the "No records set!" box. Shown in the Stable "Menu UI".
    /// </summary>
    public partial class StableSongSelectLayer : CompositeDrawable
    {
        [Resolved]
        private SongSelectPieceRegistry registry { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> working { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private BeatmapSetOverlay? beatmapOverlay { get; set; }

        private readonly StableSpace space;
        private readonly SpriteIcon statusIcon;
        private readonly Container noRecords;

        private readonly IBindable<bool> shown = new BindableBool();
        private readonly IBindable<bool> noRecordsShown = new BindableBool();

        /// <param name="screen">Song select, whose corners stable's screen matches.</param>
        public StableSongSelectLayer(Drawable screen)
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            StableDropdown<StableRanking> ranking;

            InternalChild = space = new StableSpace(screen)
            {
                Children = new Drawable[]
                {
                    statusIcon = new SpriteIcon
                    {
                        Position = new Vector2(9.6f, 9.6f),
                        Size = new Vector2(18.7f),
                    },
                    new StableBeatmapInfo(),
                    new BeatmapPageButton
                    {
                        Position = new Vector2(333, 124),
                        Size = new Vector2(130, 20),
                        Action = showBeatmapPage,
                    },
                    noRecords = new Container
                    {
                        Position = new Vector2(20.8f, 300.3f),
                        Size = new Vector2(335.5f, 38.9f),
                        Alpha = 0,
                        Masking = true,
                        BorderThickness = 4.2f,
                        BorderColour = Color4.White,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = new Color4(20, 20, 20, 255),
                            },
                            new SpriteIcon
                            {
                                Icon = FontAwesome.Solid.Trophy,
                                Position = new Vector2(13.3f, 10),
                                Size = new Vector2(22.9f, 20.3f),
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Left = 36.3f },
                                Child = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = "No records set!",
                                    Font = StableFont.Get(StableFont.NO_RECORDS_SIZE),
                                    Colour = new Color4(0, 216, 255, 255),
                                },
                            },
                        },
                    },
                    // Last, so its menu opens over the rest.
                    ranking = new StableDropdown<StableRanking>
                    {
                        Position = new Vector2(8, 117.3f),
                        Width = 306.7f,
                        HeaderHeight = 23.5f,
                        FontSize = StableFont.RANKING_SIZE,
                        BorderWidth = 1.2f,
                        AccentColour = new Color4(28, 134, 184, 255),
                        Items = Enum.GetValues<StableRanking>(),
                        TextFor = r => r.GetDescription(),
                    },
                },
            };

            ranking.Current = rankingChoice;
        }

        private readonly Bindable<StableRanking> rankingChoice = new Bindable<StableRanking>();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            rankingChoice.BindTo(registry.StableRanking);

            shown.BindTo(registry.StableTopBar);
            shown.BindValueChanged(v => this.FadeTo(v.NewValue ? 1 : 0, 200, Easing.OutQuint), true);

            noRecordsShown.BindTo(registry.StableNoRecords);
            noRecordsShown.BindValueChanged(v => noRecords.FadeTo(v.NewValue ? 1 : 0, 200, Easing.OutQuint), true);

            working.BindValueChanged(_ => updateStatus(), true);
        }

        private void updateStatus()
        {
            var status = working.Value.BeatmapSetInfo.Status;

            // Stable's status icons.
            switch (status)
            {
                case BeatmapOnlineStatus.Ranked:
                case BeatmapOnlineStatus.Approved:
                    statusIcon.Icon = FontAwesome.Solid.AngleDoubleUp;
                    statusIcon.Colour = new Color4(102, 204, 255, 255);
                    break;

                case BeatmapOnlineStatus.Qualified:
                    statusIcon.Icon = FontAwesome.Solid.Check;
                    statusIcon.Colour = new Color4(102, 204, 255, 255);
                    break;

                case BeatmapOnlineStatus.Loved:
                    statusIcon.Icon = FontAwesome.Solid.Heart;
                    statusIcon.Colour = new Color4(255, 102, 170, 255);
                    break;

                default:
                    statusIcon.Icon = FontAwesome.Solid.QuestionCircle;
                    statusIcon.Colour = new Color4(170, 170, 170, 255);
                    break;
            }

            statusIcon.Alpha = working.IsDefault ? 0 : 1;
        }

        private void showBeatmapPage()
        {
            int? id = working.Value.BeatmapInfo.OnlineID;

            if (id > 0)
                beatmapOverlay?.FetchAndShowBeatmap(id.Value);
        }

        /// <summary>
        /// Stable's "Beatmap page" button: its chat icon, and the words painted in the skin's songselect-top.
        /// </summary>
        private partial class BeatmapPageButton : OsuClickableContainer
        {
            private readonly SpriteIcon icon;

            public BeatmapPageButton()
            {
                Child = icon = new SpriteIcon
                {
                    Icon = FontAwesome.Solid.Comments,
                    Position = new Vector2(3, 4),
                    Size = new Vector2(12.3f, 11.7f),
                    Colour = new Color4(168, 119, 87, 255),
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                icon.FadeColour(Color4.White, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                icon.FadeColour(new Color4(168, 119, 87, 255), 100);
                base.OnHoverLost(e);
            }
        }
    }
}
