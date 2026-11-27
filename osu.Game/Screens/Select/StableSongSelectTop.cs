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
}
