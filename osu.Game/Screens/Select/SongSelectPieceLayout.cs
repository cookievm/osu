// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    public abstract partial class SongSelectPieceLayout : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        public abstract SongSelectPiece Piece { get; }

        [SettingSource("Stay in lazer's place", "Follows where lazer puts this part. Moving or resizing it in the skin editor turns this off.")]
        public BindableBool FollowLazer { get; } = new BindableBool();

        [SettingSource("Hidden", "Hide this part of song select.")]
        public BindableBool Hidden { get; } = new BindableBool();

        [SettingSource("Opacity", "How see-through this part of song select is.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat PieceOpacity { get; } = new BindableFloat(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource("Colour", "Tints this part of song select. White keeps lazer's colours.")]
        public BindableColour4 Tint { get; } = new BindableColour4(Colour4.White);

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
                return;

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
                UpdatePieceStyle();

            if (FollowLazer.Value)
            {
                followPiece();
                return;
            }

            hasFollowed = false;

            // Keep the same size as lazer's piece, in this component's own space. The skin editor resizes using Scale.
            Quad natural = piece.NaturalScreenSpaceQuad;
            Vector2 topLeft = Parent.ToLocalSpace(natural.TopLeft);
            float width = (Parent.ToLocalSpace(natural.TopRight) - topLeft).Length;
            float height = (Parent.ToLocalSpace(natural.BottomLeft) - topLeft).Length;

            if (width > 0 && height > 0)
                Size = new Vector2(width, height);
        }

        private bool hasFollowed;
        private Vector2 followedPosition;
        private Vector2 followedScale;
        private float followedRotation;

        /// <summary>
        /// Sits exactly over lazer's piece. If the skin editor moved, resized or rotated this component since the last frame, stop following.
        /// </summary>
        private void followPiece()
        {
            if (hasFollowed && (Position != followedPosition || Scale != followedScale || Rotation != followedRotation || Anchor != Anchor.TopLeft || Origin != Anchor.TopLeft))
            {
                FollowLazer.Value = false;
                hasFollowed = false;
                return;
            }

            snapToPiece();

            followedPosition = Position;
            followedScale = Scale;
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
        /// Puts this component exactly over lazer's piece, so adding it changes nothing until it is moved.
        /// </summary>
        private void snapToPiece()
        {
            if (piece == null || Parent == null)
                return;

            Quad natural = piece.NaturalScreenSpaceQuad;
            Vector2 topLeft = Parent.ToLocalSpace(natural.TopLeft);

            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            Rotation = 0;
            Scale = Vector2.One;
            Size = new Vector2((Parent.ToLocalSpace(natural.TopRight) - topLeft).Length, (Parent.ToLocalSpace(natural.BottomLeft) - topLeft).Length);
            Position = topLeft;
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
            }

            base.Dispose(isDisposing);
        }
    }

    public partial class SongSelectBeatmapInfoLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.BeatmapInfo;
    }

    public partial class SongSelectBeatmapDetailsLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.BeatmapDetails;
    }

    public partial class SongSelectBeatmapListLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.BeatmapList;

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
    }

    public partial class SongSelectMapStatisticsLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.MapStatistics;
    }

    public partial class SongSelectDifficultyCardLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.DifficultyCard;
    }

    public partial class SongSelectDifficultyStatsLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.DifficultyStats;
    }

    public partial class SongSelectObjectCountsLayout : SongSelectPieceLayout
    {
        public override SongSelectPiece Piece => SongSelectPiece.ObjectCounts;
    }
}
