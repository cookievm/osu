// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
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

        /// <summary>A difficulty (stable's blue cards).</summary>
        Difficulty,

        /// <summary>A group header (stable's dark blue collection cards).</summary>
        Group,
    }

    /// <summary>
    /// oily: a song select card drawn exactly like osu!stable's, using stable's own layout numbers (as recreated by McOsu):
    /// <list type="bullet">
    /// <item>menu-button-background is fitted into 699x103 (doubled for @2x) and scaled to stable's 64 osu!pixel button height,
    /// then drawn whole, centred on the card. Its 9px margin is the part of the picture the cards overlap with.</item>
    /// <item>Text sizes and places are fractions of the card's height: title 22%, artist // mapper 14%, difficulty 18% bold, stars at 85%.</item>
    /// <item>Skins v2.2+ get the beatmap's background as a thumbnail on the left (4:3, card height).</item>
    /// <item>Cards are tinted by state (pink set, blue difficulty, white selected) and the text and stars use the skin's
    /// SongSelectActiveText / SongSelectInactiveText.</item>
    /// </list>
    /// </summary>
    public partial class StablePanelContent : CompositeDrawable
    {
        // Stable's card colours (alpha included).
        public static readonly Color4 SET_COLOUR = new Color4(235, 73, 153, 240);
        public static readonly Color4 DIFFICULTY_COLOUR = new Color4(0, 150, 236, 255);
        public static readonly Color4 GROUP_COLOUR = new Color4(35, 50, 143, 255);
        public static readonly Color4 GROUP_ACTIVE_COLOUR = new Color4(163, 240, 44, 255);
        public static readonly Color4 ACTIVE_COLOUR = new Color4(255, 255, 255, 230);

        /// <summary>
        /// Stable's card picture box (menu-button-background is fitted into this, in 1x pixels).
        /// </summary>
        private static readonly Vector2 base_size = new Vector2(699, 103);

        /// <summary>
        /// Stable's button height: 64 osu!pixels (640x480) is 102.4 in lazer's 1024x768 units.
        /// </summary>
        private const float button_height = 64 * 768f / 480;

        private const float margin = 9;

        /// <summary>
        /// How tall a stable card's visible area is with stable's default picture size, in lazer's units. Cards are spaced by this.
        /// </summary>
        public static readonly float DEFAULT_CARD_HEIGHT = cardSize(base_size, 1).actual.Y;

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

        private bool insideSet;

        /// <summary>
        /// A difficulty inside an opened set. Stable draws its title and artist faintly (20%) unless it's selected.
        /// </summary>
        public bool InsideSet
        {
            get => insideSet;
            set
            {
                insideSet = value;
                updateColours(false);
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
        /// The card's beatmap: its background is the thumbnail, and (on difficulties) its SS and simulated pp are shown.
        /// </summary>
        public BeatmapInfo? Beatmap
        {
            set
            {
                performance.Beatmap = value;
                thumbnail.Beatmap.Value = value;
            }
        }

        /// <summary>
        /// The difficulty's lazer star rating and pp, used for pp outside song select.
        /// </summary>
        public StarDifficulty Difficulty
        {
            set => performance.Difficulty = value;
        }

        /// <summary>
        /// The card's visible area (the picture minus its margins). Everything is laid out in here, like stable.
        /// </summary>
        private readonly Container actualArea;

        private readonly Container backgroundContainer;
        private readonly Container thumbnailContainer;
        private readonly UpdateableBeatmapBackgroundSprite thumbnail;
        private readonly Container textContainer;
        private readonly OsuSpriteText titleText;
        private readonly OsuSpriteText subtitleText;
        private readonly FillFlowContainer difficultyLine;
        private readonly OsuSpriteText difficultyText;
        private readonly Container starContainer;
        private readonly PanelPerformanceDisplay performance;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        private Texture? starTexture;
        private Color4 activeText = Color4.Black;
        private Color4 inactiveText = Color4.White;
        private bool showThumbnail;

        public StablePanelContent()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = actualArea = new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Children = new Drawable[]
                {
                    backgroundContainer = new Container(),
                    thumbnailContainer = new Container
                    {
                        Masking = true,
                        Alpha = 0,
                        Child = thumbnail = new UpdateableBeatmapBackgroundSprite
                        {
                            RelativeSizeAxes = Axes.Both,
                            BackgroundLoadDelay = 100,
                        },
                    },
                    textContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            titleText = new OsuSpriteText { Shadow = false },
                            subtitleText = new OsuSpriteText { Shadow = false },
                            difficultyLine = new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(10, 0),
                                Children = new Drawable[]
                                {
                                    difficultyText = new OsuSpriteText { Shadow = false },
                                    // oily (ilo, 2026-11-26): SS pp and simulated pp are always on stable difficulty cards, after the difficulty name.
                                    performance = new PanelPerformanceDisplay
                                    {
                                        AlwaysShow = true,
                                        PlainColour = true,
                                    },
                                },
                            },
                            starContainer = new Container(),
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

        /// <summary>
        /// Stable's sizes for a card picture of <paramref name="pixels"/> (its real pixels) at <paramref name="density"/> (2 for @2x):
        /// the whole picture's drawn size, and its visible area once the margins are taken off.
        /// </summary>
        private static (Vector2 drawn, Vector2 offset, Vector2 actual) cardSize(Vector2 pixels, float density)
        {
            Vector2 minimum = base_size * density;
            float minimumScale = Math.Min(minimum.X / pixels.X, minimum.Y / pixels.Y);
            Vector2 fitted = pixels * minimumScale;
            float scale = Math.Max(button_height / fitted.X, button_height / fitted.Y);

            Vector2 drawn = pixels * scale;
            var offset = new Vector2(margin * scale * density, (2 * margin + pixels.Y / density - base_size.Y) / 2 * scale * density);

            return (drawn, offset, drawn - offset * 2);
        }

        private void onSkinChanged()
        {
            var cardTexture = skin.GetTexture("menu-button-background");

            var (drawn, offset, actual) = cardTexture != null
                ? cardSize(new Vector2(cardTexture.Width, cardTexture.Height), cardTexture.ScaleAdjust)
                : cardSize(base_size, 1);

            actualArea.Size = actual;
            backgroundContainer.Position = -offset;
            backgroundContainer.Size = drawn;

            if (cardTexture != null)
            {
                backgroundContainer.Child = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = cardTexture,
                };
            }
            else
            {
                // Stable's default card: a see-through panel with a light edge, tinted by the card's state.
                backgroundContainer.Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = offset.X, Vertical = offset.Y },
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
                            Colour = Color4.White.Opacity(0.7f),
                        },
                    },
                };
            }

            starTexture = skin.GetTexture("star");

            activeText = skinColour("SongSelectActiveText") ?? Color4.Black;
            inactiveText = skinColour("SongSelectInactiveText") ?? Color4.White;

            // Stable shows thumbnails for skins made for version 2.2 and up (lazer treats skins without a version as the newest).
            decimal version = skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value ?? 2.7m;
            showThumbnail = cardTexture != null && version >= 2.2m;

            updateLayout();
        }

        private Color4? skinColour(string name) => skin.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(name))?.Value;

        private void updateLayout()
        {
            if (!IsLoaded)
                return;

            Vector2 size = actualArea.Size;
            float h = size.Y;

            // Stable's text starts after the thumbnail (v2.2+ skins), or 4% in.
            float textOffset = showThumbnail && kind != StablePanelKind.Group ? h * 4 / 3 + size.X * 0.02f : size.X * 0.04f;

            thumbnailContainer.Alpha = showThumbnail && kind != StablePanelKind.Group ? 1 : 0;
            thumbnailContainer.Size = new Vector2(h * 4 / 3, h);

            // Font sizes are a little bigger than stable's line heights, so the letters come out the same height.
            const float font_scale = 1.2f;

            if (kind == StablePanelKind.Group)
            {
                titleText.Font = OsuFont.GetFont(size: h * 0.35f * font_scale);
                titleText.Anchor = Anchor.CentreLeft;
                titleText.Origin = Anchor.CentreLeft;
                titleText.Position = new Vector2(size.X * 0.02f, 0);
            }
            else
            {
                titleText.Font = OsuFont.GetFont(size: h * 0.22f * font_scale);
                titleText.Anchor = Anchor.TopLeft;
                titleText.Origin = Anchor.TopLeft;
                titleText.Position = new Vector2(textOffset, h * 0.075f - h * 0.03f);
            }

            subtitleText.Font = OsuFont.GetFont(size: h * 0.14f * font_scale);
            subtitleText.Position = new Vector2(textOffset, h * (0.075f + 0.22f + 0.075f) - h * 0.02f);

            difficultyText.Font = OsuFont.GetFont(size: h * 0.18f * font_scale, weight: FontWeight.Bold);
            difficultyLine.Position = new Vector2(textOffset, h * (0.075f + 0.22f + 0.075f + 0.14f * 0.85f + 0.075f) - h * 0.02f);

            starContainer.Position = new Vector2(textOffset, h * 0.85f);

            subtitleText.Alpha = kind == StablePanelKind.Group ? 0 : 1;
            difficultyLine.Alpha = kind == StablePanelKind.Difficulty ? 1 : 0;

            updateStars();
            updateColours(false);
        }

        private void updateColours(bool animated)
        {
            Color4 cardColour = kind switch
            {
                // Stable only tints difficulties blue inside an opened set; a difficulty on its own (grouped by difficulty) is a pink set card.
                StablePanelKind.Difficulty => active ? ACTIVE_COLOUR : insideSet ? DIFFICULTY_COLOUR : SET_COLOUR,
                StablePanelKind.Group => active ? GROUP_ACTIVE_COLOUR : GROUP_COLOUR,
                _ => active ? ACTIVE_COLOUR : SET_COLOUR,
            };

            // Matches stable with skins that set these colours (checked against WhiteCat in osu!stable, 2026-11-27): the selected card uses
            // SongSelectActiveText, difficulties inside an opened set use SongSelectInactiveText, and set cards and group headers stay white.
            Color4 text = active ? activeText : kind == StablePanelKind.Difficulty && insideSet ? inactiveText : Color4.White;
            double duration = animated ? 200 : 0;

            backgroundContainer.FadeColour(cardColour, duration, Easing.OutQuint);
            textContainer.FadeColour(text, duration, Easing.OutQuint);

            // Difficulties inside an opened set draw their title and artist faintly.
            float titleAlpha = insideSet && !active ? 0.2f : 1;
            titleText.Alpha = titleAlpha;
            subtitleText.Alpha = kind == StablePanelKind.Group ? 0 : titleAlpha;
        }

        private void updateStars()
        {
            starContainer.Clear();

            if (stars == null || kind != StablePanelKind.Difficulty || !IsLoaded)
                return;

            float h = actualArea.Height;
            float starWidth = h * 0.2f;
            double clamped = Math.Clamp(stars.Value, 0, 25);
            int fullStars = (int)clamped;
            float partial = Math.Max(0.5f, (float)(clamped - fullStars));

            // Full stars, slightly bigger the more there are (like stable: +0.015 on the picture's scale per star, up to 17.5% bigger).
            float starScale = starWidth / (starTexture?.Height ?? 50);

            for (int i = 0; i < fullStars; i++)
                starContainer.Add(createStar(i, starWidth, Math.Min(1.175f, 1 + i * 0.015f / starScale), 1));

            // The last part of a star, at least half size.
            if (clamped > fullStars)
                starContainer.Add(createStar(fullStars, starWidth, partial, 1));

            // Faint stars fill the row up to 10.
            for (int i = fullStars + 1; i < max_stars; i++)
                starContainer.Add(createStar(i, starWidth, 0.6f, 30 / 255f));
        }

        private Drawable createStar(int index, float starWidth, float scale, float alpha)
        {
            Drawable star = starTexture != null
                ? new Sprite
                {
                    Texture = starTexture,
                    Size = new Vector2(starWidth),
                }
                : new SpriteIcon
                {
                    Icon = FontAwesome.Solid.Star,
                    Size = new Vector2(starWidth * 0.8f),
                };

            star.Anchor = Anchor.TopLeft;
            star.Origin = Anchor.Centre;
            star.Position = new Vector2(starWidth / 2 + index * starWidth * 1.75f, 0);
            star.Scale = new Vector2(scale);
            star.Alpha = alpha;

            return star;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }
    }
}
