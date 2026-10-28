// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace Oily.Rendering
{
    /// <summary>
    /// What to render and how.
    /// </summary>
    public record RenderSettings
    {
        /// <summary>
        /// Frames per second of the output video.
        /// </summary>
        public int FramesPerSecond { get; init; } = 60;

        /// <summary>
        /// How much of the replay to render, in video time. <c>null</c> renders until the replay ends.
        /// </summary>
        public TimeSpan? Duration { get; init; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// How long before the first hit object the render starts, in gameplay milliseconds.
        /// </summary>
        public double LeadIn { get; init; } = 1000;

        /// <summary>
        /// How long to keep rendering after the play ends (or fails), in video milliseconds,
        /// so the video doesn't cut off on the last hit.
        /// </summary>
        public double Tail { get; init; } = 1500;

        /// <summary>
        /// Whether to record every sample played, so the video's audio can be mixed afterwards. See <see cref="Audio.RenderAudioRecorder"/>.
        /// </summary>
        public bool RecordAudio { get; init; }

        /// <summary>
        /// Whether the PP counter shows Akatsuki's relax pp instead of lazer's, for osu! plays with Relax. See <see cref="Akatsuki.AkatsukiRelaxLiveCalculator"/>.
        /// </summary>
        public bool AkatsukiPp { get; init; }

        /// <summary>
        /// When set, frames are drawn off-screen at this size and read straight from the graphics card,
        /// instead of screenshotting the window. The window only needs the same aspect ratio, and can be hidden.
        /// </summary>
        public System.Drawing.Size? CaptureSize { get; init; }

        /// <summary>
        /// Whether captured frames are converted to the video's colour format on the graphics card, when it can, rather than on the CPU.
        /// Only applies with <see cref="CaptureSize"/>.
        /// </summary>
        public bool GpuColourConversion { get; init; } = true;

        /// <summary>
        /// The length of one frame in milliseconds.
        /// </summary>
        public double FrameDuration => 1000.0 / FramesPerSecond;

        /// <summary>
        /// The video time of a frame in milliseconds.
        /// Computed from the frame index rather than accumulated, so long renders don't drift.
        /// </summary>
        public double FrameTime(int frameIndex) => frameIndex * 1000.0 / FramesPerSecond;

        /// <summary>
        /// How many frames to render, or <c>null</c> to render until the replay ends.
        /// </summary>
        public int? FrameCount => Duration == null ? null : (int)Math.Ceiling(Duration.Value.TotalMilliseconds / FrameDuration);
    }
}
