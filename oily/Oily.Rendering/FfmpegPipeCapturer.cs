// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Oily.Rendering
{
    /// <summary>
    /// Sends every frame straight to ffmpeg as raw pixels, which encodes the video as it goes.
    /// No PNGs are written.
    /// </summary>
    /// <remarks>
    /// Frames still come from <see cref="GameHost.TakeScreenshotAsync"/>, so they are the window's size.
    /// ffmpeg scales them to the job's size.
    /// </remarks>
    public class FfmpegPipeCapturer : IFrameCapturer
    {
        private readonly GameHost host;
        private readonly RenderJob job;

        private Process? ffmpeg;
        private Stream? input;
        private int frameWidth;
        private int frameHeight;
        private readonly StringBuilder ffmpegLog = new StringBuilder();

        public string OutputDescription => job.Output;

        public FfmpegPipeCapturer(GameHost host, RenderJob job)
        {
            this.host = host;
            this.job = job;
        }

        public async Task CaptureFrameAsync(RenderedFrame frame)
        {
            using (var image = await host.TakeScreenshotAsync().ConfigureAwait(false))
            {
                if (ffmpeg == null)
                    start(image.Width, image.Height);
                else if (image.Width != frameWidth || image.Height != frameHeight)
                    throw new InvalidOperationException($"The window changed size during the render ({frameWidth}x{frameHeight} to {image.Width}x{image.Height}).");

                int length = image.Width * image.Height * 4;
                byte[] buffer = ArrayPool<byte>.Shared.Rent(length);

                try
                {
                    image.CopyPixelDataTo(buffer.AsSpan(0, length));
                    await input!.WriteAsync(buffer.AsMemory(0, length)).ConfigureAwait(false);
                }
                catch (IOException e)
                {
                    throw new InvalidOperationException($"ffmpeg stopped accepting frames. ffmpeg said:\n{tail()}", e);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        public void Complete()
        {
            if (ffmpeg == null)
                throw new InvalidOperationException("No frames were rendered.");

            input!.Flush();
            input.Dispose();
            input = null;

            ffmpeg.WaitForExit();

            if (ffmpeg.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg failed with code {ffmpeg.ExitCode}:\n{tail()}");
        }

        private void start(int width, int height)
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

            foreach (string arg in BuildArguments(job, width, height))
                startInfo.ArgumentList.Add(arg);

            Logger.Log($"Starting ffmpeg: {job.Ffmpeg} {string.Join(' ', startInfo.ArgumentList)}");

            try
            {
                ffmpeg = Process.Start(startInfo) ?? throw new InvalidOperationException("ffmpeg didn't start.");
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
        /// The ffmpeg command line: raw RGBA frames of <paramref name="frameWidth"/>x<paramref name="frameHeight"/> on stdin,
        /// scaled to the job's size and encoded to the job's output file.
        /// </summary>
        public static IReadOnlyList<string> BuildArguments(RenderJob job, int frameWidth, int frameHeight)
        {
            var args = new List<string>
            {
                "-y",
                "-hide_banner",
                "-loglevel", "warning",
                "-f", "rawvideo",
                "-pix_fmt", "rgba",
                "-s", $"{frameWidth}x{frameHeight}",
                "-r", job.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
                "-i", "-",
            };

            if (frameWidth != job.Width || frameHeight != job.Height)
                args.AddRange(new[] { "-vf", $"scale={job.Width}:{job.Height}:flags=lanczos" });

            args.AddRange(new[] { "-c:v", job.Encoder });

            string quality = job.Quality.ToString(CultureInfo.InvariantCulture);

            if (job.Encoder.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
                args.AddRange(new[] { "-rc", "vbr", "-cq", quality, "-b:v", "0", "-preset", "p5" });
            else
                args.AddRange(new[] { "-crf", quality, "-preset", "faster" });

            args.AddRange(new[] { "-pix_fmt", "yuv420p", "-movflags", "+faststart", job.Output });

            return args;
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
