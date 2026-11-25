// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: the pp an SS would give on a beatmap card, with the selected mods. It follows mod changes like the star rating does.
    /// </summary>
    public partial class PanelPerformanceDisplay : CompositeDrawable
    {
        private readonly OsuSpriteText text;

        [Resolved]
        private SongSelectPieceRegistry? pieceRegistry { get; set; }

        public PanelPerformanceDisplay()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;

            InternalChild = text = new OsuSpriteText
            {
                Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            text.Colour = colourProvider.Content2;
        }

        public StarDifficulty Difficulty
        {
            set
            {
                double? pp = value.PerformanceAttributes?.Total;
                text.Text = pp == null || pp <= 0 ? string.Empty : $"{Math.Round(pp.Value, MidpointRounding.AwayFromZero):N0}pp";
            }
        }

        protected override void Update()
        {
            base.Update();

            Alpha = pieceRegistry?.CardStyle.ShowPerformance.Value == false ? 0 : 1;
        }
    }
}
