// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Oily.Rendering
{
    /// <summary>
    /// Where a render's time goes, summed over every frame and logged when it finishes, so a slow stage shows up.
    /// </summary>
    public static class RenderTimings
    {
        /// <summary>
        /// Game update and draw, until the frame's copy back from the graphics card has started.
        /// </summary>
        public static Stage Draw { get; } = new Stage("draw");

        /// <summary>
        /// The game waiting for room in the queue to ffmpeg.
        /// </summary>
        public static Stage QueueFull { get; } = new Stage("queue full");

        /// <summary>
        /// The writer waiting for a frame's pixels to come back from the graphics card.
        /// </summary>
        public static Stage WaitPixels { get; } = new Stage("wait pixels");

        /// <summary>
        /// Sending pixels to ffmpeg, which includes waiting for ffmpeg to take them.
        /// </summary>
        public static Stage Ffmpeg { get; } = new Stage("ffmpeg");

        /// <summary>
        /// Mapping and copying pixels out of a staging texture, on the draw thread.
        /// </summary>
        public static Stage Readback { get; } = new Stage("readback copy");

        /// <summary>
        /// The draw thread waiting for the oldest copy because every staging texture was busy.
        /// </summary>
        public static Stage ReadbackStall { get; } = new Stage("readback stall");

        private static readonly Stage[] all = { Draw, QueueFull, WaitPixels, Ffmpeg, Readback, ReadbackStall };

        public static void Reset()
        {
            foreach (var stage in all)
                stage.Reset();
        }

        /// <summary>
        /// One line per stage: count, total and average milliseconds.
        /// </summary>
        public static string Summary(double totalSeconds, int frames)
        {
            var text = new System.Text.StringBuilder();
            text.Append(CultureInfo.InvariantCulture, $"Render timings: {frames} frames in {totalSeconds:0.0} s ({(totalSeconds > 0 ? frames / totalSeconds : 0):0.0} fps)");

            foreach (var stage in all)
                text.Append(CultureInfo.InvariantCulture, $"\n  {stage.Name}: {stage.Count} times, {stage.TotalMilliseconds / 1000:0.0} s, {stage.AverageMilliseconds:0.00} ms each");

            return text.ToString();
        }

        public sealed class Stage
        {
            public string Name { get; }

            private long ticks;
            private long count;

            public Stage(string name)
            {
                Name = name;
            }

            public long Count => Interlocked.Read(ref count);

            public double TotalMilliseconds => Interlocked.Read(ref ticks) * 1000.0 / Stopwatch.Frequency;

            public double AverageMilliseconds => Count == 0 ? 0 : TotalMilliseconds / Count;

            /// <summary>
            /// Adds the time since <paramref name="startTimestamp"/>, a <see cref="Stopwatch.GetTimestamp"/>.
            /// </summary>
            public void AddSince(long startTimestamp)
            {
                Interlocked.Add(ref ticks, Stopwatch.GetTimestamp() - startTimestamp);
                Interlocked.Increment(ref count);
            }

            public void Reset()
            {
                Interlocked.Exchange(ref ticks, 0);
                Interlocked.Exchange(ref count, 0);
            }
        }
    }
}
