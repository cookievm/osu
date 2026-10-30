// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Configuration;

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
        [TestCase("""{ "output": "a.mp4", "autoplay": true }""")]
        [TestCase("""{ "replay": "a.osr" }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "width": 1921 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "framesPerSecond": 0 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "quality": 60 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "durationSeconds": -1 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "colourConversion": "magic" }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "motionBlurFrames": 0 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "backgroundDim": 101 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "backgroundBlur": -1 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "cursorSize": 3 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "motionBlurFrames": 17 }""")]
        [TestCase("""{ "replay": "a.osr", "output": "a.mp4", "framesPerSecond": 240, "motionBlurFrames": 8 }""")]
        [TestCase("""{ "beatmap": "a.osz", "autoplay": true, "output": "preview", "hudPreview": true }""")]
        [TestCase("null")]
        public void TestRejectsBadJobs(string json)
        {
            Assert.Throws<InvalidDataException>(() => RenderJob.Parse(json));
        }

        [Test]
        public void TestHudPreviewHasNoAudio()
        {
            var job = RenderJob.Parse("""{ "beatmap": "a.osz", "autoplay": true, "output": "C:\\preview", "hudPreview": true, "durationSeconds": 4 }""");

            Assert.That(job.HudPreview, Is.True);
            Assert.That(job.ToRenderSettings().RecordAudio, Is.False);
            Assert.That(job.ToRenderSettings().FrameCount, Is.Not.Null);
        }

        [Test]
        public void TestPlayerGlobalRankReachesTheGame()
        {
            var job = RenderJob.Parse("""{ "replay": "a.osr", "output": "a.mp4", "playerGlobalRank": 12345 }""");

            Assert.That(job.ToRenderSettings().PlayerGlobalRank, Is.EqualTo(12345));

            var withLeaderboard = RenderJob.Parse("""{ "replay": "a.osr", "output": "a.mp4", "leaderboard": [ { "username": "peppy", "userId": 2, "totalScore": 900000, "pp": 727.5, "accuracy": 0.99, "maxCombo": 1234 } ] }""");
            var withMods = RenderJob.Parse("""{ "replay": "a.osr", "output": "a.mp4", "leaderboard": [ { "username": "peppy", "mods": [ "HD", "DT" ] } ] }""");
            Assert.That(withMods.ToRenderSettings().Leaderboard.Single().Mods, Is.EqualTo(new[] { "HD", "DT" }));
            Assert.That(withLeaderboard.ToRenderSettings().Leaderboard.Single(), Is.EqualTo(new RenderLeaderboardEntry { Username = "peppy", UserId = 2, TotalScore = 900000, Pp = 727.5, Accuracy = 0.99, MaxCombo = 1234 }));
            Assert.That(RenderJob.Parse("""{ "replay": "a.osr", "output": "a.mp4" }""").ToRenderSettings().PlayerGlobalRank, Is.Null);
        }

        [Test]
        public void TestColourConversionDefaultsToTheGraphicsCard()
        {
            var job = RenderJob.Parse("""{ "replay": "a.osr", "output": "a.mp4" }""");
            Assert.That(job.ColourConversion, Is.EqualTo("gpu"));
            Assert.That(job.ToRenderSettings().GpuColourConversion, Is.True);

            var cpu = RenderJob.Parse("""{ "replay": "a.osr", "output": "a.mp4", "colourConversion": "cpu" }""");
            Assert.That(cpu.ToRenderSettings().GpuColourConversion, Is.False);
        }

        [Test]
        public void TestMotionBlurDrawsMoreFramesThanTheVideoHas()
        {
            var job = RenderJob.Parse("""{ "replay": "a.osr", "output": "a.mp4", "framesPerSecond": 60, "motionBlurFrames": 8, "durationSeconds": 2 }""");
            var settings = job.ToRenderSettings();

            Assert.That(settings.FramesPerSecond, Is.EqualTo(480));
            Assert.That(settings.BlendFrames, Is.EqualTo(8));
            Assert.That(settings.FrameCount, Is.EqualTo(960));

            // The video itself stays at 60 fps.
            Assert.That(string.Join(' ', FfmpegPipeCapturer.BuildArguments(job, 1920, 1080)), Does.Contain("-r 60 "));
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

        [Test]
        public void TestDisplayDefaultsMatchLazer()
        {
            var display = RenderJob.Parse("""{ "replay": "a.osr", "output": "a.mp4" }""").ToRenderSettings().Display;

            Assert.That(display, Is.EqualTo(new RenderDisplay()));
        }

        [Test]
        public void TestDisplaySettingsReachTheGame()
        {
            var job = RenderJob.Parse("""
                                      {
                                        "replay": "a.osr", "output": "a.mp4",
                                        "backgroundDim": 90, "backgroundBlur": 25, "storyboard": false, "showInterface": false,
                                        "hitLighting": false, "cursorTrail": false, "cursorSize": 1.5,
                                        "beatmapHitsounds": false, "beatmapColours": false
                                      }
                                      """);

            using var storage = new TemporaryNativeStorage("oily-render-display-test");
            var config = new OsuConfigManager(storage);
            var osuConfig = new OsuRulesetConfigManager(null, new OsuRuleset().RulesetInfo);

            job.ToRenderSettings().Display!.Apply(config, osuConfig);

            Assert.That(config.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.9).Within(0.001));
            Assert.That(config.Get<double>(OsuSetting.BlurLevel), Is.EqualTo(0.25).Within(0.001));
            Assert.That(config.Get<bool>(OsuSetting.ShowStoryboard), Is.False);
            Assert.That(config.Get<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode), Is.EqualTo(HUDVisibilityMode.Never));
            Assert.That(config.Get<bool>(OsuSetting.HitLighting), Is.False);
            Assert.That(config.Get<float>(OsuSetting.GameplayCursorSize), Is.EqualTo(1.5f).Within(0.001));
            Assert.That(config.Get<bool>(OsuSetting.BeatmapHitsounds), Is.False);
            Assert.That(config.Get<bool>(OsuSetting.BeatmapColours), Is.False);
            Assert.That(osuConfig.Get<bool>(OsuRulesetSetting.ShowCursorTrail), Is.False);
        }

        [Test]
        public void TestAutoplayNeedsNoReplay()
        {
            var job = RenderJob.Parse("""{ "autoplay": true, "beatmap": "C:\\maps\\set.osz", "beatmapId": 42, "mods": "HD DT", "output": "a.mp4" }""");

            Assert.That(job.Autoplay, Is.True);
            Assert.That(job.Replay, Is.Empty);
            Assert.That(job.ModAcronyms, Is.EqualTo(new[] { "HD", "DT" }));
        }

        [Test]
        public void TestPicksTheDifficultyToAutoplay()
        {
            var easy = new BeatmapInfo { OnlineID = 1, DifficultyName = "Easy" };
            var insane = new BeatmapInfo { OnlineID = 2, DifficultyName = "Insane" };
            var unsubmitted = new BeatmapInfo { OnlineID = -1, DifficultyName = "My Extra" };
            var set = new[] { easy, insane, unsubmitted };

            Assert.That(job(42, null).PickDifficulty(new[] { easy }), Is.SameAs(easy), "the only difficulty");
            Assert.That(job(2, null).PickDifficulty(set), Is.SameAs(insane));
            Assert.That(job(null, "my extra").PickDifficulty(set), Is.SameAs(unsubmitted));
            Assert.That(job(99, "Easy").PickDifficulty(set), Is.SameAs(easy), "falls back to the name");
            Assert.That(job(null, null).PickDifficulty(set), Is.Null);

            static RenderJob job(int? id, string? name) => new RenderJob { Autoplay = true, Beatmap = "s.osz", Output = "a.mp4", BeatmapId = id, Difficulty = name };
        }
    }
}
