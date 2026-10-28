// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Oily.Rendering.Audio;

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

        /// <summary>
        /// Encoder speed preset, for example "p4" for NVENC or "faster" for libx264. Null uses the encoder's default here.
        /// Faster presets render sooner and make slightly bigger files at the same quality.
        /// </summary>
        public string? Preset { get; init; }

        /// <summary>
        /// Whether the PP counter shows Akatsuki's relax pp instead of lazer's, for osu! plays with Relax.
        /// </summary>
        public bool AkatsukiPp { get; init; }

        /// <summary>Overall volume of the video's audio, 0 to 100.</summary>
        public int MasterVolume { get; init; } = 100;

        /// <summary>Volume of the hitsounds, 0 to 100, before <see cref="MasterVolume"/>.</summary>
        public int HitsoundVolume { get; init; } = 100;

        /// <summary>Volume of the song, 0 to 100, before <see cref="MasterVolume"/>.</summary>
        public int MusicVolume { get; init; } = 100;

        /// <summary>
        /// The volumes as the audio mix takes them.
        /// </summary>
        [JsonIgnore]
        public AudioVolumes Volumes => new AudioVolumes(MasterVolume / 100.0, HitsoundVolume / 100.0, MusicVolume / 100.0);

        /// <summary>
        /// Where frames are converted to the video's colour format: "gpu" (on the graphics card, falling back to the CPU if it can't) or "cpu".
        /// </summary>
        public string ColourConversion { get; init; } = "gpu";

        /// <summary>
        /// Motion blur: how many frames the game draws for each video frame, averaged together. 1 is off.
        /// Renders take about this many times longer, as every one of those frames is drawn.
        /// </summary>
        public int MotionBlurFrames { get; init; } = 1;

        /// <summary>How dark the background is, 0 (not at all) to 100 (black).</summary>
        public int BackgroundDim { get; init; } = 70;

        /// <summary>How blurred the background is, 0 to 100.</summary>
        public int BackgroundBlur { get; init; }

        /// <summary>Whether the beatmap's storyboard and video play.</summary>
        public bool Storyboard { get; init; } = true;

        /// <summary>Whether the interface (score, combo, health bar, PP counter...) shows.</summary>
        public bool ShowInterface { get; init; } = true;

        /// <summary>Whether hit circles light up when hit.</summary>
        public bool HitLighting { get; init; } = true;

        /// <summary>Whether the cursor leaves a trail, in osu! plays.</summary>
        public bool CursorTrail { get; init; } = true;

        /// <summary>The cursor's size, 0.1 to 2 times the skin's.</summary>
        public double CursorSize { get; init; } = 1;

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

        private static bool isPercentage(int value) => value >= 0 && value <= 100;

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
            if (!isPercentage(MasterVolume) || !isPercentage(HitsoundVolume) || !isPercentage(MusicVolume))
                throw new InvalidDataException("Volumes must be between 0 and 100.");
            if (ColourConversion is not ("gpu" or "cpu"))
                throw new InvalidDataException($"Colour conversion must be \"gpu\" or \"cpu\", not \"{ColourConversion}\".");
            if (MotionBlurFrames < 1 || MotionBlurFrames > GpuFrameBlender.MAX_FRAMES)
                throw new InvalidDataException($"Motion blur frames must be between 1 and {GpuFrameBlender.MAX_FRAMES}.");
            if (FramesPerSecond * MotionBlurFrames > 1000)
                throw new InvalidDataException($"Motion blur at {FramesPerSecond} fps would draw more than 1000 frames a second. Use fewer blur frames or a lower frame rate.");
            if (!isPercentage(BackgroundDim) || !isPercentage(BackgroundBlur))
                throw new InvalidDataException("Background dim and blur must be between 0 and 100.");
            if (!(CursorSize >= 0.1 && CursorSize <= 2))
                throw new InvalidDataException("Cursor size must be between 0.1 and 2.");
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
            // With motion blur the game draws several frames for each one in the video.
            FramesPerSecond = FramesPerSecond * MotionBlurFrames,
            BlendFrames = MotionBlurFrames,
            RecordAudio = Audio,
            AkatsukiPp = AkatsukiPp,
            CaptureSize = new System.Drawing.Size(Width, Height),
            GpuColourConversion = ColourConversion == "gpu",
            Duration = DurationSeconds is double seconds ? TimeSpan.FromSeconds(seconds) : null,
            Display = new RenderDisplay
            {
                BackgroundDim = BackgroundDim / 100.0,
                BackgroundBlur = BackgroundBlur / 100.0,
                Storyboard = Storyboard,
                Interface = ShowInterface,
                HitLighting = HitLighting,
                CursorTrail = CursorTrail,
                CursorSize = (float)CursorSize,
            },
        };
    }
}
