// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Skinning.Components
{
    /// <summary>
    /// oily: the player's global osu! rank, for example "#12,345". Only renders know it (see <see cref="PlayerGlobalRank"/>); it's hidden otherwise.
    /// </summary>
    [UsedImplicitly]
    public partial class GlobalRankDisplay : FontAdjustableSkinComponent
    {
        private readonly OsuSpriteText text;

        public GlobalRankDisplay()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = text = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (PlayerGlobalRank.Current is int rank)
                text.Text = $"#{rank:N0}";
            else
                Alpha = 0;
        }

        protected override void SetFont(FontUsage font) => text.Font = font.With(size: 40);

        protected override void SetTextColour(Colour4 textColour) => text.Colour = textColour;
    }
}
