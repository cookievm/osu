// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Screens.Footer;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: opens the Universal PP Simulator for the selected beatmap.
    /// </summary>
    public partial class FooterButtonPerformanceSimulator : ScreenFooterButton
    {
        /// <summary>
        /// Opens or closes the simulator.
        /// </summary>
        public Action? Toggle { get; init; }

        /// <summary>
        /// Whether the simulator is open, so the button lights up like the other buttons do while their overlay is open.
        /// </summary>
        public IBindable<Visibility>? SimulatorState { get; init; }

        private readonly IBindable<Visibility> simulatorState = new Bindable<Visibility>();

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Text = "PP Simulator";
            IconText = "pp";
            AccentColour = colours.Yellow;
            Action = () => Toggle?.Invoke();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (SimulatorState != null)
            {
                simulatorState.BindTo(SimulatorState);
                simulatorState.BindValueChanged(v => OverlayState.Value = v.NewValue, true);
            }

            workingBeatmap.BindValueChanged(_ => Enabled.Value = !workingBeatmap.IsDefault, true);
        }
    }
}
