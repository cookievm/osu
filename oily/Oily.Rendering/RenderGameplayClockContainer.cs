// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens.Play;

namespace Oily.Rendering
{
    /// <summary>
    /// A gameplay clock driven by the render loop instead of the music track.
    /// </summary>
    /// <remarks>
    /// Offsets and interpolation are off: both depend on the user's settings or the wall clock,
    /// and frame N must always show gameplay at exactly the same time.
    /// </remarks>
    public partial class RenderGameplayClockContainer : GameplayClockContainer
    {
        private readonly RenderSourceClock source;

        public RenderGameplayClockContainer(double startTime, double gameplayStartTime)
            : this(new RenderSourceClock(), startTime, gameplayStartTime)
        {
        }

        private RenderGameplayClockContainer(RenderSourceClock source, double startTime, double gameplayStartTime)
            : base(source, applyOffsets: false, requireDecoupling: false, interpolate: false)
        {
            this.source = source;

            source.ModRate = () => AdjustmentsFromMods.AggregateTempo.Value * AdjustmentsFromMods.AggregateFrequency.Value;

            StartTime = startTime;
            GameplayStartTime = gameplayStartTime;
        }

        protected override void Update()
        {
            // Time here is the render loop's video clock, inherited from the parent.
            // This runs before children update, so the gameplay clock below picks up the new time in this same frame.
            source.Advance(Time.Elapsed);

            base.Update();
        }
    }
}
