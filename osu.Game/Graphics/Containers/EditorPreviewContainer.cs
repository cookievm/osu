// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Utils;
using osuTK;

namespace osu.Game.Graphics.Containers
{
    /// <summary>
    /// oily: shrinks the whole game screen evenly (keeping its shape) into a box, so the skin editor shows exactly what the game shows.
    /// Nothing inside is laid out again, so a part placed in the editor lands in the same place once the editor closes.
    /// </summary>
    public partial class EditorPreviewContainer : Container
    {
        /// <summary>
        /// Stops clicks and keys reaching what's inside (the toolbars, while the skin editor is open).
        /// </summary>
        public bool BlockInput { get; set; }

        public override bool PropagatePositionalInputSubTree => !BlockInput && base.PropagatePositionalInputSubTree;
        public override bool PropagateNonPositionalInputSubTree => !BlockInput && base.PropagateNonPositionalInputSubTree;

        // Like lazer's own screen scaling, the mouse still reaches the screen outside the shrunk box (its edges stay usable).
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        private RectangleF? previewRect;

        public EditorPreviewContainer()
        {
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Fits the screen inside <paramref name="rect"/> (relative to the full screen), or <c>null</c> to fill the screen again.
        /// </summary>
        public void SetPreviewRect(RectangleF? rect) => previewRect = rect;

        /// <summary>
        /// How much the screen is shrunk, and where its top left corner goes (relative to the full screen), to fit <paramref name="rect"/>.
        /// </summary>
        public static (Vector2 position, float scale) Fit(RectangleF rect)
        {
            float scale = Math.Min(rect.Width, rect.Height);
            return (rect.Location + (rect.Size - new Vector2(scale)) / 2, scale);
        }

        protected override void Update()
        {
            base.Update();

            Vector2 targetPosition = Vector2.Zero;
            float targetScale = 1;

            if (previewRect is RectangleF rect && Parent != null)
            {
                (Vector2 relativePosition, float scale) = Fit(rect);
                targetPosition = relativePosition * Parent.ChildSize;
                targetScale = scale;
            }

            Position = new Vector2(
                (float)Interpolation.DampContinuously(Position.X, targetPosition.X, 60, Time.Elapsed),
                (float)Interpolation.DampContinuously(Position.Y, targetPosition.Y, 60, Time.Elapsed));
            Scale = new Vector2((float)Interpolation.DampContinuously(Scale.X, targetScale, 60, Time.Elapsed));

            if (Precision.AlmostEquals(Position, targetPosition, 0.1f) && Precision.AlmostEquals(Scale.X, targetScale, 0.0001f))
            {
                Position = targetPosition;
                Scale = new Vector2(targetScale);
            }

            bool shrunk = Scale.X < 1;
            Masking = shrunk;
            CornerRadius = shrunk ? 10 / Scale.X : 0;
        }
    }
}
