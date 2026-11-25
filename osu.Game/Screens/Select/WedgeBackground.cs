// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Overlays;

namespace osu.Game.Screens.Select
{
    internal sealed partial class WedgeBackground : InputBlockingContainer
    {
        public float StartAlpha { get; init; } = 0.9f;

        public float FinalAlpha { get; init; } = 0.6f;

        public float WidthForGradient { get; init; } = 0.3f;

        /// <summary>
        /// oily: which panel this is, so its colour and opacity can be changed in settings.
        /// </summary>
        public OilyPanel? Panel { get; init; }

        private Box glow = null!;
        private Box main = null!;
        private Box gradient = null!;
        private IBindable<string>? panelSetting;

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager? config)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                glow = new Box
                {
                    Blending = BlendingParameters.Additive,
                    RelativeSizeAxes = Axes.Both,
                    Width = 0.6f,
                    Alpha = 0.5f,
                    Colour = ColourInfo.GradientHorizontal(colourProvider.Background2, colourProvider.Background2.Opacity(0)),
                },
                main = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = 1 - WidthForGradient,
                    Colour = colourProvider.Background5.Opacity(StartAlpha),
                },
                gradient = new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Both,
                    Width = WidthForGradient,
                    Colour = ColourInfo.GradientHorizontal(colourProvider.Background5.Opacity(StartAlpha), colourProvider.Background5.Opacity(FinalAlpha)),
                },
            };

            if (Panel != null && config != null)
            {
                panelSetting = OilyPanelColours.Track(config, Panel.Value, style =>
                {
                    var background = style.CustomColour ?? colourProvider.Background5;
                    var highlight = style.CustomColour?.Lighten(0.3f) ?? colourProvider.Background2;

                    glow.Colour = ColourInfo.GradientHorizontal(highlight, highlight.Opacity(0));
                    main.Colour = background.Opacity(StartAlpha);
                    gradient.Colour = ColourInfo.GradientHorizontal(background.Opacity(StartAlpha), background.Opacity(FinalAlpha));
                    Alpha = Math.Clamp(style.Opacity, 0, 1);
                });
            }
        }
    }
}
