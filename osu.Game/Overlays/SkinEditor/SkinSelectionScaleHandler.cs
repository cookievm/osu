// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Utils;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Overlays.SkinEditor
{
    public partial class SkinSelectionScaleHandler : SelectionScaleHandler
    {
        public Action<Drawable, Vector2> UpdatePosition { get; init; } = null!;

        public event Action<Axes>? PerformFlipFromScaleHandles;

        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        private BindableList<ISerialisableDrawable> selectedItems { get; } = new BindableList<ISerialisableDrawable>();

        [BackgroundDependencyLoader]
        private void load(SkinEditor skinEditor)
        {
            selectedItems.BindTo(skinEditor.SelectedComponents);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedItems.CollectionChanged += (_, _) => updateState();
            updateState();
        }

        private void updateState()
        {
            CanScaleX.Value = allSelectedSupportManualSizing(Axes.X);
            CanScaleY.Value = allSelectedSupportManualSizing(Axes.Y);
            CanScaleDiagonally.Value = true;
        }

        private bool allSelectedSupportManualSizing(Axes axis) => selectedItems.All(b => (b as CompositeDrawable)?.AutoSizeAxes.HasFlag(axis) == false);

        private Dictionary<Drawable, OriginalDrawableState>? objectsInScale;
        private Vector2? defaultOrigin;

        private bool isFlippedX;
        private bool isFlippedY;

        public override void Begin()
        {
            if (objectsInScale != null)
                throw new InvalidOperationException($"Cannot {nameof(Begin)} a scale operation while another is in progress!");

            changeHandler?.BeginChange();

            objectsInScale = selectedItems.Cast<Drawable>().ToDictionary(d => d, d => new OriginalDrawableState(d));
            OriginalSurroundingQuad = ToLocalSpace(GeometryUtils.GetSurroundingQuad(objectsInScale.SelectMany(d => d.Key.ScreenSpaceDrawQuad.GetVertices().ToArray())));
            defaultOrigin = ToLocalSpace(GeometryUtils.MinimumEnclosingCircle(objectsInScale.SelectMany(d => d.Key.ScreenSpaceDrawQuad.GetVertices().ToArray())).Item1);

            isFlippedX = false;
            isFlippedY = false;

            // oily: song select parts resize like a window, and crop with Ctrl held.
            piecesInScale = selectedItems.Count > 0 && selectedItems.All(i => i is SongSelectPieceLayout)
                ? selectedItems.Cast<SongSelectPieceLayout>().ToDictionary(l => l, l => new OriginalPieceState(l))
                : null;
        }

        public override void Update(Vector2 scale, Vector2? origin = null, Axes adjustAxis = Axes.Both, float axisRotation = 0)
        {
            if (objectsInScale == null)
                throw new InvalidOperationException($"Cannot {nameof(Update)} a scale operation without calling {nameof(Begin)} first!");

            Debug.Assert(defaultOrigin != null && OriginalSurroundingQuad != null);

            var actualOrigin = ToScreenSpace(origin ?? defaultOrigin.Value);

            if (piecesInScale != null)
            {
                var keyboard = GetContainingInputManager()?.CurrentState.Keyboard;

                // Shift + corner zooms the part (keeping its shape), like every other skin element.
                if (!(adjustAxis == Axes.Both && keyboard?.ShiftPressed == true))
                {
                    resizePieces(scale, actualOrigin, adjustAxis, keyboard?.ControlPressed == true);
                    return;
                }
            }

            if ((adjustAxis == Axes.Y && !allSelectedSupportManualSizing(Axes.Y)) ||
                (adjustAxis == Axes.X && !allSelectedSupportManualSizing(Axes.X)))
                return;

            // If the selection has no area we cannot scale it
            if (OriginalSurroundingQuad.Value.Width == 0 || OriginalSurroundingQuad.Value.Height == 0)
                return;

            // for now aspect lock scale adjustments that occur at corners.
            if (adjustAxis == Axes.Both)
            {
                // project scale vector along diagonal
                scale = new Vector2((scale.X + scale.Y) * 0.5f);
            }
            // If any of the selection have been rotated and the adjust axis is not both,
            // we would require skew logic to achieve a correct image editor-like scale.
            // For now we just ignore, because it would likely not be the user's expected transform anyway.

            bool flippedX = scale.X < 0;
            bool flippedY = scale.Y < 0;
            Axes toFlip = Axes.None;

            if (flippedX != isFlippedX)
            {
                isFlippedX = flippedX;
                toFlip |= Axes.X;
            }

            if (flippedY != isFlippedY)
            {
                isFlippedY = flippedY;
                toFlip |= Axes.Y;
            }

            if (toFlip != Axes.None)
            {
                PerformFlipFromScaleHandles?.Invoke(toFlip);
                return;
            }

            foreach (var (b, originalState) in objectsInScale)
            {
                UpdatePosition(b, GeometryUtils.GetScaledPosition(scale, actualOrigin, originalState.ScreenSpaceOriginPosition));

                var currentScale = scale;
                if (Precision.AlmostEquals(MathF.Abs(b.Rotation) % 180, 90))
                    currentScale = new Vector2(scale.Y, scale.X);

                switch (adjustAxis)
                {
                    case Axes.X:
                        b.Width = MathF.Abs(originalState.Width * currentScale.X);
                        break;

                    case Axes.Y:
                        b.Height = MathF.Abs(originalState.Height * currentScale.Y);
                        break;

                    case Axes.Both:
                        b.Scale = originalState.Scale * currentScale;
                        break;
                }
            }
        }

        public override void Commit()
        {
            if (objectsInScale == null)
                throw new InvalidOperationException($"Cannot {nameof(Commit)} a scale operation without calling {nameof(Begin)} first!");

            changeHandler?.EndChange();

            objectsInScale = null;
            piecesInScale = null;
            defaultOrigin = null;
        }

        private Dictionary<SongSelectPieceLayout, OriginalPieceState>? piecesInScale;

        /// <summary>
        /// oily: resizes song select parts like windows (the part is laid out at the new size instead of being stretched).
        /// With <paramref name="crop"/>, the part stays where it is and the dragged edges cut it off (or uncover it) instead.
        /// </summary>
        private void resizePieces(Vector2 scale, Vector2 screenSpaceOrigin, Axes adjustAxis, bool crop)
        {
            Debug.Assert(piecesInScale != null);

            scale = new Vector2((adjustAxis & Axes.X) != 0 ? scale.X : 1, (adjustAxis & Axes.Y) != 0 ? scale.Y : 1);

            foreach (var (layout, original) in piecesInScale)
            {
                Quad quad = original.ScreenSpaceQuad;
                Vector2 xAxis = quad.TopRight - quad.TopLeft;
                Vector2 yAxis = quad.BottomLeft - quad.TopLeft;

                if (xAxis.LengthSquared < 1 || yAxis.LengthSquared < 1)
                    continue;

                var currentScale = scale;
                if (Precision.AlmostEquals(MathF.Abs(layout.Rotation) % 180, 90))
                    currentScale = new Vector2(scale.Y, scale.X);

                // Where the point that stays still is, as a fraction across the part (0 = left/top edge, 1 = right/bottom edge).
                Vector2 fromCorner = screenSpaceOrigin - quad.TopLeft;
                var still = new Vector2(Vector2.Dot(fromCorner, xAxis) / xAxis.LengthSquared, Vector2.Dot(fromCorner, yAxis) / yAxis.LengthSquared);

                Vector2 size = Vector2.ComponentMax(original.Size * currentScale, new Vector2(min_piece_size));

                if (crop)
                {
                    // The part's whole size stays the same; only how much of it is cut off changes.
                    Vector2 full = original.FullSize;
                    size = Vector2.ComponentMin(Vector2.ComponentMax(size, full * (1 - SongSelectPieceLayout.MAX_CROP)), full);

                    Vector2 shrink = original.Size - size;
                    float left = Math.Max(0, original.Crop.left + still.X * shrink.X / full.X);
                    float right = Math.Max(0, original.Crop.right + (1 - still.X) * shrink.X / full.X);
                    float top = Math.Max(0, original.Crop.top + still.Y * shrink.Y / full.Y);
                    float bottom = Math.Max(0, original.Crop.bottom + (1 - still.Y) * shrink.Y / full.Y);

                    layout.CropLeft.Value = left;
                    layout.CropRight.Value = right;
                    layout.CropTop.Value = top;
                    layout.CropBottom.Value = bottom;

                    // An edge can't be uncovered past the part's own edge.
                    size = new Vector2(full.X * (1 - left - right), full.Y * (1 - top - bottom));
                    Vector2 moved = new Vector2((left - original.Crop.left) * full.X, (top - original.Crop.top) * full.Y);
                    placeOrigin(layout, original, moved, size);
                }
                else
                {
                    layout.CropLeft.Value = original.Crop.left;
                    layout.CropRight.Value = original.Crop.right;
                    layout.CropTop.Value = original.Crop.top;
                    layout.CropBottom.Value = original.Crop.bottom;

                    Vector2 shrink = original.Size - size;
                    placeOrigin(layout, original, new Vector2(still.X * shrink.X, still.Y * shrink.Y), size);
                }
            }
        }

        private const float min_piece_size = 10;

        /// <summary>
        /// Sizes <paramref name="layout"/> and moves it so its top left corner is <paramref name="topLeft"/> (in its original, unscaled units) from where it was.
        /// </summary>
        private void placeOrigin(SongSelectPieceLayout layout, OriginalPieceState original, Vector2 topLeft, Vector2 size)
        {
            Quad quad = original.ScreenSpaceQuad;
            Vector2 local = topLeft + layout.RelativeOriginPosition * size;

            layout.Size = size;
            UpdatePosition(layout, quad.TopLeft + (quad.TopRight - quad.TopLeft) * (local.X / original.Size.X) + (quad.BottomLeft - quad.TopLeft) * (local.Y / original.Size.Y));
        }

        private readonly struct OriginalPieceState
        {
            public Vector2 Size { get; }
            public Vector2 FullSize { get; }
            public (float left, float top, float right, float bottom) Crop { get; }
            public Quad ScreenSpaceQuad { get; }

            public OriginalPieceState(SongSelectPieceLayout layout)
            {
                Size = layout.Size;
                FullSize = layout.FullSize;
                Crop = layout.Crop;
                ScreenSpaceQuad = layout.ScreenSpaceDrawQuad;
            }
        }

        private struct OriginalDrawableState
        {
            public float Width { get; }
            public float Height { get; }
            public Vector2 Scale { get; }
            public Vector2 ScreenSpaceOriginPosition { get; }

            public OriginalDrawableState(Drawable drawable)
            {
                Width = drawable.Width;
                Height = drawable.Height;
                Scale = drawable.Scale;
                ScreenSpaceOriginPosition = drawable.ToScreenSpace(drawable.OriginPosition);
            }
        }
    }
}
