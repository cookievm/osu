// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: what kind of card a <see cref="StablePanelContent"/> is drawn as. Stable colours each kind differently.
    /// </summary>
    public enum StablePanelKind
    {
        /// <summary>A beatmap set (stable's pink cards).</summary>
        BeatmapSet,

        /// <summary>A difficulty inside an opened set (stable's blue cards).</summary>
        Difficulty,

        /// <summary>A group header (stable's dark grey cards).</summary>
        Group,
    }

    /// <summary>
    /// oily: a song select card drawn like osu!stable's.
    /// The skin's menu-button-background is drawn at its own size (stable's 1024x768 units, the same as lazer's), from the card's left edge,
    /// so it looks exactly as it does in stable: nothing is stretched or cut, and it overlaps the cards around it like stable's does.
    /// It is tinted by the card's state like stable (pink set, blue difficulty, white selected), and the text uses the skin's
    /// SongSelectActiveText / SongSelectInactiveText colours. Without a menu-button-background, stable's default look is drawn.
    /// </summary>
    public partial class StablePanelContent : CompositeDrawable
    {
        // Stable's own card colours.
        public static readonly Color4 SET_COLOUR = new Color4(235, 73, 153, 255);
        public static readonly Color4 DIFFICULTY_COLOUR = new Color4(0, 150, 236, 255);
        public static readonly Color4 GROUP_COLOUR = new Color4(60, 60, 60, 255);
        public static readonly Color4 ACTIVE_COLOUR = Color4.White;

        /// <summary>
        /// Stable's card picture size (the wiki's minimum for menu-button-background), used when the skin has none.
        /// </summary>
        public static readonly Vector2 DEFAULT_CARD_SIZE = new Vector2(690, 85);

        private const int max_stars = 10;

        // Stable's star.png is 50x50 and drawn at a bit under half size on the cards.
        private const float star_size = 50 * 0.4f;

        /// <summary>
        /// Stable's second line: "Artist // Mapper".
        /// </summary>
        public static LocalisableString ArtistAndMapper(IBeatmapMetadataInfo metadata) =>
            LocalisableString.Interpolate($"{new RomanisableString(metadata.ArtistUnicode, metadata.Artist)} // {metadata.Author.Username}");

        public static LocalisableString TitleOf(IBeatmapMetadataInfo metadata) => new RomanisableString(metadata.TitleUnicode, metadata.Title);

        private StablePanelKind kind;

        public StablePanelKind Kind
        {
            get => kind;
            set
            {
                kind = value;
                updateLayout();
            }
        }

        private bool active;

        /// <summary>
        /// Selected (or an opened set). Stable draws these white with the active text colour.
        /// </summary>
        public bool Active
        {
            get => active;
            set
            {
                if (active == value)
                    return;

                active = value;
                updateColours(true);
            }
        }

        public LocalisableString Title
        {
            set => titleText.Text = value;
        }

        public LocalisableString Subtitle
        {
            set => subtitleText.Text = value;
        }

        public LocalisableString DifficultyName
        {
            set => difficultyText.Text = value;
        }

        private double? stars;

        /// <summary>
        /// The star rating shown as a row of stars, or null for none.
        /// </summary>
        public double? Stars
        {
            get => stars;
            set
            {
                stars = value;
                updateStars();
            }
        }

        /// <summary>
        /// The difficulty's beatmap, for the SS pp and simulated pp (always shown on stable difficulty cards).
        /// </summary>
        public BeatmapInfo? Beatmap
        {
            set => performance.Beatmap = value;
        }

        /// <summary>
        /// The difficulty's lazer star rating and pp, used for pp outside song select.
        /// </summary>
        public StarDifficulty Difficulty
        {
            set => performance.Difficulty = value;
        }

        /// <summary>
        /// The card picture and everything on it, at the picture's own size.
        /// </summary>
        private readonly Container card;

        private readonly Container backgroundContainer;
        private readonly FillFlowContainer textFlow;
        private readonly OsuSpriteText titleText;
        private readonly OsuSpriteText subtitleText;
        private readonly OsuSpriteText difficultyText;
        private readonly FillFlowContainer starFlow;
        private readonly PanelPerformanceDisplay performance;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        private Texture? starTexture;
        private Color4 activeText = Color4.Black;
        private Color4 inactiveText = Color4.White;

        public StablePanelContent()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = card = new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = DEFAULT_CARD_SIZE,
                Children = new Drawable[]
                {
                    backgroundContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    textFlow = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        // Stable puts its text (or thumbnail) 9px in from the picture's left edge.
                        Position = new Vector2(9 + 6, 7),
                        Children = new Drawable[]
                        {
                            titleText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 21),
                                Shadow = false,
                            },
                            subtitleText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 15),
                                Shadow = false,
                            },
                            difficultyText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                                Shadow = false,
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Children = new Drawable[]
                                {
                                    starFlow = new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                    },
                                    // oily (ilo, 2026-11-26): SS pp and simulated pp are always on stable cards.
                                    performance = new PanelPerformanceDisplay
                                    {
                                        AlwaysShow = true,
                                        PlainColour = true,
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            onSkinChanged();
            updateLayout();
        }

        private void onSkinChanged()
        {
            var cardTexture = skin.GetTexture("menu-button-background");

            if (cardTexture != null)
            {
                // Drawn at its own size, exactly like stable. Lazer's UI uses the same 1024x768 units as stable, so no extra scaling.
                card.Size = new Vector2(cardTexture.DisplayWidth, cardTexture.DisplayHeight);
                backgroundContainer.Child = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = cardTexture,
                };
            }
            else
            {
                // Stable's default card: a see-through panel with a light edge, tinted by the card's state.
                card.Size = DEFAULT_CARD_SIZE;
                backgroundContainer.Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Vertical = 8 },
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 4,
                        BorderThickness = 2.5f,
                        BorderColour = Color4.White.Opacity(0.8f),
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientHorizontal(Color4.White.Opacity(0.85f), Color4.White.Opacity(0.6f)),
                        },
                    },
                };
            }

            starTexture = skin.GetTexture("star");

            activeText = skinColour("SongSelectActiveText") ?? Color4.Black;
            inactiveText = skinColour("SongSelectInactiveText") ?? Color4.White;

            updateStars();
            updateColours(false);
        }

        private Color4? skinColour(string name) => skin.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(name))?.Value;

        private void updateLayout()
        {
            subtitleText.Alpha = kind == StablePanelKind.Group ? 0 : 1;
            difficultyText.Alpha = kind == StablePanelKind.Difficulty ? 1 : 0;
            performance.Alpha = kind == StablePanelKind.Difficulty ? 1 : 0;
            titleText.Font = titleText.Font.With(size: kind == StablePanelKind.Group ? 24 : 21);

            // A group header is one line, so it sits in the middle of the card.
            textFlow.Y = kind == StablePanelKind.Group ? (card.Height - 24) / 2 : 7;

            updateColours(false);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateColours(true);
            return false;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateColours(true);
            base.OnHoverLost(e);
        }

        private void updateColours(bool animated)
        {
            Color4 cardColour = active
                ? ACTIVE_COLOUR
                : kind switch
                {
                    StablePanelKind.Difficulty => DIFFICULTY_COLOUR,
                    StablePanelKind.Group => GROUP_COLOUR,
                    _ => SET_COLOUR,
                };

            // Stable brightens a card under the mouse.
            if (IsHovered && !active)
                cardColour = Interpolation.ValueAt(0.25, cardColour, Color4.White, 0, 1);

            double duration = animated ? 200 : 0;

            backgroundContainer.FadeColour(cardColour, duration, Easing.OutQuint);
            textFlow.FadeColour(active ? activeText : inactiveText, duration, Easing.OutQuint);
            // Stable's stars take the card's tint.
            starFlow.FadeColour(cardColour, duration, Easing.OutQuint);
        }

        private void updateStars()
        {
            starFlow.Clear();

            if (stars == null || kind != StablePanelKind.Difficulty)
            {
                starFlow.Alpha = 0;
                return;
            }

            starFlow.Alpha = 1;

            // Stable (skin v2.2+) shows one star per whole star, then scales down the last one for the rest.
            double remaining = Math.Min(stars.Value, max_stars);

            while (remaining > 0.05)
            {
                float fill = (float)Math.Min(1, remaining);
                starFlow.Add(createStar(fill));
                remaining -= 1;
            }
        }

        private Drawable createStar(float fill)
        {
            Drawable star = starTexture != null
                ? new Sprite
                {
                    Texture = starTexture,
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                }
                : new SpriteIcon
                {
                    Icon = FontAwesome.Solid.Star,
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };

            return new Container
            {
                Size = new Vector2(star_size),
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Scale = new Vector2(fill),
                    Child = star,
                },
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
