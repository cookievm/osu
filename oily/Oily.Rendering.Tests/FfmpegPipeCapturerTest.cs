// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Oily.Rendering.Tests
{
    [TestFixture]
    public class FfmpegPipeCapturerTest
    {
        private const int width = 64;
        private const int height = 36;

        private string folder = null!;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "oily-capturer-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(folder, true);

        [Test]
        public void TestFrameFromImageCopiesPixels()
        {
            using var image = new Image<Rgba32>(2, 1);
            image[0, 0] = new Rgba32(1, 2, 3, 4);
            image[1, 0] = new Rgba32(5, 6, 7, 8);

            var frame = RenderFrame.FromImage(image);

            Assert.That(frame.Format, Is.EqualTo(RenderPixelFormat.Rgba));
            Assert.That(frame.Pixels.Take(frame.Length), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

            frame.Dispose();
            Assert.DoesNotThrow(frame.Dispose);
        }

        [Test]
        public void TestBgraFramesAreDescribedToFfmpeg()
        {
            var job = new RenderJob { Replay = "a.osr", Output = "out.mp4" };

            var args = FfmpegPipeCapturer.BuildArguments(job, 1920, 1080, "bgra");

            Assert.That(string.Join(' ', args), Does.Contain("-f rawvideo -pix_fmt bgra -s 1920x1080"));
        }

        [Test]
        public void TestNv12FramesGoStraightToHardwareEncoders()
        {
            string nvenc = string.Join(' ', FfmpegPipeCapturer.BuildArguments(new RenderJob { Replay = "a.osr", Output = "out.mp4", Encoder = "h264_nvenc" }, 3840, 2160, "nv12"));
            string x264 = string.Join(' ', FfmpegPipeCapturer.BuildArguments(new RenderJob { Replay = "a.osr", Output = "out.mp4" }, 3840, 2160, "nv12"));

            Assert.That(nvenc, Does.Contain("-f rawvideo -pix_fmt nv12 -s 3840x2160"));
            Assert.That(nvenc, Does.Contain("-pix_fmt nv12 -colorspace bt709"));
            Assert.That(x264, Does.Contain("-pix_fmt yuv420p -colorspace bt709"));
            Assert.That(x264, Does.Contain("-color_range tv"));
        }

        [Test]
        public void TestCompletingWithoutFramesFails()
        {
            using var capturer = new FfmpegPipeCapturer(() => throw new InvalidOperationException("never called"), job("ffmpeg"));

            Assert.ThrowsAsync<InvalidOperationException>(capturer.CompleteAsync);
        }

        [Test]
        public void TestMissingFfmpegIsReported()
        {
            var readbacks = new Queue<FrameReadback>(Enumerable.Range(0, 8).Select(i => new FrameReadback(Task.FromResult(grey(i)))));

            using var capturer = new FfmpegPipeCapturer(() => Task.FromResult(readbacks.Dequeue()), job(Path.Combine(folder, "no-ffmpeg-here")));

            var error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                for (int i = 0; i < 8; i++)
                    await capturer.CaptureFrameAsync(new RenderedFrame(i, i * 100, i * 100)).ConfigureAwait(false);

                await capturer.CompleteAsync().ConfigureAwait(false);
            });

            Assert.That(error!.Message, Does.Contain("Couldn't start ffmpeg"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TestFramesAreWrittenInOrderWhenTheyComeBackOutOfOrder(bool nv12)
        {
            if (!ffmpegAvailable())
                Assert.Ignore("ffmpeg isn't installed.");

            const int count = 10;

            var job = this.job("ffmpeg");
            var sources = Enumerable.Range(0, count).Select(_ => new TaskCompletionSource<RenderFrame>()).ToArray();
            int next = 0;

            using (var capturer = new FfmpegPipeCapturer(() => Task.FromResult(new FrameReadback(sources[next++].Task)), job))
            {
                // Capturing doesn't wait for the pixels, so the game can draw on while earlier frames come back.
                Assert.That(capturer.CaptureFrameAsync(new RenderedFrame(0, 0, 0)).IsCompleted, Is.True);

                var captures = Task.Run(async () =>
                {
                    for (int i = 1; i < count; i++)
                        await capturer.CaptureFrameAsync(new RenderedFrame(i, i * 100, i * 100)).ConfigureAwait(false);
                });

                // The pixels arrive last frame first.
                for (int i = count - 1; i >= 0; i--)
                    sources[i].SetResult(nv12 ? greyNv12(i) : grey(i));

                await captures.ConfigureAwait(false);
                await capturer.CompleteAsync().ConfigureAwait(false);
            }

            byte[] decoded = await decodeToGrey(job.Output).ConfigureAwait(false);
            Assert.That(decoded.Length, Is.EqualTo(count * width * height));

            double[] brightness = Enumerable.Range(0, count).Select(i => decoded.Skip(i * width * height).Take(width * height).Average(b => (double)b)).ToArray();

            for (int i = 0; i < count; i++)
                Assert.That(brightness[i], Is.EqualTo(greyLevel(i)).Within(6), $"frame {i}");
        }

        private RenderJob job(string ffmpeg) => new RenderJob
        {
            Replay = "a.osr",
            Output = Path.Combine(folder, "out.mp4"),
            Width = width,
            Height = height,
            FramesPerSecond = 10,
            Ffmpeg = ffmpeg,
        };

        private static int greyLevel(int index) => 20 + index * 20;

        private static RenderFrame grey(int index)
        {
            var frame = new RenderFrame(width, height, index % 2 == 1 ? RenderPixelFormat.Bgra : RenderPixelFormat.Rgba);
            frame.Pixels.AsSpan(0, frame.Length).Fill((byte)greyLevel(index));

            // Alpha.
            for (int i = 3; i < frame.Length; i += 4)
                frame.Pixels[i] = 255;

            return frame;
        }

        private static RenderFrame greyNv12(int index)
        {
            using (var rgba = grey(index))
            {
                var frame = new RenderFrame(width, height, RenderPixelFormat.Nv12);
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(rgba.Pixels, System.Runtime.InteropServices.GCHandleType.Pinned);

                try
                {
                    Nv12Converter.Convert(handle.AddrOfPinnedObject(), width * 4, width, height, rgba.Format == RenderPixelFormat.Bgra, frame.Pixels);
                }
                finally
                {
                    handle.Free();
                }

                return frame;
            }
        }

        private static bool ffmpegAvailable()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("ffmpeg", "-version") { RedirectStandardOutput = true, UseShellExecute = false });
                process!.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static async Task<byte[]> decodeToGrey(string video)
        {
            var info = new ProcessStartInfo("ffmpeg") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };

            foreach (string arg in new[] { "-v", "error", "-i", video, "-f", "rawvideo", "-pix_fmt", "gray", "-" })
                info.ArgumentList.Add(arg);

            using var process = Process.Start(info)!;
            using var output = new MemoryStream();

            var errors = process.StandardError.ReadToEndAsync();
            await process.StandardOutput.BaseStream.CopyToAsync(output).ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            Assert.That(process.ExitCode, Is.Zero, await errors.ConfigureAwait(false));
            return output.ToArray();
        }
    }
}
