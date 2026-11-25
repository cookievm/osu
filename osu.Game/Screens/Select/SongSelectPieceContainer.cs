// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: the parts of song select a skin can move, resize, rotate, hide and colour (see <see cref="SongSelectPieceLayout"/>).
    /// </summary>
    public enum SongSelectPiece
    {
        BeatmapInfo,
        BeatmapDetails,
        BeatmapList,
        SearchBar,
        MapStatistics,
        DifficultyCard,
        DifficultyStats,
        ObjectCounts,
    }

    /// <summary>
    /// oily: lets <see cref="SongSelectPieceLayout"/> skin components find the pieces of the current song select.
    /// </summary>
    public class SongSelectPieceRegistry
    {
        private readonly Dictionary<SongSelectPiece, SongSelectPieceContainer> pieces = new Dictionary<SongSelectPiece, SongSelectPieceContainer>();

        public void Register(SongSelectPieceContainer container) => pieces[container.Piece] = container;

        public SongSelectPieceContainer? Get(SongSelectPiece piece) => pieces.GetValueOrDefault(piece);

        /// <summary>
        /// How the beatmap cards look (set by the "Beatmap List" skin component).
        /// </summary>
        public readonly SongSelectCardStyle CardStyle = new SongSelectCardStyle();

        /// <summary>
        /// oily: colours of the Universal PP Simulator's sliders and buttons (set by the "PP Simulator colours" skin component).
        /// </summary>
        public readonly PerformanceSimulatorStyle SimulatorStyle = new PerformanceSimulatorStyle();

        /// <summary>
        /// Wraps a piece of song select so skins can move and style it. Without a registry (outside song select) the piece is still wrapped, but nothing controls it.
        /// </summary>
        public static SongSelectPieceContainer Wrap(SongSelectPieceRegistry? registry, SongSelectPiece piece, Drawable content)
        {
            var container = new SongSelectPieceContainer(piece, content);
            registry?.Register(container);
            return container;
        }
    }

    /// <summary>
    /// oily: wraps one piece of song select. The piece stays where lazer puts it, unless the skin has a
    /// <see cref="SongSelectPieceLayout"/> for it. Then the piece is drawn where that layout component is, with its size, rotation and colour.
    /// </summary>
    public partial class SongSelectPieceContainer : Container
    {
        public readonly SongSelectPiece Piece;

        /// <summary>
        /// The skin component currently controlling this piece, if any.
        /// </summary>
        public SongSelectPieceLayout? Layout { get; set; }

        private readonly Container inner;

        public SongSelectPieceContainer(SongSelectPiece piece, Drawable content)
        {
            Piece = piece;

            RelativeSizeAxes = content.RelativeSizeAxes;
            Anchor = content.Anchor;
            Origin = content.Origin;

            content.Anchor = Anchor.TopLeft;
            content.Origin = Anchor.TopLeft;

            InternalChild = inner = new Container
            {
                RelativeSizeAxes = content.RelativeSizeAxes,
                AutoSizeAxes = Axes.Both & ~content.RelativeSizeAxes,
                Child = content,
            };
        }

        /// <summary>
        /// Where lazer would draw this piece, in screen space.
        /// </summary>
        public Quad NaturalScreenSpaceQuad => ToScreenSpace(new RectangleF(Vector2.Zero, inner.DrawSize));

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // Follow the piece's own size on axes lazer doesn't size relatively.
            if ((RelativeSizeAxes & Axes.X) == 0) Width = inner.DrawWidth;
            if ((RelativeSizeAxes & Axes.Y) == 0) Height = inner.DrawHeight;

            var layout = Layout;

            if (layout == null || !layout.IsAlive || layout.Parent == null || !layout.IsLoaded)
            {
                Layout = null;
                resetInner();
                return;
            }

            Vector2 origin = ToScreenSpace(Vector2.Zero);
            Vector2 xAxis = ToScreenSpace(new Vector2(inner.DrawWidth, 0)) - origin;
            Vector2 yAxis = ToScreenSpace(new Vector2(0, inner.DrawHeight)) - origin;

            if (xAxis.Length < 1 || yAxis.Length < 1)
                return;

            Quad target = layout.ScreenSpaceDrawQuad;
            Vector2 targetX = target.TopRight - target.TopLeft;
            Vector2 targetY = target.BottomLeft - target.TopLeft;

            inner.Position = ToLocalSpace(target.TopLeft);
            inner.Scale = new Vector2(targetX.Length / xAxis.Length, targetY.Length / yAxis.Length);
            inner.Rotation = MathHelper.RadiansToDegrees(MathF.Atan2(targetX.Y, targetX.X) - MathF.Atan2(xAxis.Y, xAxis.X));
            inner.Alpha = layout.Hidden.Value ? 0 : layout.PieceOpacity.Value;
            inner.Colour = layout.Tint.Value;
        }

        private void resetInner()
        {
            inner.Position = Vector2.Zero;
            inner.Scale = Vector2.One;
            inner.Rotation = 0;
            inner.Alpha = 1;
            inner.Colour = Color4.White;
        }
    }
}
