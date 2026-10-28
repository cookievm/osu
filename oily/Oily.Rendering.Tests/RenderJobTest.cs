// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Oily.Rendering.Tests
{
    [TestFixture]
    public class RenderJobTest
    {
        [Test]
        public void TestParsesCompanionJob()
        {
            var job = RenderJob.Parse("""
                                      {
                                        // written by the companion app
                                        "replay": "C:\\r\\play.osr",
                                        "beatmap": "C:\\r\\set.osz",
                                        "skin": "C:\\r\\my skin.osk",
                                        "output": "C:\\Videos\\play.mp4",
                                        "width": 2560, "height": 1440, "framesPerSecond": 120,
                                        "encoder": "h264_nvenc", "quality": 20,
                                        "ffmpeg": "C:\\ffmpeg\\ffmpeg.exe",
                                      }
                                      """);

            Assert.That(job.Replay, Is.EqualTo(@"C:\r\play.osr"));
            Assert.That(job.Skin, Is.EqualTo(@"C:\r\my skin.osk"));
            Assert.That(job.Width, Is.EqualTo(2560));
            Assert.That(job.FramesPerSecond, Is.EqualTo(120));
            Assert.That(job.Encoder, Is.EqualTo("h264_nvenc"));

            var settings = job.ToRenderSettings();
            Assert.That(settings.FramesPerSecond, Is.EqualTo(120));
            Assert.That(settings.Duration, Is.Null);
        }

        [Test]
        public void TestDefaultsAndDuration()
        {
            var job = RenderJob.Parse("""{ "replay": "a.osr", "output": "a.mp4", "durationSeconds": 12.5 }""");

            Assert.That(job.Width, Is.EqualTo(1920));
            Assert.That(job.Height, Is.EqualTo(1080));
            Assert.That(job.Encoder, Is.EqualTo("libx264"));
            Assert.That(job.Ffmpeg, Is.EqualTo("ffmpeg"));
            Assert.That(job.ToRenderSettings().Duration, Is.EqualTo(TimeSpan.FromSeconds(12.5)));
        }

        [TestCase("""{ "output": "a.mp4" }""")]
        [TestCase("""{ "replay": "a.osr" }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "width": 1921 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "framesPerSecond": 0 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "quality": 60 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "durationSeconds": -1 }""")]
        [TestCase("null")]
        public void TestRejectsBadJobs(string json)
        {
            Assert.Throws<InvalidDataException>(() => RenderJob.Parse(json));
        }

        [Test]
        public void TestRoundTrips()
        {
            var job = new RenderJob { Replay = "a.osr", Output = "a.mp4", Skin = "s.osk", FramesPerSecond = 30 };
            Assert.That(RenderJob.Parse(job.ToJson()), Is.EqualTo(job));
        }

        [Test]
        public void TestFfmpegArgumentsScaleWindowToVideoSize()
        {
            var job = new RenderJob { Replay = "a.osr", Output = "out.mp4", Width = 1920, Height = 1080, FramesPerSecond = 60, Quality = 18 };

            var args = FfmpegPipeCapturer.BuildArguments(job, 2880, 1620);

            Assert.That(string.Join(' ', args), Does.Contain("-f rawvideo -pix_fmt rgba -s 2880x1620 -r 60 -i -"));
            Assert.That(string.Join(' ', args), Does.Contain("-vf scale=1920:1080:flags=lanczos"));
            Assert.That(string.Join(' ', args), Does.Contain("-c:v libx264 -crf 18"));
            Assert.That(args.Last(), Is.EqualTo("out.mp4"));
        }

        [Test]
        public void TestFfmpegArgumentsSkipScaleAndUseNvenc()
        {
            var job = new RenderJob { Replay = "a.osr", Output = "out.mp4", Encoder = "h264_nvenc", Quality = 22 };

            var args = FfmpegPipeCapturer.BuildArguments(job, 1920, 1080);

            Assert.That(args, Does.Not.Contain("-vf"));
            Assert.That(string.Join(' ', args), Does.Contain("-c:v h264_nvenc -rc vbr -cq 22"));
        }

        [Test]
        public void TestReporterLines()
        {
            var output = new StringWriter();
            var reporter = new RenderJobReporter(output);

            reporter.Status("Importing\nthe beatmap");
            reporter.Progress(0.426);
            reporter.Progress(2);
            reporter.Done(@"C:\Videos\a.mp4");
            reporter.Error("ffmpeg failed");

            Assert.That(output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new[]
            {
                "OILY-RENDER STATUS Importing the beatmap",
                "OILY-RENDER PROGRESS 43",
                "OILY-RENDER PROGRESS 100",
                @"OILY-RENDER DONE C:\Videos\a.mp4",
                "OILY-RENDER ERROR ffmpeg failed",
            }));
        }
    
        [TestCase(1280, 720, 2560, 1440, 1280, 720)]
        [TestCase(2560, 1440, 2560, 1440, 2304, 1296)]
        [TestCase(3840, 2160, 1920, 1080, 1728, 972)]
        [TestCase(1920, 1080, 0, 0, 1920, 1080)]
        public void TestWindowFitsOnTheDisplayWithTheVideosShape(int width, int height, int displayWidth, int displayHeight, int expectedWidth, int expectedHeight)
        {
            var job = RenderJob.Parse($$"""{ "replay": "r.osr", "output": "o.mp4", "width": {{width}}, "height": {{height}} }""");
            var window = job.WindowSizeFor(new System.Drawing.Size(displayWidth, displayHeight));

            Assert.That(window, Is.EqualTo(new System.Drawing.Size(expectedWidth, expectedHeight)));
            Assert.That(job.ToRenderSettings().CaptureSize, Is.EqualTo(new System.Drawing.Size(width, height)));
            Assert.That(job.Hidden, Is.True);
        }
}
}
