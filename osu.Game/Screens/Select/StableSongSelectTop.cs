// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: osu!stable's top bar behind song select's beatmap info and search bar, shown while they use the Stable style.
    /// Like stable, the skin's songselect-top picture is drawn at its own size from the top left of the screen, and its last column
    /// is stretched to the right edge. Without one, a dark bar goes there.
    /// </summary>
    public partial class StableSongSelectTop : CompositeDrawable
    {
        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SongSelectPieceRegistry registry { get; set; } = null!;

        private readonly IBindable<bool> shown = new BindableBool();

        public StableSongSelectTop()
        {
            RelativeSizeAxes = Axes.X;
            Alpha = 0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            shown.BindTo(registry.StableTopBar);
            shown.BindValueChanged(v => this.FadeTo(v.NewValue ? 1 : 0, 200, Easing.OutQuint), true);

            skin.SourceChanged += onSkinChanged;
            onSkinChanged();
        }

        private void onSkinChanged()
        {
            var top = skin.GetTexture("songselect-top");

            if (top == null)
            {
                Height = FilterControl.HEIGHT_FROM_SCREEN_TOP;
                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.6f),
                };
                return;
            }

            Height = top.DisplayHeight;
            InternalChildren = new Drawable[]
            {
                new Sprite
                {
                    Texture = top.Crop(new RectangleF(0.995f, 0, 0.005f, 1), Axes.Both),
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = top.DisplayHeight,
                },
                new Sprite
                {
                    Texture = top,
                    Size = new Vector2(top.DisplayWidth, top.DisplayHeight),
                },
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }
    }
    /// <summary>
    /// oily (ilo, 2026-11-27): in osu!stable the top bar is drawn over the beatmap list, so cards scroll under it and the
    /// search, group and sort boxes sit on the bar. This draws the right part of <see cref="StableSongSelectTop"/> again in front of the cards
    /// (only over the list, so the beatmap info on the left stays on top), and keeps clicks on the bar from reaching the cards.
    /// </summary>
    public partial class StableTopBarCover : CompositeDrawable
    {
        private readonly Drawable screen;
        private readonly StableSongSelectTop bar;

        /// <param name="screen">Song select, whose top left corner the bar starts from.</param>
        public StableTopBarCover(Drawable screen)
        {
            this.screen = screen;

            Masking = true;
            InternalChild = bar = new StableSongSelectTop
            {
                RelativeSizeAxes = Axes.None,
            };
        }

        protected override void Update()
        {
            base.Update();

            if (Parent == null)
                return;

            var screenQuad = screen.ScreenSpaceDrawQuad;
            float toScreen = screen.DrawHeight > 0 ? screenQuad.Height / screen.DrawHeight : 1;

            // From a little left of the list (selected cards stick out) to the screen's right edge, as tall as the bar.
            float left = Parent.ScreenSpaceDrawQuad.TopLeft.X - 80 * toScreen;
            Vector2 topLeft = Parent.ToLocalSpace(new Vector2(left, screenQuad.TopLeft.Y));
            Vector2 bottomRight = Parent.ToLocalSpace(new Vector2(screenQuad.TopRight.X, screenQuad.TopLeft.Y + bar.Height * toScreen));

            Position = topLeft;
            Size = Vector2.ComponentMax(Vector2.Zero, bottomRight - topLeft);

            float screenLeft = ToLocalSpace(screenQuad.TopLeft).X;
            bar.X = screenLeft;
            bar.Width = ToLocalSpace(screenQuad.TopRight).X - screenLeft;
        }

        private bool shown => bar.Alpha > 0;

        protected override bool OnHover(HoverEvent e) => shown;

        protected override bool OnMouseDown(MouseDownEvent e) => shown;

        protected override bool OnClick(ClickEvent e) => shown;
    }
}
