// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Timing;

namespace Oily.Rendering
{
    /// <summary>
    /// Stands in for the music track as the source of gameplay time.
    /// Time only moves when <see cref="Advance"/> is called, so every render of a replay sees exactly the same times.
    /// </summary>
    public class RenderSourceClock : IAdjustableClock
    {
        /// <summary>
        /// Returns the speed change applied by mods (for example 1.5 for Double Time).
        /// </summary>
        public Func<double> ModRate { get; set; } = () => 1;

        private double baseRate = 1;

        public double CurrentTime { get; private set; }

        public bool IsRunning { get; private set; }

        public double Rate
        {
            get => baseRate * ModRate();
            set => baseRate = value;
        }

        /// <summary>
        /// Moves time forward by an amount of video time. Mod speed changes are applied here.
        /// </summary>
        public void Advance(double videoElapsed)
        {
            if (IsRunning)
                CurrentTime += videoElapsed * Rate;
        }

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void Reset()
        {
            Stop();
            CurrentTime = 0;
        }

        public bool Seek(double position)
        {
            CurrentTime = position;
            return true;
        }

        public void ResetSpeedAdjustments() => baseRate = 1;
    }
}
