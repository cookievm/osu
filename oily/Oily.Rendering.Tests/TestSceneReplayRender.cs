// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning.Components;
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

        [Test]
        public void TestMeasuresHudPieces()
        {
            render(new RenderSettings { FramesPerSecond = 30, Duration = TimeSpan.FromSeconds(2) });

            AddAssert("HUD pieces measured", () =>
            {
                var hud = HudPreviewCapturer.Measure(renderScreen.Player.Hud);
                return hud.Pieces.Count(p => p.Visible && p.Group == "global");
            }, () => Is.GreaterThan(2));

            AddAssert("origins sit inside or on their pieces", () => HudPreviewCapturer.Measure(renderScreen.Player.Hud).Pieces.Where(p => p.Visible).All(p =>
                p.OriginX >= p.Left - 1 && p.OriginX <= p.Left + p.Width + 1 && p.OriginY >= p.Top - 1 && p.OriginY <= p.Top + p.Height + 1));

            AddStep("hide gameplay", () => renderScreen.Player.HideGameplay());
            AddAssert("HUD still measured", () => HudPreviewCapturer.Measure(renderScreen.Player.Hud).Pieces.Count(p => p.Visible), () => Is.GreaterThan(2));
        }

        [Test]
        public void TestOilyHudPieces()
        {
            ArgonIfFcPerformancePointsCounter ifFc = null!;
            GlobalRankDisplay rank = null!;

            startRender(new RenderSettings { FramesPerSecond = 30, Duration = TimeSpan.FromSeconds(4), PlayerGlobalRank = 12345 });
            AddUntilStep("wait for gameplay", () => renderScreen.Player?.Hud?.IsLoaded == true);

            AddStep("add pieces", () => renderScreen.Player.Hud.AddRange(new Drawable[]
            {
                ifFc = new ArgonIfFcPerformancePointsCounter(),
                rank = new GlobalRankDisplay(),
            }));

            AddUntilStep("pp if FC counts", () => ifFc.Current.Value, () => Is.GreaterThan(0));
            AddAssert("rank shows", () => rank.ChildrenOfType<OsuSpriteText>().Single().Text.ToString(), () => Is.EqualTo("#12,345"));
            AddUntilStep("wait for render to finish", () => renderScreen.Completed);
            AddAssert("rank cleared after the render", () => PlayerGlobalRank.Current, () => Is.Null);
        }

        [Test]
        public void TestLeaderboardSortsByScoreOrPp()
        {
            RenderLeaderboardProvider provider = null!;

            startRender(new RenderSettings
            {
                FramesPerSecond = 30,
                Duration = TimeSpan.FromSeconds(4),
                Leaderboard = new[]
                {
                    new RenderLeaderboardEntry { Username = "high score", UserId = 2, TotalScore = 2_000_000, Pp = 1, Accuracy = 0.9, MaxCombo = 10 },
                    new RenderLeaderboardEntry { Username = "high pp", UserId = 3, TotalScore = 1, Pp = 5000, Accuracy = 1, MaxCombo = 10 },
                },
            });

            AddUntilStep("wait for leaderboard", () => (provider = renderScreen.Player?.ChildrenOfType<RenderLeaderboardProvider>().SingleOrDefault()!) != null && provider.IsLoaded);
            AddAssert("player is on it", () => provider.Scores.Count, () => Is.EqualTo(3));
            AddUntilStep("player has pp", () => provider.Scores.Single(s => s.Tracked).PerformancePoints.Value, () => Is.GreaterThan(0));

            AddStep("sort by score", () => provider.Sort());
            AddAssert("high score first", () => provider.Scores.Single(s => s.DisplayOrder.Value == 1).User.Username, () => Is.EqualTo("high score"));

            AddStep("sort by pp", () => provider.SortBy.Value = GameplayLeaderboardSort.PerformancePoints);
            AddAssert("high pp first", () => provider.Scores.Single(s => s.DisplayOrder.Value == 1).User.Username, () => Is.EqualTo("high pp"));
            AddAssert("pp shown", () => provider.Scores.All(s => s.ShowPerformancePoints.Value));
        }

        [Test]
        public void TestNoFixedModIcons()
        {
            startRender(new RenderSettings { FramesPerSecond = 30, Duration = TimeSpan.FromSeconds(2) });
            AddUntilStep("wait for gameplay", () => renderScreen.Player?.Hud?.IsLoaded == true);
            AddAssert("top right mod icons gone", () => renderScreen.Player.Hud.TopRightElements.Contains(renderScreen.Player.Hud.ModDisplay), () => Is.False);
        }

        [Test]
        public void TestLeaderboardColours()
        {
            DrawableGameplayLeaderboard leaderboard = null!;

            startRender(new RenderSettings
            {
                FramesPerSecond = 30,
                Duration = TimeSpan.FromSeconds(3),
                Leaderboard = new[] { new RenderLeaderboardEntry { Username = "someone", UserId = 2, TotalScore = 2_000_000, Pp = 100, Accuracy = 0.9, MaxCombo = 10, Mods = new[] { "HD", "DT" } } },
            });
            AddUntilStep("wait for gameplay", () => renderScreen.Player?.Hud?.IsLoaded == true);

            AddStep("add a coloured leaderboard", () => renderScreen.Player.Hud.Add(leaderboard = new DrawableGameplayLeaderboard
            {
                UseCustomColours = { Value = true },
                BackgroundColour = { Value = Colour4.Purple },
                PlayerColour = { Value = Colour4.Red },
                BackgroundOpacity = { Value = 1 },
            }));

            AddUntilStep("rows shown", () => leaderboard.ChildrenOfType<DrawableGameplayLeaderboardScore>().Count(), () => Is.EqualTo(2));
            AddAssert("rows use the leaderboard's settings", () => leaderboard.ChildrenOfType<DrawableGameplayLeaderboardScore>()
                                                                             .All(r => r.UseCustomColours.Value && r.CustomColour.Value == Colour4.Purple && r.BackgroundOpacity.Value == 1));
            AddAssert("other score shows its mods", () => leaderboard.ChildrenOfType<DrawableGameplayLeaderboardScore>().Single(r => !r.Tracked)
                                                                         .ChildrenOfType<ModIcon>().Count(), () => Is.EqualTo(2));
            AddAssert("player's autoplay isn't shown as a mod", () => leaderboard.ChildrenOfType<DrawableGameplayLeaderboardScore>().Single(r => r.Tracked)
                                                                                   .ChildrenOfType<ModIcon>().Count(), () => Is.Zero);
            AddStep("back to lazer's look", () => leaderboard.UseCustomColours.Value = false);
            AddAssert("rows follow", () => leaderboard.ChildrenOfType<DrawableGameplayLeaderboardScore>().All(r => !r.UseCustomColours.Value));
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
