// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace Oily.Rendering.Tests
{
    /// <summary>
    /// Renders an autoplay replay with a capturer that only records frame timings, and checks the timings are exact and repeatable.
    /// </summary>
    public partial class TestSceneReplayRender : ScreenTestScene
    {
        private ReplayRenderScreen renderScreen = null!;
        private RecordingCapturer capturer = null!;

        [TestCase(60)]
        [TestCase(240)]
        public void TestFrameTimesAreExact(int fps)
        {
            render(new RenderSettings { FramesPerSecond = fps, Duration = TimeSpan.FromSeconds(1) });

            AddAssert("all frames captured", () => capturer.Frames, () => Has.Count.EqualTo(fps));
            AddAssert("first frame at start time", () => capturer.Frames[0].GameplayTime, () => Is.EqualTo(renderScreen.Player.GameplayClockContainer!.StartTime));
            AddAssert("video time is frame / fps", () => capturer.Frames.Select(f => f.VideoTime), () => Is.EqualTo(Enumerable.Range(0, fps).Select(i => i * 1000.0 / fps)));
            assertGameplayAdvancesBy(fps, 1);
        }

        [Test]
        public void TestDoubleTimeAdvancesFaster()
        {
            render(new RenderSettings { FramesPerSecond = 60, Duration = TimeSpan.FromSeconds(3) }, new OsuModDoubleTime());

            AddAssert("all frames captured", () => capturer.Frames, () => Has.Count.EqualTo(180));
            assertGameplayAdvancesBy(60, 1.5);
            AddAssert("replay hit objects", () => renderScreen.Player.GameplayState.ScoreProcessor.TotalScore.Value, () => Is.GreaterThan(0));
        }

        [Test]
        public void TestEscapeCancelsRender()
        {
            startRender(new RenderSettings { FramesPerSecond = 60, Duration = null });

            AddUntilStep("wait for frames", () => renderScreen.FramesCaptured, () => Is.GreaterThan(10));
            AddStep("press escape", () => InputManager.Key(Key.Escape));
            AddUntilStep("render screen exited", () => !renderScreen.IsCurrentScreen() && Stack.CurrentScreen is not ReplayRenderScreen);
            AddAssert("not completed", () => renderScreen.Completed, () => Is.False);
        }

        [Test]
        public void TestRendersAreRepeatable()
        {
            List<RenderedFrame> firstRender = null!;

            render(new RenderSettings { FramesPerSecond = 120, Duration = TimeSpan.FromSeconds(1) });
            AddStep("store first render", () => firstRender = capturer.Frames.ToList());

            render(new RenderSettings { FramesPerSecond = 120, Duration = TimeSpan.FromSeconds(1) });
            AddAssert("second render matches", () => capturer.Frames, () => Is.EqualTo(firstRender));
        }

        private void render(RenderSettings settings, params Mod[] extraMods)
        {
            startRender(settings, extraMods);
            AddUntilStep("wait for render to finish", () => renderScreen.Completed);
        }

        private void startRender(RenderSettings settings, params Mod[] extraMods)
        {
            AddStep("exit previous render", () =>
            {
                if (Stack.CurrentScreen is ReplayRenderScreen previous)
                    previous.Exit();
            });

            AddStep("start render", () =>
            {
                var ruleset = new OsuRuleset();
                var autoplay = ruleset.GetAutoplayMod()!;
                var mods = extraMods.Prepend(autoplay).ToArray();
                var working = CreateWorkingBeatmap(ruleset.RulesetInfo);

                Beatmap.Value = working;
                Ruleset.Value = ruleset.RulesetInfo;
                SelectedMods.Value = Array.Empty<Mod>();

                var score = autoplay.CreateScoreFromReplayData(working.GetPlayableBeatmap(ruleset.RulesetInfo, mods), mods);
                score.ScoreInfo.Ruleset = ruleset.RulesetInfo;
                score.ScoreInfo.BeatmapInfo = working.BeatmapInfo;
                score.ScoreInfo.Mods = mods;

                LoadScreen(renderScreen = new ReplayRenderScreen(score, settings, (_, _) => capturer = new RecordingCapturer()));
            });
        }

        private void assertGameplayAdvancesBy(int fps, double rate)
        {
            AddAssert("gameplay advances exactly one frame at a time", () =>
            {
                double start = capturer.Frames[0].GameplayTime;

                return capturer.Frames.Select((f, i) => Math.Abs(f.GameplayTime - (start + i * 1000.0 / fps * rate))).Max();
            }, () => Is.LessThan(1e-6));
        }

        private class RecordingCapturer : IFrameCapturer
        {
            public readonly List<RenderedFrame> Frames = new List<RenderedFrame>();

            public string OutputDescription => "memory";

            public Task CaptureFrameAsync(RenderedFrame frame)
            {
                Frames.Add(frame);
                return Task.CompletedTask;
            }

            public Task CompleteAsync() => Task.CompletedTask;

            public void Dispose()
            {
            }
        }
    }
}
