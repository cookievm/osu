// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Testing;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: a skin editor component that takes control of one piece of song select.
    /// Move, resize and rotate it in the skin editor, and the real piece follows. Settings hide it, fade it or colour it.
    /// </summary>
    /// <remarks>
    /// Needs <see cref="SongSelectPieceRegistry"/>, so it is only offered in the skin editor's song select layer.
    /// The component draws nothing itself; it's the same size as the piece, so the skin editor's selection box fits the piece.
    /// </remarks>
    public abstract partial class SongSelectPieceLayout : CompositeDrawable, ISkinComponentWithLabel
    {
        public bool UsesFixedAnchor { get; set; }

        public abstract SongSelectPiece Piece { get; }

        /// <summary>
        /// What this part of song select is called in the skin editor's component list.
        /// </summary>
        public abstract string DisplayName { get; }

        [SettingSource("Stay in lazer's place", "Follows where lazer puts this part. Moving or resizing it in the skin editor turns this off.")]
        public BindableBool FollowLazer { get; } = new BindableBool();

        [SettingSource("Hidden", "Hide this part of song select.")]
        public BindableBool Hidden { get; } = new BindableBool();

        // oily: the crop is set by holding Ctrl while dragging the skin editor's handles (see SkinSelectionScaleHandler), so no sliders are shown.
        [SettingSource("Crop left", "How much of the part's left side is cut off.", HideControl = true)]
        public BindableFloat CropLeft { get; } = createCrop();

        [SettingSource("Crop right", "How much of the part's right side is cut off.", HideControl = true)]
        public BindableFloat CropRight { get; } = createCrop();

        [SettingSource("Crop top", "How much of the part's top is cut off.", HideControl = true)]
        public BindableFloat CropTop { get; } = createCrop();

        [SettingSource("Crop bottom", "How much of the part's bottom is cut off.", HideControl = true)]
        public BindableFloat CropBottom { get; } = createCrop();

        /// <summary>
        /// The most of a part that can be cut off on one axis.
        /// </summary>
        public const float MAX_CROP = 0.95f;

        private static BindableFloat createCrop() => new BindableFloat
        {
            MinValue = 0,
            MaxValue = MAX_CROP,
        };

        /// <summary>
        /// The crop as fractions of the part's size (left, top, right, bottom), never cutting off everything.
        /// </summary>
        public (float left, float top, float right, float bottom) Crop
        {
            get
            {
                float left = CropLeft.Value, right = CropRight.Value, top = CropTop.Value, bottom = CropBottom.Value;

                if (left + right > MAX_CROP)
                {
                    float f = MAX_CROP / (left + right);
                    left *= f;
                    right *= f;
                }

                if (top + bottom > MAX_CROP)
                {
                    float f = MAX_CROP / (top + bottom);
                    top *= f;
                    bottom *= f;
                }

                return (left, top, right, bottom);
            }
        }

        /// <summary>
        /// The part's whole size (before cropping) in this component's own space. This component is the size of the part's visible (not cut off) area.
        /// </summary>
        public Vector2 FullSize
        {
            get
            {
                var crop = Crop;
                return new Vector2(DrawWidth / (1 - crop.left - crop.right), DrawHeight / (1 - crop.top - crop.bottom));
            }
        }

        /// <summary>
        /// The part's whole area (before cropping) in this component's own space.
        /// </summary>
        public RectangleF FullRectangle
        {
            get
            {
                var crop = Crop;
                Vector2 full = FullSize;
                return new RectangleF(-crop.left * full.X, -crop.top * full.Y, full.X, full.Y);
            }
        }

        [SettingSource("Opacity", "How see-through this part of song select is.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat PieceOpacity { get; } = new BindableFloat(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource("Colour", "Tints this part of song select. White keeps lazer's colours.")]
        public BindableColour4 Tint { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Background colour", "The panel behind this part. White keeps lazer's colour.")]
        public BindableColour4 BackgroundTint { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Background opacity", "How see-through the panel behind this part is.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat BackgroundOpacity { get; } = new BindableFloat(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource("Text colour", "Every text and icon in this part. White keeps lazer's colour.")]
        public BindableColour4 TextColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Buttons colour", "Buttons, dropdowns, tabs, sliders and text boxes in this part. White keeps lazer's colour.")]
        public BindableColour4 ControlsColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Buttons opacity", "How see-through the buttons, dropdowns, tabs, sliders and text boxes are.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat ControlsOpacity { get; } = new BindableFloat(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource("Buttons text colour", "Text and icons on the buttons, dropdowns, tabs, sliders and text boxes. White keeps lazer's colour.")]
        public BindableColour4 ControlsText { get; } = new BindableColour4(Colour4.White);

        [Resolved]
        protected SongSelectPieceRegistry Registry { get; private set; } = null!;

        /// <summary>
        /// Whether this component is in control of its piece.
        /// </summary>
        protected bool InControl => piece?.Layout == this;

        private SongSelectPieceContainer? piece;

        protected SongSelectPieceLayout()
        {
            // Placeholder until the piece's real size is known.
            Size = new Vector2(300, 100);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Only components placed in song select's skin layer take control.
            // (The skin editor's component list also creates one as a preview.)
            if (findSkinnableContainer() == null)
            {
                // oily: in the component list, just say what it is.
                AutoSizeAxes = Axes.Both;
                InternalChild = new SkinComponentLabel(DisplayName);
                return;
            }

            piece = Registry.Get(Piece);

            if (piece == null)
                return;

            if (isFreshlyPlaced())
                snapToPiece();

            piece.Layout = this;
        }

        protected override void Update()
        {
            base.Update();

            if (piece == null || Parent == null)
                return;

            if (InControl)
            {
                UpdatePieceStyle();
                updateRecolour();
            }

            if (FollowLazer.Value)
            {
                followPiece();
                return;
            }

            // oily: once moved, this component's size is the part's size (like a window), so lazer's own size no longer matters.
            hasFollowed = false;
        }

        private bool recolourIsDefault = true;
        private double lastRecolour = double.MinValue;

        /// <summary>
        /// Applies the background, text and part colours. Big pieces (the beatmap list) are only redone a few times a second.
        /// </summary>
        private void updateRecolour()
        {
            if (piece == null)
                return;

            bool isDefault = !HasCustomColours;

            if (isDefault && recolourIsDefault)
                return;

            if (!isDefault && !recolourIsDefault && Time.Current - lastRecolour < 100)
                return;

            OilyRecolour.Apply(piece, RecolourZones(piece, false).ToList());
            recolourIsDefault = isDefault;
            lastRecolour = Time.Current;
        }

        /// <summary>
        /// The parts of this piece that get their own colours. <paramref name="reset"/> asks for lazer's colours everywhere.
        /// </summary>
        protected virtual IEnumerable<OilyRecolourZone> RecolourZones(SongSelectPieceContainer root, bool reset)
        {
            yield return new OilyRecolourZone
            {
                Root = root,
                Text = reset ? null : OilyRecolour.Custom(TextColour.Value),
            };

            // Parts with their own settings come first, so they win over the shared "Buttons" settings.
            foreach (var zone in PartZones(root, reset))
                yield return zone;

            foreach (var control in root.ChildrenOfType<Drawable>().Where(isControl))
            {
                if (control.FindClosestParent<SongSelectPieceContainer>() != root)
                    continue;

                yield return Zone(control, ControlsColour.Value, ControlsOpacity.Value, ControlsText.Value, reset);
            }

            foreach (var wedge in root.ChildrenOfType<WedgeBackground>())
            {
                if (wedge.FindClosestParent<SongSelectPieceContainer>() != root)
                    continue;

                yield return new OilyRecolourZone
                {
                    Root = wedge,
                    Colour = reset ? null : OilyRecolour.Custom(BackgroundTint.Value),
                    Opacity = reset ? 1 : BackgroundOpacity.Value,
                };
            }
        }

        /// <summary>
        /// Whether any colour setting differs from lazer's look (checked every frame, so it must be cheap).
        /// </summary>
        protected virtual bool HasCustomColours =>
            !BackgroundTint.IsDefault || !BackgroundOpacity.IsDefault || !TextColour.IsDefault
            || !ControlsColour.IsDefault || !ControlsOpacity.IsDefault || !ControlsText.IsDefault;

        /// <summary>
        /// Parts of this piece with their own settings (the search bar's search box, dropdowns...).
        /// </summary>
        protected virtual IEnumerable<OilyRecolourZone> PartZones(SongSelectPieceContainer root, bool reset) => Enumerable.Empty<OilyRecolourZone>();

        private static readonly ConcurrentDictionary<Type, bool> control_types = new ConcurrentDictionary<Type, bool>();

        private static bool isControl(Drawable d) => control_types.GetOrAdd(d.GetType(), isControlType);

        private static bool isControlType(Type type)
        {
            if (typeof(ShearedButton).IsAssignableFrom(type) || typeof(ShearedSearchTextBox).IsAssignableFrom(type)
                                                             || typeof(ShearedRangeSlider).IsAssignableFrom(type) || typeof(OsuTextBox).IsAssignableFrom(type))
                return true;

            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                if (!t.IsGenericType)
                    continue;

                var definition = t.GetGenericTypeDefinition();

                if (definition == typeof(Dropdown<>) || definition == typeof(TabControl<>) || definition == typeof(SliderBar<>))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A part with its own colour, opacity and text colour settings. Settings left at lazer's look fall back to the "Buttons" settings.
        /// </summary>
        protected OilyRecolourZone Zone(Drawable? part, Colour4 colour, float opacity, Colour4 text, bool reset) => new OilyRecolourZone
        {
            Root = part ?? new Container(),
            Colour = reset ? null : OilyRecolour.Custom(colour) ?? OilyRecolour.Custom(ControlsColour.Value),
            Opacity = reset ? 1 : opacity < 1 ? opacity : ControlsOpacity.Value,
            Text = reset ? null : OilyRecolour.Custom(text) ?? OilyRecolour.Custom(ControlsText.Value),
        };

        private bool hasFollowed;
        private Vector2 followedPosition;
        private Vector2 followedScale;
        private Vector2 followedSize;
        private float followedRotation;

        /// <summary>
        /// Sits exactly over lazer's piece. If the skin editor moved, resized or rotated this component since the last frame, stop following.
        /// </summary>
        private void followPiece()
        {
            if (hasFollowed && (Position != followedPosition || Scale != followedScale || Size != followedSize || Rotation != followedRotation
                                || Anchor != Anchor.TopLeft || Origin != Anchor.TopLeft))
            {
                FollowLazer.Value = false;
                hasFollowed = false;
                return;
            }

            snapToPiece();

            followedPosition = Position;
            followedScale = Scale;
            followedSize = Size;
            followedRotation = Rotation;
            hasFollowed = true;
        }

        /// <summary>
        /// Applies settings that change how the piece itself looks. Called every frame while in control.
        /// </summary>
        protected virtual void UpdatePieceStyle()
        {
        }

        /// <summary>
        /// Undoes <see cref="UpdatePieceStyle"/> when this component stops controlling its piece.
        /// </summary>
        protected virtual void ResetPieceStyle()
        {
        }

        /// <summary>
        /// The skin editor puts new components at the top centre, half way down. A component loaded from a skin is anywhere else.
        /// </summary>
        private bool isFreshlyPlaced() =>
            Anchor == Anchor.TopCentre && Origin == Anchor.TopCentre && X == 0 && Parent != null && Math.Abs(Y - Parent.DrawHeight / 2) < 1;

        /// <summary>
        /// Puts this component exactly over the visible (not cropped) part of lazer's piece, so adding it changes nothing until it is moved.
        /// </summary>
        private void snapToPiece()
        {
            if (piece == null || Parent == null)
                return;

            Quad natural = piece.NaturalScreenSpaceQuad;
            Vector2 topLeft = Parent.ToLocalSpace(natural.TopLeft);
            Vector2 xAxis = Parent.ToLocalSpace(natural.TopRight) - topLeft;
            Vector2 yAxis = Parent.ToLocalSpace(natural.BottomLeft) - topLeft;
            var crop = Crop;

            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            Rotation = 0;
            Scale = Vector2.One;
            Size = new Vector2(xAxis.Length * (1 - crop.left - crop.right), yAxis.Length * (1 - crop.top - crop.bottom));
            Position = topLeft + xAxis * crop.left + yAxis * crop.top;
        }

        private SkinnableContainer? findSkinnableContainer()
        {
            for (var p = Parent; p != null; p = p.Parent)
            {
                if (p is SkinnableContainer container)
                    return container;
            }

            return null;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (piece?.Layout == this)
            {
                piece.Layout = null;
                ResetPieceStyle();

                if (!recolourIsDefault)
                    OilyRecolour.Apply(piece, RecolourZones(piece, true).ToList());
            }

            base.Dispose(isDisposing);
        }
    }

    public partial class SongSelectBeatmapInfoLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.BeatmapInfo;

        public override string DisplayName => "Beatmap info";
    }

    public partial class SongSelectBeatmapDetailsLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.BeatmapDetails;

        public override string DisplayName => "Beatmap details";
    }

    public partial class SongSelectBeatmapListLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.BeatmapList;

        public override string DisplayName => "Beatmap list";

        [SettingSource("Flip cards", "Cards come in from the left side instead of the right.")]
        public BindableBool FlipCards { get; } = new BindableBool();

        [SettingSource("Card rotation", "Tilts every card, in degrees.")]
        public BindableFloat CardRotation { get; } = new BindableFloat
        {
            MinValue = -45,
            MaxValue = 45,
            Precision = 0.5f,
        };

        [SettingSource("Card width", "How wide the cards are, compared to the list.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat CardWidth { get; } = new BindableFloat(1)
        {
            MinValue = 0.3f,
            MaxValue = 1.5f,
            Precision = 0.01f,
        };

        [SettingSource("Card height", "How tall the cards are, compared to lazer's cards.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat CardHeight { get; } = new BindableFloat(1)
        {
            MinValue = 0.5f,
            MaxValue = 2,
            Precision = 0.01f,
        };

        [SettingSource("Card corner radius", "How round the card corners are.")]
        public BindableFloat CardCornerRadius { get; } = new BindableFloat(SongSelectCardStyle.DEFAULT_CORNER_RADIUS)
        {
            MinValue = 0,
            MaxValue = 30,
            Precision = 0.5f,
        };

        [SettingSource("Card picture opacity", "How visible the beatmap's background picture is on each card.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat CardBackgroundOpacity { get; } = new BindableFloat(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource("Show pp on cards", "Shows the pp an SS gives (with your mods) on each difficulty card.")]
        public BindableBool ShowPerformance { get; } = new BindableBool(true);

        [SettingSource("Show simulated pp on cards", "Shows each card's pretend score (accuracy and misses) and its pp, next to the SS pp. Right-click a card to change its pretend score.")]
        public BindableBool ShowSimulatedPerformance { get; } = new BindableBool(true);

        [SettingSource("Card colour", "Tints every card. White keeps lazer's colours.")]
        public BindableColour4 CardTint { get; } = new BindableColour4(Colour4.White);

        protected override void UpdatePieceStyle()
        {
            var style = Registry.CardStyle;

            style.Flip.Value = FlipCards.Value;
            style.Rotation.Value = CardRotation.Value;
            style.Width.Value = CardWidth.Value;
            style.Height.Value = CardHeight.Value;
            style.CornerRadius.Value = CardCornerRadius.Value;
            style.BackgroundOpacity.Value = CardBackgroundOpacity.Value;
            style.Tint.Value = CardTint.Value;
            style.ShowPerformance.Value = ShowPerformance.Value;
            style.ShowSimulatedPerformance.Value = ShowSimulatedPerformance.Value;
        }

        protected override void ResetPieceStyle() => Registry.CardStyle.Reset();
    }

    public partial class SongSelectSearchBarLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.SearchBar;

        public override string DisplayName => "Search bar";

        [SettingSource("Search box colour", "White keeps lazer's colour.")]
        public BindableColour4 SearchBoxColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Search box opacity", "How see-through it is.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat SearchBoxOpacity { get; } = opacity();

        [SettingSource("Search box text colour", "White keeps lazer's colour.")]
        public BindableColour4 SearchBoxText { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Star rating bar colour", "White keeps lazer's colour.")]
        public BindableColour4 StarRatingColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Star rating bar opacity", "How see-through it is.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat StarRatingOpacity { get; } = opacity();

        [SettingSource("Star rating bar text colour", "White keeps lazer's colour.")]
        public BindableColour4 StarRatingText { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Show converts button colour", "White keeps lazer's colour.")]
        public BindableColour4 ShowConvertsColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Show converts button opacity", "How see-through it is.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat ShowConvertsOpacity { get; } = opacity();

        [SettingSource("Show converts button text colour", "White keeps lazer's colour.")]
        public BindableColour4 ShowConvertsText { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Sort dropdown colour", "White keeps lazer's colour.")]
        public BindableColour4 SortColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Sort dropdown opacity", "How see-through it is.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat SortOpacity { get; } = opacity();

        [SettingSource("Sort dropdown text colour", "White keeps lazer's colour.")]
        public BindableColour4 SortText { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Group dropdown colour", "White keeps lazer's colour.")]
        public BindableColour4 GroupColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Group dropdown opacity", "How see-through it is.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat GroupOpacity { get; } = opacity();

        [SettingSource("Group dropdown text colour", "White keeps lazer's colour.")]
        public BindableColour4 GroupText { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Collection dropdown colour", "White keeps lazer's colour.")]
        public BindableColour4 CollectionColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Collection dropdown opacity", "How see-through it is.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat CollectionOpacity { get; } = opacity();

        [SettingSource("Collection dropdown text colour", "White keeps lazer's colour.")]
        public BindableColour4 CollectionText { get; } = new BindableColour4(Colour4.White);

        protected override bool HasCustomColours => base.HasCustomColours
                                                    || new IHasDefaultValue[]
                                                    {
                                                        SearchBoxColour, SearchBoxOpacity, SearchBoxText, StarRatingColour, StarRatingOpacity, StarRatingText,
                                                        ShowConvertsColour, ShowConvertsOpacity, ShowConvertsText, SortColour, SortOpacity, SortText,
                                                        GroupColour, GroupOpacity, GroupText, CollectionColour, CollectionOpacity, CollectionText,
                                                    }.Any(b => !b.IsDefault);

        private static BindableFloat opacity() => new BindableFloat(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        protected override IEnumerable<OilyRecolourZone> PartZones(SongSelectPieceContainer root, bool reset)
        {
            var filter = root.ChildrenOfType<FilterControl>().FirstOrDefault();

            if (filter == null)
                yield break;

            yield return Zone(filter.SearchBox, SearchBoxColour.Value, SearchBoxOpacity.Value, SearchBoxText.Value, reset);
            yield return Zone(filter.StarRatingBar, StarRatingColour.Value, StarRatingOpacity.Value, StarRatingText.Value, reset);
            yield return Zone(filter.ShowConvertsButton, ShowConvertsColour.Value, ShowConvertsOpacity.Value, ShowConvertsText.Value, reset);
            yield return Zone(filter.SortDropdown, SortColour.Value, SortOpacity.Value, SortText.Value, reset);
            yield return Zone(filter.GroupDropdown, GroupColour.Value, GroupOpacity.Value, GroupText.Value, reset);
            yield return Zone(filter.CollectionDropdown, CollectionColour.Value, CollectionOpacity.Value, CollectionText.Value, reset);
        }
    }

    public partial class SongSelectMapStatisticsLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.MapStatistics;

        public override string DisplayName => "Map statistics";
    }

    public partial class SongSelectDifficultyCardLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.DifficultyCard;

        public override string DisplayName => "Difficulty card";
    }

    public partial class SongSelectDifficultyStatsLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.DifficultyStats;

        public override string DisplayName => "Difficulty stats";
    }

    public partial class SongSelectObjectCountsLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.ObjectCounts;

        public override string DisplayName => "Object counts";
    }
}
