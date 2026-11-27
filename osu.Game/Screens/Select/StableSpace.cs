// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily (ilo, 2026-11-27): osu!stable's screen space (768 high, as wide as the screen), lined up with the whole song select screen
    /// even when this sits inside something smaller or scaled. Stable's song select parts are placed in here with stable's numbers.
    /// </summary>
    public partial class StableSpace : Container
    {
        public const float HEIGHT = 768;

        private readonly Drawable screen;
        private readonly Container content;

        protected override Container<Drawable> Content => content;

        /// <param name="screen">Song select, whose corners stable's screen matches.</param>
        public StableSpace(Drawable screen)
        {
            this.screen = screen;

            RelativeSizeAxes = Axes.Both;
            InternalChild = content = new Container();
        }

        protected override void Update()
        {
            base.Update();

            var quad = screen.ScreenSpaceDrawQuad;
            var topLeft = ToLocalSpace(quad.TopLeft);
            float height = ToLocalSpace(quad.BottomLeft).Y - topLeft.Y;
            float width = ToLocalSpace(quad.TopRight).X - topLeft.X;
            float scale = height > 0 ? height / HEIGHT : 1;

            content.Position = topLeft;
            content.Scale = new Vector2(scale);
            content.Size = new Vector2(width / scale, HEIGHT);
        }
    }
}
