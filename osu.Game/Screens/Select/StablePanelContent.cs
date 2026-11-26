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
using osu.Framework.Localisation;
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
    /// Uses the skin's stable images and colours (menu-button-background, star, SongSelectActiveText / SongSelectInactiveText) when the skin has them,
    /// otherwise draws stable's default look.
    /// </summary>
    public partial class StablePanelContent : CompositeDrawable
    {
        // Stable's own card colours.
        public static readonly Color4 SET_COLOUR = new Color4(235, 73, 153, 255);
        public static readonly Color4 DIFFICULTY_COLOUR = new Color4(0, 150, 236, 255);
        public static readonly Color4 GROUP_COLOUR = new Color4(60, 60, 60, 255);
        public static readonly Color4 ACTIVE_COLOUR = Color4.White;

        private const int max_stars = 10;

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
        /// Selected (or an opened set). Stable draws these white with dark text.
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

        private readonly Container backgroundContainer;
        private readonly FillFlowContainer textFlow;
        private readonly OsuSpriteText titleText;
        private readonly OsuSpriteText subtitleText;
        private readonly OsuSpriteText difficultyText;
        private readonly FillFlowContainer starFlow;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        private Texture? starTexture;
        private Color4 activeText = Color4.Black;
        private Color4 inactiveText = Color4.White;

        public StablePanelContent()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                backgroundContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                textFlow = new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Left = 18 },
                    Children = new Drawable[]
                    {
                        titleText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 19, weight: FontWeight.SemiBold),
                            Shadow = false,
                        },
                        subtitleText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 14),
                            Shadow = false,
                        },
                        difficultyText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                            Shadow = false,
                        },
                        starFlow = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(1),
                            Margin = new MarginPadding { Top = 2 },
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
        }

        private void onSkinChanged()
        {
            // Stable's card picture is wide and sticks out past the right edge, so it is drawn at its own aspect ratio from the left.
            var cardTexture = skin.GetTexture("menu-button-background");

            if (cardTexture != null)
            {
                backgroundContainer.Child = new Sprite
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fill,
                    Texture = cardTexture,
                };
            }
            else
            {
                // Stable's default card: a see-through panel with a light edge, tinted by the card's colour.
                backgroundContainer.Child = new Container
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
            titleText.Font = titleText.Font.With(size: kind == StablePanelKind.Group ? 22 : 19);
            updateColours(false);
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

            double duration = animated ? 200 : 0;

            backgroundContainer.FadeColour(cardColour, duration, Easing.OutQuint);
            textFlow.FadeColour(active ? activeText : inactiveText, duration, Easing.OutQuint);
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

            // Stable shows one star per whole star, then a smaller one for the rest.
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
            const float size = 13;

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
                Size = new Vector2(size),
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Scale = new Vector2(0.5f + fill * 0.5f),
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
