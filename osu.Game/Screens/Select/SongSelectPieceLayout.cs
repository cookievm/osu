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

        protected abstract SongSelectPiece Piece { get; }

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
        private SongSelectPieceRegistry registry { get; set; } = null!;

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

            piece = registry.Get(Piece);

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

            // Keep the same size as lazer's piece, in this component's own space. The skin editor resizes using Scale.
            Quad natural = piece.NaturalScreenSpaceQuad;
            Vector2 topLeft = Parent.ToLocalSpace(natural.TopLeft);
            float width = (Parent.ToLocalSpace(natural.TopRight) - topLeft).Length;
            float height = (Parent.ToLocalSpace(natural.BottomLeft) - topLeft).Length;

            if (width > 0 && height > 0)
                Size = new Vector2(width, height);
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
                piece.Layout = null;

            base.Dispose(isDisposing);
        }
    }

    public partial class SongSelectBeatmapInfoLayout : SongSelectPieceLayout
    {
        protected override SongSelectPiece Piece => SongSelectPiece.BeatmapInfo;
    }

    public partial class SongSelectBeatmapDetailsLayout : SongSelectPieceLayout
    {
        protected override SongSelectPiece Piece => SongSelectPiece.BeatmapDetails;
    }

    public partial class SongSelectBeatmapListLayout : SongSelectPieceLayout
    {
        protected override SongSelectPiece Piece => SongSelectPiece.BeatmapList;
    }

    public partial class SongSelectSearchBarLayout : SongSelectPieceLayout
    {
        protected override SongSelectPiece Piece => SongSelectPiece.SearchBar;
    }
}
