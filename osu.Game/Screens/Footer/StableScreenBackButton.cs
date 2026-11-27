// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Footer
{
    /// <summary>
    /// oily (ilo, 2026-11-26): osu!stable's back button: the skin's menu-back picture (animated when the skin has menu-back-0, -1, ...),
    /// drawn at its own size in the bottom left corner. Skins without one get stable's default look (a pink box with "back").
    /// </summary>
    public partial class StableScreenBackButton : OsuClickableContainer
    {
        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        private readonly Container pictureContainer;

        public StableScreenBackButton()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            Child = pictureContainer = new Container
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            onSkinChanged();
        }

        private void onSkinChanged()
        {
            var picture = skin.GetAnimation("menu-back", true, true, applyConfigFrameRate: true);

            if (picture != null)
            {
                picture.Anchor = Anchor.BottomLeft;
                picture.Origin = Anchor.BottomLeft;
                pictureContainer.Child = picture;
                return;
            }

            pictureContainer.Child = new Container
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Size = new Vector2(93, 45),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(238, 51, 153, 255),
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6, 0),
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Icon = FontAwesome.Solid.ChevronLeft,
                                Size = new Vector2(14),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = "back",
                                Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                            },
                        },
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            pictureContainer.FadeColour(new Color4(1.2f, 1.2f, 1.2f, 1), 100, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            pictureContainer.FadeColour(Color4.White, 200, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }
    }
}
