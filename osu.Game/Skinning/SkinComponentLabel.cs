// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Skinning
{
    /// <summary>
    /// oily: a skin component that shows its own name in the skin editor's component list, so the list doesn't name it twice.
    /// </summary>
    public interface ISkinComponentWithLabel : ISerialisableDrawable
    {
    }

    /// <summary>
    /// oily: the bold name in a box that such components show in the skin editor's component list.
    /// </summary>
    public partial class SkinComponentLabel : CompositeDrawable
    {
        private readonly string text;

        public SkinComponentLabel(string text)
        {
            this.text = text;
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChild = new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 5,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Gray2,
                    },
                    new OsuSpriteText
                    {
                        Margin = new MarginPadding { Horizontal = 10, Vertical = 5 },
                        Text = text,
                        Font = OsuFont.Torus.With(size: 14, weight: FontWeight.Bold),
                        Colour = Colour4.White,
                    },
                }
            };
        }
    }
}
