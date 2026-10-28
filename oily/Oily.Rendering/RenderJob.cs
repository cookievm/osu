// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oily.Rendering
{
    /// <summary>
    /// A render requested from outside the game, for example by the o!ily companion app.
    /// Passed as a JSON file: <c>oily --render-job=C:\path\job.json</c>.
    /// </summary>
    public record RenderJob
    {
        /// <summary>The replay to render (.osr).</summary>
        public string Replay { get; init; } = string.Empty;

        /// <summary>The beatmapset to import first (.osz), if the game doesn't have it yet.</summary>
        public string? Beatmap { get; init; }

        /// <summary>A skin to import and use (.osk), for example one exported from lazer with its skin editor layout.</summary>
        public string? Skin { get; init; }

        /// <summary>Where the video goes (.mp4).</summary>
        public string Output { get; init; } = string.Empty;

        public int Width { get; init; } = 1920;
        public int Height { get; init; } = 1080;
        public int FramesPerSecond { get; init; } = 60;

        /// <summary>ffmpeg video encoder, for example "libx264" or "h264_nvenc".</summary>
        public string Encoder { get; init; } = "libx264";

        /// <summary>Quality: CRF for libx264, CQ for NVENC. Lower is better.</summary>
        public int Quality { get; init; } = 18;

        /// <summary>Path to ffmpeg.exe, or "ffmpeg" to find it on PATH.</summary>
        public string Ffmpeg { get; init; } = "ffmpeg";

        /// <summary>Whether to hide the engine's window while it renders. Frames are drawn off-screen either way.</summary>
        public bool Hidden { get; init; } = true;

        /// <summary>Whether the video gets the music and hitsounds.</summary>
        public bool Audio { get; init; } = true;

        /// <summary>Only render this many seconds of video. Null renders the whole replay.</summary>
        public double? DurationSeconds { get; init; }

        private static readonly JsonSerializerOptions json_options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
        };

        public static RenderJob Parse(string json)
        {
            var job = JsonSerializer.Deserialize<RenderJob>(json, json_options)
                      ?? throw new InvalidDataException("The render job file is empty.");
            job.Validate();
            return job;
        }

        public static RenderJob Load(string path) => Parse(File.ReadAllText(path));

        public string ToJson() => JsonSerializer.Serialize(this, json_options);

        /// <summary>
        /// Throws if the job can't be rendered as written.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Replay))
                throw new InvalidDataException("The render job has no replay.");
            if (string.IsNullOrWhiteSpace(Output))
                throw new InvalidDataException("The render job has no output path.");
            if (Width < 16 || Height < 16 || Width > 7680 || Height > 4320)
                throw new InvalidDataException($"Unsupported video size {Width}x{Height}.");
            if (Width % 2 != 0 || Height % 2 != 0)
                throw new InvalidDataException("Video width and height must be even numbers.");
            if (FramesPerSecond < 1 || FramesPerSecond > 1000)
                throw new InvalidDataException($"Unsupported frame rate {FramesPerSecond}.");
            if (Quality < 0 || Quality > 51)
                throw new InvalidDataException("Quality must be between 0 and 51.");
            if (DurationSeconds <= 0)
                throw new InvalidDataException("Duration must be positive.");
        }

        /// <summary>
        /// The window size to render in: the video's size if it fits on the display, otherwise the largest size that does,
        /// with the video's aspect ratio so the off-screen frames aren't stretched.
        /// </summary>
        /// <remarks>
        /// Some parts of lazer (slider bodies, for one) are drawn at the window's resolution, so a bigger window gives sharper frames.
        /// </remarks>
        public System.Drawing.Size WindowSizeFor(System.Drawing.Size display)
        {
            if (display.Width <= 0 || display.Height <= 0)
                return new System.Drawing.Size(Width, Height);

            // Leave room for the taskbar and window borders, which Windows won't let a window grow over.
            double scale = Math.Min(1, Math.Min(display.Width * 0.9 / Width, display.Height * 0.9 / Height));

            return new System.Drawing.Size(Math.Max(16, (int)(Width * scale) / 2 * 2), Math.Max(16, (int)(Height * scale) / 2 * 2));
        }

        public RenderSettings ToRenderSettings() => new RenderSettings
        {
            FramesPerSecond = FramesPerSecond,
            RecordAudio = Audio,
            CaptureSize = new System.Drawing.Size(Width, Height),
            Duration = DurationSeconds is double seconds ? TimeSpan.FromSeconds(seconds) : null,
        };
    }
}
