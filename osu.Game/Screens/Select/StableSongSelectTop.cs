// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
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
    /// (ilo, 2026-11-27) Skins like WhiteCat also paint the top right (collections, search, dashed lines) into their selection-mode
    /// and mode-osu-small pictures, which stable draws from the bottom bar. Only the parts of those that reach the top of the screen are drawn here,
    /// so the bottom bar stays lazer's.
    /// </summary>
    public partial class StableSongSelectTop : CompositeDrawable
    {
        /// <summary>
        /// Stable's screen is 768 high; pictures are drawn in that space.
        /// </summary>
        private const float stable_height = 768;

        /// <summary>
        /// Parts of the bottom bar pictures lower than this (in stable's space) are left out.
        /// </summary>
        private const float top_part_limit = 400;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SongSelectPieceRegistry registry { get; set; } = null!;

        private readonly IBindable<bool> shown = new BindableBool();

        private readonly Container art;

        private float artHeight;

        /// <summary>
        /// Whether this one tells song select if the skin has its own top right panel (the copy over the cards doesn't).
        /// </summary>
        public bool ReportsSkinPanel { get; init; } = true;

        /// <summary>
        /// How tall song select's screen is here. Without it, the parent's height is used.
        /// </summary>
        public float? ScreenHeight { get; set; }

        public StableSongSelectTop()
        {
            RelativeSizeAxes = Axes.X;
            Alpha = 0;
            InternalChild = art = new Container();
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
            art.Clear();

            var top = skin.GetTexture("songselect-top");

            if (top == null)
            {
                // Like stable's own top bar: dark, down past the ranking box.
                artHeight = 148;
                art.Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.8f),
                });
            }
            else
            {
                artHeight = top.DisplayHeight;
                art.AddRange(new Drawable[]
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
                });
            }

            // Stable draws selection-mode from its bottom left at 224 from the left, and the mode icon centred on it.
            bool panel = addTopPart(skin.GetTexture("selection-mode"), new Vector2(224, stable_height), Anchor.BottomLeft);
            addTopPart(skin.GetTexture("mode-osu-small"), new Vector2(268, 713), Anchor.Centre);

            if (ReportsSkinPanel)
                registry.StableSkinPanel.Value = panel;
        }

        /// <summary>
        /// Draws the part of a bottom bar picture that reaches up into the top of the screen. Returns whether there was any.
        /// </summary>
        private bool addTopPart(Texture? texture, Vector2 position, Anchor origin)
        {
            if (texture == null || texture.DisplayHeight <= 0)
                return false;

            var size = new Vector2(texture.DisplayWidth, texture.DisplayHeight);
            var topLeft = position - (origin == Anchor.Centre ? size / 2 : new Vector2(0, size.Y));

            float visible = Math.Min(size.Y, top_part_limit - topLeft.Y);

            // Nothing above the limit (a normal small button).
            if (visible <= 0)
                return false;

            art.Add(new Sprite
            {
                Texture = texture.Crop(new RectangleF(0, 0, 1, visible / size.Y), Axes.Both),
                Position = topLeft,
                Size = new Vector2(size.X, visible),
            });

            artHeight = Math.Max(artHeight, topLeft.Y + visible);
            return true;
        }

        protected override void Update()
        {
            base.Update();

            float screenHeight = ScreenHeight ?? Parent?.DrawHeight ?? stable_height;
            float scale = screenHeight > 0 ? screenHeight / stable_height : 1;

            art.Scale = new Vector2(scale);
            art.Size = new Vector2(DrawWidth / scale, artHeight);
            Height = artHeight * scale;
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
                ReportsSkinPanel = false,
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

            // The parent may have padding, so move by how far off we are rather than setting the position from its corner.
            Size = Vector2.ComponentMax(Vector2.Zero, bottomRight - topLeft);
            Position += topLeft - Parent.ToLocalSpace(ScreenSpaceDrawQuad.TopLeft);

            float screenLeft = ToLocalSpace(screenQuad.TopLeft).X;
            bar.ScreenHeight = ToLocalSpace(screenQuad.BottomLeft).Y - ToLocalSpace(screenQuad.TopLeft).Y;
            bar.X = screenLeft;
            bar.Width = ToLocalSpace(screenQuad.TopRight).X - screenLeft;
        }

        private bool shown => bar.Alpha > 0;

        protected override bool OnHover(HoverEvent e) => shown;

        protected override bool OnMouseDown(MouseDownEvent e) => shown;

        protected override bool OnClick(ClickEvent e) => shown;
    }
}
