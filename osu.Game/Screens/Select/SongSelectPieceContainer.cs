// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Whether a skin moved this piece away from lazer's place (so the box it sits in shouldn't cut it off).
        /// </summary>
        public bool Moved => Layout is SongSelectPieceLayout layout && layout.IsAlive && !layout.FollowLazer.Value;

        /// <summary>
        /// Whether any of these pieces was moved by the skin.
        /// </summary>
        public static bool AnyMoved(SongSelectPieceRegistry? registry, params SongSelectPiece[] pieces)
            => registry != null && pieces.Any(p => registry.Get(p)?.Moved == true);

        /// <summary>
        /// Moved, scaled, rotated and cropped to where the skin wants the piece. Cuts off the cropped parts.
        /// </summary>
        private readonly Container frame;

        /// <summary>
        /// The piece at its own size (lazer's size, or the size the skin resized it to).
        /// </summary>
        private readonly Container inner;

        private readonly Axes contentRelativeAxes;

        private readonly Drawable content;

        /// <summary>
        /// How much bigger than lazer's size the piece is laid out.
        /// </summary>
        private Vector2 resize = Vector2.One;

        // oily: a hidden piece (like the beatmap info before it first fades in) has no height. Without this, an empty piece sitting exactly
        // on the edge of the screen counts as "masked away" and stops updating, so the piece inside never gets to fade in.
        protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;

        public SongSelectPieceContainer(SongSelectPiece piece, Drawable content)
        {
            Piece = piece;
            this.content = content;

            contentRelativeAxes = content.RelativeSizeAxes;
            RelativeSizeAxes = content.RelativeSizeAxes;
            Anchor = content.Anchor;
            Origin = content.Origin;

            content.Anchor = Anchor.TopLeft;
            content.Origin = Anchor.TopLeft;

            // The piece fills lazer's space on the axes lazer sizes relatively, and sizes itself on the others.
            // Its size on the relative axes is set every frame, so it can be laid out bigger or smaller without stretching.
            InternalChild = frame = new NeverMaskedAwayContainer
            {
                Child = inner = new NeverMaskedAwayContainer
                {
                    AutoSizeAxes = Axes.Both & ~contentRelativeAxes,
                    Child = content,
                },
            };
        }

        /// <summary>
        /// Where lazer would draw this piece, in screen space.
        /// </summary>
        public Quad NaturalScreenSpaceQuad => ToScreenSpace(new RectangleF(Vector2.Zero, naturalSize));

        /// <summary>
        /// lazer's size for the piece.
        /// </summary>
        private Vector2 naturalSize => new Vector2(
            (contentRelativeAxes & Axes.X) != 0 ? DrawWidth : inner.DrawWidth,
            (contentRelativeAxes & Axes.Y) != 0 ? DrawHeight : inner.DrawHeight);

        protected override void Update()
        {
            base.Update();

            // Lay the piece out before it updates, so a relatively sized piece is the right size this frame.
            if ((contentRelativeAxes & Axes.X) != 0) inner.Width = DrawWidth * resize.X;
            if ((contentRelativeAxes & Axes.Y) != 0) inner.Height = DrawHeight * resize.Y;
        }

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
                resize = Vector2.One;
                place(Vector2.Zero, Vector2.One, 0, null, default, 1, Color4.White);
                return;
            }

            float alpha = layout.Hidden.Value ? 0 : layout.PieceOpacity.Value;
            var crop = layout.Crop;

            // oily: a layout that stays in lazer's place only changes how the piece looks, so lazer's own movement isn't a frame behind.
            if (layout.FollowLazer.Value)
            {
                resize = Vector2.One;
                place(Vector2.Zero, Vector2.One, 0, null, crop, alpha, layout.Tint.Value);
                return;
            }

            Vector2 natural = naturalSize;
            Vector2 origin = ToScreenSpace(Vector2.Zero);
            Vector2 xAxis = ToScreenSpace(new Vector2(natural.X, 0)) - origin;
            Vector2 yAxis = ToScreenSpace(new Vector2(0, natural.Y)) - origin;

            if (xAxis.Length < 1 || yAxis.Length < 1)
                return;

            // oily: the layout is the visible (not cropped) part of the piece. Like a window, its size is the size the piece is laid out at,
            // and only the layout's scale (Shift + corner drag) zooms the piece.
            RectangleF fullRectangle = layout.FullRectangle;
            Quad target = layout.ToScreenSpace(fullRectangle);
            Vector2 targetX = target.TopRight - target.TopLeft;
            Vector2 targetY = target.BottomLeft - target.TopLeft;

            var scale = new Vector2(Math.Abs(layout.Scale.X), Math.Abs(layout.Scale.Y));

            // The piece's whole size in this container's units, before zooming.
            var full = new Vector2(targetX.Length / (xAxis.Length / natural.X) / scale.X, targetY.Length / (yAxis.Length / natural.Y) / scale.Y);

            if (!float.IsFinite(full.X) || !float.IsFinite(full.Y) || full.X <= 0 || full.Y <= 0)
                return;

            // Where lazer sizes the piece to fit, it's laid out at the layout's size and rearranges itself. Parts that size themselves keep their size.
            resize = new Vector2(
                (contentRelativeAxes & Axes.X) != 0 && DrawWidth > 0 ? full.X / DrawWidth : 1,
                (contentRelativeAxes & Axes.Y) != 0 && DrawHeight > 0 ? full.Y / DrawHeight : 1);

            float rotation = MathHelper.RadiansToDegrees(MathF.Atan2(targetX.Y, targetX.X) - MathF.Atan2(xAxis.Y, xAxis.X));

            place(ToLocalSpace(target.TopLeft), scale, rotation, full, crop, alpha, layout.Tint.Value);
        }

        /// <summary>
        /// Puts the piece's top left corner at <paramref name="position"/>, then cuts off the cropped edges.
        /// <paramref name="full"/> is the piece's whole size, which the crop is a fraction of (null for its own size).
        /// </summary>
        private void place(Vector2 position, Vector2 scale, float rotation, Vector2? full, (float left, float top, float right, float bottom) crop, float alpha, Color4 colour)
        {
            Vector2 contentSize = inner.DrawSize;
            Vector2 size = full ?? contentSize;

            // Cut off what's cropped, and anything that sticks out of a window smaller than the part.
            bool cropped = crop.left > 0 || crop.top > 0 || crop.right > 0 || crop.bottom > 0
                           || size.X < contentSize.X - 0.5f || size.Y < contentSize.Y - 0.5f;

            Vector2 cut = new Vector2(crop.left * size.X, crop.top * size.Y);

            // oily (ilo, 2026-11-26): some pieces (like the slanted search bar) draw past their own box on purpose.
            // Only cropped edges cut the piece off; the other edges leave room for what sticks out.
            RectangleF bounds = inner.ToLocalSpace(content.ScreenSpaceDrawQuad).AABBFloat;
            float padLeft = crop.left > 0 ? 0 : Math.Max(0, -bounds.Left);
            float padTop = crop.top > 0 ? 0 : Math.Max(0, -bounds.Top);
            float padRight = crop.right > 0 ? 0 : Math.Max(0, bounds.Right - size.X);
            float padBottom = crop.bottom > 0 ? 0 : Math.Max(0, bounds.Bottom - size.Y);

            if (!cropped)
                padLeft = padTop = padRight = padBottom = 0;
            else
            {
                // A couple of pixels spare, so the smoothed edge of what sticks out isn't shaved off.
                if (padLeft > 0) padLeft += 2;
                if (padTop > 0) padTop += 2;
                if (padRight > 0) padRight += 2;
                if (padBottom > 0) padBottom += 2;
            }

            Vector2 start = cut - new Vector2(padLeft, padTop);

            // The visible part starts after the cut, along the piece's own (scaled and rotated) axes.
            float radians = MathHelper.DegreesToRadians(rotation);
            Vector2 offset = new Vector2(start.X * scale.X, start.Y * scale.Y);
            offset = new Vector2(
                offset.X * MathF.Cos(radians) - offset.Y * MathF.Sin(radians),
                offset.X * MathF.Sin(radians) + offset.Y * MathF.Cos(radians));

            frame.Position = position + offset;
            frame.Scale = scale;
            frame.Rotation = rotation;
            frame.Size = new Vector2(size.X * (1 - crop.left - crop.right) + padLeft + padRight, size.Y * (1 - crop.top - crop.bottom) + padTop + padBottom);
            frame.Masking = cropped;
            frame.Alpha = alpha;
            frame.Colour = colour;

            inner.Position = -start;
        }

        private partial class NeverMaskedAwayContainer : Container
        {
            protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;
        }
    }
}
