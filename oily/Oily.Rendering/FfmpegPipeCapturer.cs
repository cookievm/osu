// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace Oily.Rendering
{
    /// <summary>
    /// Sends every frame straight to ffmpeg as raw pixels, which encodes the video as it goes.
    /// No PNGs are written.
    /// </summary>
    /// <remarks>
    /// Frames are usually already the job's size (see <see cref="RenderSettings.CaptureSize"/>).
    /// Window screenshots may not be, and ffmpeg scales those.
    /// </remarks>
    public class FfmpegPipeCapturer : IFrameCapturer
    {
        /// <summary>
        /// Frames that may wait for ffmpeg at once. Each 4K frame is about 33 MB.
        /// </summary>
        private const int max_queued_frames = 4;

        private readonly Func<Task<FrameReadback>> grabFrame;
        private readonly RenderJob job;

        // Frames go to ffmpeg in order on a background task, so the game can draw the next frame while one is written.
        private readonly Channel<Task<RenderFrame>> queue = Channel.CreateBounded<Task<RenderFrame>>(new BoundedChannelOptions(max_queued_frames)
        {
            SingleReader = true,
            SingleWriter = true,
        });

        private Task? writer;

        private Process? ffmpeg;
        private Stream? input;
        private int frameWidth;
        private int frameHeight;
        private readonly StringBuilder ffmpegLog = new StringBuilder();

        public string OutputDescription => job.Output;

        /// <param name="grabFrame">Gets the current frame, for example <see cref="ReplayRenderScreen.GrabFrameAsync"/>.</param>
        /// <param name="job">The render job, for the ffmpeg settings and output path.</param>
        public FfmpegPipeCapturer(Func<Task<FrameReadback>> grabFrame, RenderJob job)
        {
            this.grabFrame = grabFrame;
            this.job = job;
        }

        public async Task CaptureFrameAsync(RenderedFrame frame)
        {
            var readback = await grabFrame().ConfigureAwait(false);

            writer ??= Task.Run(writeFramesAsync);

            long queueStart = Stopwatch.GetTimestamp();

            try
            {
                await queue.Writer.WriteAsync(readback.Pixels).ConfigureAwait(false);
                RenderTimings.QueueFull.AddSince(queueStart);
            }
            catch (ChannelClosedException)
            {
                // The writer stopped. Its exception says why.
                await writer.ConfigureAwait(false);
                throw;
            }
        }

        public async Task CompleteAsync()
        {
            if (writer == null)
                throw new InvalidOperationException("No frames were rendered.");

            queue.Writer.TryComplete();
            await writer.ConfigureAwait(false);

            await input!.FlushAsync().ConfigureAwait(false);
            await input.DisposeAsync().ConfigureAwait(false);
            input = null;

            await ffmpeg!.WaitForExitAsync().ConfigureAwait(false);

            if (ffmpeg.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg failed with code {ffmpeg.ExitCode}:\n{tail()}");
        }

        private async Task writeFramesAsync()
        {
            try
            {
                await foreach (var pending in queue.Reader.ReadAllAsync().ConfigureAwait(false))
                {
                    long waitStart = Stopwatch.GetTimestamp();

                    using (var frame = await pending.ConfigureAwait(false))
                    {
                        RenderTimings.WaitPixels.AddSince(waitStart);

                        if (ffmpeg == null)
                            start(frame.Width, frame.Height, frame.Format);
                        else if (frame.Width != frameWidth || frame.Height != frameHeight)
                            throw new InvalidOperationException($"The window changed size during the render ({frameWidth}x{frameHeight} to {frame.Width}x{frame.Height}).");

                        long writeStart = Stopwatch.GetTimestamp();

                        try
                        {
                            await input!.WriteAsync(frame.Pixels.AsMemory(0, frame.Length)).ConfigureAwait(false);
                            RenderTimings.Ffmpeg.AddSince(writeStart);
                        }
                        catch (IOException e)
                        {
                            throw new InvalidOperationException($"ffmpeg stopped accepting frames. ffmpeg said:\n{tail()}", e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                queue.Writer.TryComplete(e);
                throw;
            }
        }

        private void start(int width, int height, RenderPixelFormat format)
        {
            frameWidth = width;
            frameHeight = height;

            string? folder = Path.GetDirectoryName(Path.GetFullPath(job.Output));
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            var startInfo = new ProcessStartInfo(job.Ffmpeg)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            foreach (string arg in BuildArguments(job, width, height, PixelFormatName(format)))
                startInfo.ArgumentList.Add(arg);

            Logger.Log($"Starting ffmpeg: {job.Ffmpeg} {string.Join(' ', startInfo.ArgumentList)}");

            try
            {
                ffmpeg = Process.Start(startInfo) ?? throw new InvalidOperationException("ffmpeg didn't start.");
                RenderPriority.Lower(ffmpeg);
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new InvalidOperationException($"Couldn't start ffmpeg at \"{job.Ffmpeg}\". Is it installed?", e);
            }

            ffmpeg.ErrorDataReceived += (_, e) => appendLog(e.Data);
            ffmpeg.OutputDataReceived += (_, e) => appendLog(e.Data);
            ffmpeg.BeginErrorReadLine();
            ffmpeg.BeginOutputReadLine();

            input = ffmpeg.StandardInput.BaseStream;
        }

        /// <summary>
        /// The ffmpeg command line: raw frames of <paramref name="frameWidth"/>x<paramref name="frameHeight"/> on stdin,
        /// scaled to the job's size and encoded to the job's output file.
        /// </summary>
        /// <param name="job">The render job.</param>
        /// <param name="frameWidth">Width of the frames sent to ffmpeg.</param>
        /// <param name="frameHeight">Height of the frames sent to ffmpeg.</param>
        /// <param name="pixelFormat">The frames' ffmpeg pixel format, see <see cref="PixelFormatName"/>.</param>
        public static IReadOnlyList<string> BuildArguments(RenderJob job, int frameWidth, int frameHeight, string pixelFormat = "rgba")
        {
            var args = new List<string>
            {
                "-y",
                "-hide_banner",
                "-loglevel", "warning",
                "-f", "rawvideo",
                "-pix_fmt", pixelFormat,
                "-s", $"{frameWidth}x{frameHeight}",
                "-r", job.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
                "-i", "-",
            };

            if (frameWidth != job.Width || frameHeight != job.Height)
                args.AddRange(new[] { "-vf", $"scale={job.Width}:{job.Height}:flags=lanczos" });

            args.AddRange(new[] { "-c:v", job.Encoder });

            string quality = job.Quality.ToString(CultureInfo.InvariantCulture);

            if (job.Encoder.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
                args.AddRange(new[] { "-rc", "vbr", "-cq", quality, "-b:v", "0", "-preset", job.Preset ?? "p5" });
            else
                args.AddRange(new[] { "-crf", quality, "-preset", job.Preset ?? "faster" });

            if (pixelFormat == "nv12")
            {
                // Hardware encoders take NV12 as it is. Frames converted by Nv12Converter are BT.709, so say so.
                bool hardware = job.Encoder.Contains("nvenc", StringComparison.OrdinalIgnoreCase)
                                || job.Encoder.Contains("qsv", StringComparison.OrdinalIgnoreCase)
                                || job.Encoder.Contains("amf", StringComparison.OrdinalIgnoreCase);

                args.AddRange(new[] { "-pix_fmt", hardware ? "nv12" : "yuv420p" });
                args.AddRange(new[] { "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv" });
            }
            else
                args.AddRange(new[] { "-pix_fmt", "yuv420p" });

            args.AddRange(new[] { "-movflags", "+faststart", job.Output });

            return args;
        }

        /// <summary>
        /// ffmpeg's name for <paramref name="format"/>.
        /// </summary>
        public static string PixelFormatName(RenderPixelFormat format)
        {
            switch (format)
            {
                case RenderPixelFormat.Bgra:
                    return "bgra";

                case RenderPixelFormat.Nv12:
                    return "nv12";

                default:
                    return "rgba";
            }
        }

        private void appendLog(string? line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            lock (ffmpegLog)
                ffmpegLog.AppendLine(line);

            Logger.Log($"ffmpeg: {line}");
        }

        private string tail()
        {
            lock (ffmpegLog)
            {
                string text = ffmpegLog.ToString();
                return text.Length > 2000 ? text[^2000..] : text;
            }
        }

        public void Dispose()
        {
            queue.Writer.TryComplete();
            input?.Dispose();

            if (ffmpeg != null)
            {
                if (!ffmpeg.HasExited)
                {
                    try
                    {
                        ffmpeg.Kill(true);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                ffmpeg.Dispose();
            }
        }
    }
}
