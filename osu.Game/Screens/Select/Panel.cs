// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public abstract partial class Panel : PoolableDrawable, ICarouselPanel, IHasContextMenu
    {
        public const float CORNER_RADIUS = 10;

        private const float active_x_offset = 25f;

        protected const float DURATION = 400;

        protected float PanelXOffset { get; init; }

        private Container backgroundContainer = null!;
        private Container iconContainer = null!;

        private Drawable activationFlash = null!;
        private Drawable hoverLayer = null!;

        private Drawable keyboardSelectionLayer = null!;

        private PulsatingBox selectionLayer = null!;

        public Container TopLevelContent { get; private set; } = null!;

        private Container contentPaddingContainer = null!;
        protected Container Content { get; private set; } = null!;

        /// <summary>
        /// oily: the osu!stable look of this card, shown instead of lazer's when the skin's beatmap list style is "Stable".
        /// Cards that set <see cref="SupportsStableLook"/> fill it in.
        /// </summary>
        protected StablePanelContent StableContent { get; private set; } = null!;

        /// <summary>
        /// oily: whether this card has an osu!stable look. Cards without one keep lazer's look.
        /// </summary>
        protected virtual bool SupportsStableLook => false;

        /// <summary>
        /// oily: how tall this card is with the stable look (stable's cards are all the same size).
        /// </summary>
        /// <remarks>
        /// Stable's card pictures (85 tall) overlap each other, so cards are spaced closer than the picture is tall.
        /// </remarks>
        public static readonly float STABLE_HEIGHT = StablePanelContent.DEFAULT_CARD_HEIGHT - BeatmapCarousel.SPACING;

        /// <summary>
        /// oily: how tall this card is with the stable look.
        /// </summary>
        protected virtual float StableHeight => STABLE_HEIGHT;

        /// <summary>
        /// oily: whether the stable look is showing.
        /// </summary>
        protected bool StableLookShown { get; private set; }

        /// <summary>
        /// oily: osu!stable's group header text, like "2 Stars (1,694 maps)".
        /// </summary>
        protected static LocalisableString StableGroupTitle(LocalisableString title, int count)
            => LocalisableString.Interpolate($"{title} ({count:N0} {(count == 1 ? "map" : "maps")})");

        public Drawable Background
        {
            set => backgroundContainer.Child = value;
        }

        public Drawable Icon
        {
            set => iconContainer.Child = value;
        }

        private Color4? accentColour;

        public Color4? AccentColour
        {
            get => accentColour;
            set
            {
                if (value == accentColour)
                    return;

                accentColour = value;
                updateAccentColour();
            }
        }

        public sealed override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            if (item == null)
                return TopLevelContent.ReceivePositionalInputAt(screenSpacePos);

            var inputRectangle = TopLevelContent.DrawRectangle;

            // Cover the gaps introduced by the spacing between panels so that user mis-aims don't result in no-ops.
            inputRectangle = inputRectangle.Inflate(new MarginPadding
            {
                Top = item.CarouselInputLenienceAbove,
                Bottom = item.CarouselInputLenienceBelow,
            });

            return inputRectangle.Contains(TopLevelContent.ToLocalSpace(screenSpacePos));
        }

        [Resolved]
        private BeatmapCarousel? carousel { get; set; }

        // oily: card look set by the skin (see SongSelectCardStyle).
        [Resolved]
        private SongSelectPieceRegistry? pieceRegistry { get; set; }

        private float baseHeight;

        private ColourInfo keyboardSelectionColour;

        [Resolved]
        private OsuColour colours { get; set; } = null!;
        private bool? appliedFlip;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours)
        {
            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;

            RelativeSizeAxes = Axes.X;
            Height = CarouselItem.DEFAULT_HEIGHT;

            InternalChild = TopLevelContent = new Container
            {
                Masking = true,
                CornerRadius = CORNER_RADIUS,
                RelativeSizeAxes = Axes.Both,
                X = CORNER_RADIUS,
                Children = new[]
                {
                    backgroundContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    iconContainer = new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                    },
                    contentPaddingContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = Content = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = CORNER_RADIUS,
                            Masking = true,
                        },
                    },
                    StableContent = new StablePanelContent
                    {
                        Alpha = 0,
                    },
                    hoverLayer = new Box
                    {
                        Alpha = 0,
                        Colour = colours.Blue.Opacity(0.1f),
                        Blending = BlendingParameters.Additive,
                        RelativeSizeAxes = Axes.Both,
                    },
                    selectionLayer = new PulsatingBox
                    {
                        Alpha = 0,
                        RelativeSizeAxes = Axes.Both,
                        Width = 0.8f,
                        Blending = BlendingParameters.Additive,
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                    },
                    keyboardSelectionLayer = new Box
                    {
                        Alpha = 0,
                        Colour = keyboardSelectionColour = ColourInfo.GradientHorizontal(colourProvider.Highlight1.Opacity(0.1f), colourProvider.Highlight1.Opacity(0.4f)),
                        Blending = BlendingParameters.Additive,
                        RelativeSizeAxes = Axes.Both,
                    },
                    activationFlash = new Box
                    {
                        Colour = Color4.White.Opacity(0.4f),
                        Blending = BlendingParameters.Additive,
                        Alpha = 0f,
                        RelativeSizeAxes = Axes.Both,
                    },
                    new HoverSounds(),
                }
            };
        }

        public partial class PulsatingBox : BeatSyncedContainer
        {
            public int DepthLayer;

            private readonly Box box;

            public PulsatingBox()
            {
                EarlyActivationMilliseconds = 40;

                InternalChildren = new Drawable[]
                {
                    box = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            }

            protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
            {
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

                if (beatIndex % getFlashInterval(timingPoint.TimeSignature.Numerator) != 0)
                    return;

                double length = timingPoint.BeatLength;

                while (length < 250)
                    length *= 2;

                box
                    .FadeTo(0.8f, 40, Easing.Out)
                    .Then()
                    .FadeTo(0.4f, length, Easing.Out);
            }

            /// <summary>
            /// Returns the interval between flashes for this panel as a number of beats.
            /// </summary>
            /// <remarks>
            /// Interval has the following properties:
            /// <list type="number">
            /// <item><description>stay bar aligned</description></item>
            /// <item><description>higher DepthLayers flash less often than lower DepthLayers</description></item>
            /// <item><description>higher DepthLayers only flash when lower DepthLayers flash as well</description></item>
            /// <item><description>next smallest possible value to fulfill 1, 2 and 3</description></item>
            /// </list>
            /// </remarks>
            private long getFlashInterval(int beatsPerBar)
            {
                long beatsPerFlash = 1;
                int remainingBar = beatsPerBar;

                for (int i = 0; i < DepthLayer; i++)
                {
                    int factor = smallestPrimeFactor(remainingBar);

                    // when running out of prime factors, use 2 for higher multiple of beatsPerBar
                    if (factor <= 1)
                        factor = 2;

                    beatsPerFlash *= factor;

                    // divide out factor just used, so next iteration takes the next smallest prime factor
                    if (remainingBar % factor == 0)
                        remainingBar /= factor;
                }

                return beatsPerFlash;
            }

            private static int smallestPrimeFactor(int value)
            {
                for (int i = 2; i <= value / i; i++)
                {
                    if (value % i == 0)
                        return i;
                }

                return value;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            baseHeight = Height;

            Expanded.BindValueChanged(_ =>
            {
                updateSelectedState();
                updateXOffset();
            });

            Selected.BindValueChanged(_ =>
            {
                updateSelectedState();
                updateXOffset();
            }, true);

            KeyboardSelected.BindValueChanged(selected =>
            {
                if (selected.NewValue)
                {
                    keyboardSelectionLayer.FadeIn(80, Easing.Out)
                                          .Then()
                                          .FadeTo(0.5f, 2000, Easing.OutQuint);
                }
                else
                    keyboardSelectionLayer.FadeOut(1000, Easing.OutQuint);

                updateXOffset();
            }, true);
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            // Slightly offset the flash animation based on the panel depth.
            // This assumes a minimum depth of -2 (groups).
            selectionLayer.DepthLayer = -Item!.DepthLayer;

            updateAccentColour();

            updateXOffset(animated: false);
            updateSelectedState(animated: false);

            this.FadeIn(DURATION, Easing.OutQuint);
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            Hide();

            // Important to set this to null to handle reuse scenarios correctly, see `Item` implementation.
            item = null;
        }

        protected override bool OnClick(ClickEvent e)
        {
            // Item may be set to null before actual `FreeAfterUse`.
            // This is because Carousel knows to do this ahead of time and let the drawable fade/animate away.
            // See https://github.com/ppy/osu/blob/033e13cb3b79e6195ddcd9f659b04095aa52fd2f/osu.Game/Graphics/Carousel/Carousel.cs#L1132-L1135.
            if (item != null)
                carousel?.Activate(item);

            return true;
        }

        private void updateAccentColour()
        {
            var backgroundColour = accentColour ?? Color4.White;

            selectionLayer.Colour = ColourInfo.GradientHorizontal(backgroundColour.Opacity(0), backgroundColour.Opacity(0.5f));

            updateSelectedState(animated: false);
        }

        private void updateSelectedState(bool animated = true)
        {
            bool selectedOrExpanded = Expanded.Value || Selected.Value;

            // oily: stable's cards have no glow or pulsing selection, only colours (see StablePanelContent).
            if (StableLookShown)
            {
                TopLevelContent.EdgeEffect = default;
                selectionLayer.FadeOut(animated ? 200 : 0, Easing.OutQuint);
                StableContent.Active = selectedOrExpanded;
                return;
            }

            var edgeEffectColour = accentColour ?? Color4Extensions.FromHex(@"4EBFFF");

            if (selectedOrExpanded)
            {
                TopLevelContent.EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 2f,
                    Hollow = true,
                };
            }
            else
            {
                TopLevelContent.EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 4f,
                    Hollow = true,
                    Offset = new Vector2(0f, 1f),
                };
            }

            TopLevelContent.FadeEdgeEffectTo(selectedOrExpanded ? edgeEffectColour.Opacity(0.8f) : Color4.Black.Opacity(0.2f), animated ? DURATION : 0, Easing.OutQuint);

            if (selectedOrExpanded)
                selectionLayer.FadeIn(100, Easing.OutQuint);
            else
                selectionLayer.FadeOut(200, Easing.OutQuint);
        }

        private bool flipped => pieceRegistry?.CardStyle.Flip.Value == true;

        private void updateXOffset(bool animated = true)
        {
            float x = PanelXOffset + CORNER_RADIUS;

            if (!Expanded.Value && !Selected.Value)
            {
                if (this is PanelBeatmap || this is PanelBeatmapStandalone)
                    x += active_x_offset * 2;
                else
                    x += active_x_offset * 4;
            }

            if (!KeyboardSelected.Value)
                x += active_x_offset;

            // oily: flipped cards come in from the left.
            if (flipped)
                x = -x;

            TopLevelContent.MoveToX(x, animated ? DURATION : 0, Easing.OutQuint);
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverLayer.FadeIn(100, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverLayer.FadeOut(1000, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override void Update()
        {
            base.Update();
            contentPaddingContainer.Padding = contentPaddingContainer.Padding with { Left = iconContainer.DrawWidth };

            applyCardStyle();
        }

        /// <summary>
        /// oily: applies the skin's card look (flip, rotation, size, corners, picture opacity, colour).
        /// </summary>
        private void applyCardStyle()
        {
            if (pieceRegistry == null)
                return;

            var style = pieceRegistry.CardStyle;

            if (appliedFlip != style.Flip.Value)
            {
                appliedFlip = style.Flip.Value;
                Anchor = Origin = appliedFlip.Value ? Anchor.TopLeft : Anchor.TopRight;
                selectionLayer.Anchor = selectionLayer.Origin = appliedFlip.Value ? Anchor.TopLeft : Anchor.TopRight;
                updateXOffset(animated: false);
            }

            bool stableLook = style.Stable.Value && SupportsStableLook;

            if (StableLookShown != stableLook)
            {
                StableLookShown = stableLook;
                iconContainer.Alpha = stableLook ? 0 : 1;
                contentPaddingContainer.Alpha = stableLook ? 0 : 1;
                StableContent.Alpha = stableLook ? 1 : 0;

                // Stable's card picture is drawn whole, sticking out past the card like in stable, and has its own hover and selection look.
                TopLevelContent.Masking = !stableLook;
                hoverLayer.Alpha = 0;
                hoverLayer.Colour = stableLook ? Color4.Transparent : colours.Blue.Opacity(0.1f);
                keyboardSelectionLayer.Colour = stableLook ? ColourInfo.SingleColour(Color4.Transparent) : keyboardSelectionColour;
                updateSelectedState(animated: false);
            }

            Rotation = style.Rotation.Value;
            Width = style.Width.Value;

            if (baseHeight > 0)
                Height = (stableLook ? StableHeight : baseHeight) * style.Height.Value;

            TopLevelContent.CornerRadius = stableLook ? 0 : style.CornerRadius.Value;
            Content.CornerRadius = style.CornerRadius.Value;
            backgroundContainer.Alpha = stableLook ? 0 : style.BackgroundOpacity.Value;
            TopLevelContent.Colour = style.Tint.Value;
        }

        public abstract MenuItem[]? ContextMenuItems { get; }

        #region ICarouselPanel

        private CarouselItem? item;

        public CarouselItem? Item
        {
            get => item;
            set
            {
                if (ReferenceEquals(item, value))
                    return;

                // If a new item is set and we already have an item, this is a special case of reuse.
                // See https://github.com/ppy/osu/blob/033e13cb3b79e6195ddcd9f659b04095aa52fd2f/osu.Game/Graphics/Carousel/Carousel.cs#L1071
                // To keep things simple, assume that we need to do a full refresh.
                //
                // In the future, this could be more contextual and check whether the associated model has actually changed.
                if (item != null && value != null)
                {
                    item = value;
                    PrepareForUse();
                }
                else
                    item = value;
            }
        }

        public BindableBool Selected { get; } = new BindableBool();
        public BindableBool Expanded { get; } = new BindableBool();
        public BindableBool KeyboardSelected { get; } = new BindableBool();

        public double DrawYPosition { get; set; }

        public virtual void Activated()
        {
            activationFlash.FadeOutFromOne(1000, Easing.OutQuint);
        }

        #endregion
    }
}
