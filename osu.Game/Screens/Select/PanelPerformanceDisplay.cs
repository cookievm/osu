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
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: on a beatmap card, the o!ily pp an SS would give with the selected mods (Relax uses Akatsuki's relax pp),
    /// then the card's pretend score (accuracy and misses) and its pp. Both follow mod changes.
    /// </summary>
    public partial class PanelPerformanceDisplay : CompositeDrawable
    {
        private readonly OsuSpriteText ssText;
        private readonly OsuSpriteText simulatedText;

        [Resolved]
        private SongSelectPieceRegistry? pieceRegistry { get; set; }

        [Resolved]
        private SSPerformanceStore? performanceStore { get; set; }

        [Resolved]
        private PerformanceSimulationStore? simulations { get; set; }

        private BeatmapInfo? beatmap;
        private int requestedGeneration = -1;
        private double? lazerPerformance;

        private string? shownSS;
        private string? shownSimulated;

        /// <summary>
        /// oily: always show the SS and simulated pp, whatever the card style says (stable cards always show them).
        /// </summary>
        public bool AlwaysShow { get; init; }

        /// <summary>
        /// oily: draw the text white, so it takes the colour of what it sits in (stable cards colour their text from the skin).
        /// </summary>
        public bool PlainColour { get; init; }

        public PanelPerformanceDisplay()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;

            InternalChild = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    ssText = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                    },
                    simulatedText = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                    },
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            if (PlainColour)
                return;

            ssText.Colour = colourProvider.Content2;
            simulatedText.Colour = colourProvider.Content2;
        }

        /// <summary>
        /// The card's beatmap. Its o!ily pp comes from <see cref="SSPerformanceStore"/> on song select.
        /// </summary>
        public BeatmapInfo? Beatmap
        {
            set
            {
                beatmap = value;
                requestedGeneration = -1;
            }
        }

        /// <summary>
        /// Lazer's star rating and pp, used when there is no <see cref="SSPerformanceStore"/> (outside song select).
        /// </summary>
        public StarDifficulty Difficulty
        {
            set => lazerPerformance = value.PerformanceAttributes?.Total;
        }

        /// <summary>
        /// The pretend score text shown on the card, for tests.
        /// </summary>
        public string SimulatedText => simulatedText.Text.ToString();

        protected override void Update()
        {
            base.Update();

            var style = AlwaysShow ? null : pieceRegistry?.CardStyle;
            Alpha = style?.ShowPerformance.Value == false && style.ShowSimulatedPerformance.Value == false ? 0 : 1;

            double? ss;
            string simulated = string.Empty;

            if (performanceStore != null && beatmap != null)
            {
                ss = performanceStore.Get(beatmap);
                double? simulatedPP = performanceStore.GetSimulated(beatmap);

                if ((ss == null || simulatedPP == null) && requestedGeneration != performanceStore.Generation)
                {
                    requestedGeneration = performanceStore.Generation;
                    performanceStore.RequestFirst(beatmap);
                }

                if (simulatedPP != null && simulations != null && style?.ShowSimulatedPerformance.Value != false)
                    simulated = $"{simulations.Get(beatmap).ShortText}: {format(simulatedPP.Value)}pp";
            }
            else
                ss = lazerPerformance;

            string ssString = style?.ShowPerformance.Value == false || ss == null || ss <= 0 ? string.Empty : $"SS: {format(ss.Value)}pp";

            if (ssString != shownSS)
            {
                shownSS = ssString;
                ssText.Text = ssString;
                ssText.Alpha = ssString.Length > 0 ? 1 : 0;
            }

            if (simulated != shownSimulated)
            {
                shownSimulated = simulated;
                simulatedText.Text = simulated;
                simulatedText.Alpha = simulated.Length > 0 ? 1 : 0;
            }
        }

        private static string format(double pp) => Math.Round(pp, MidpointRounding.AwayFromZero).ToString("N0");
    }
}
