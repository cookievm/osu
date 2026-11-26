// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// oily: o!ily's gamerules (stable notelock, slider accuracy) are saved with scores, and replays are played back with them.
    /// Official lazer replays are played back with lazer's rules.
    /// </summary>
    public partial class TestSceneOilyGameRules : RateAdjustedBeatmapTestScene
    {
        private const double time_first = 1000;
        private const double time_second = 1100;

        private static readonly Vector2 position_first = new Vector2(100, 192);
        private static readonly Vector2 position_second = new Vector2(400, 192);

        private ScoreAccessibleReplayPlayer currentPlayer = null!;
        private List<JudgementResult> judgementResults = new List<JudgementResult>();

        private const string lazer_version = "2026.1005.0";

        [Test]
        public void TestClientVersionTag()
        {
            AddAssert("plain lazer version uses lazer rules", () => OilyGameRules.FromClientVersion(lazer_version), () => Is.EqualTo(OilyGameRules.LAZER));
            AddAssert("empty version uses lazer rules", () => OilyGameRules.FromClientVersion(null), () => Is.EqualTo(OilyGameRules.LAZER));

            foreach (bool noteLock in new[] { true, false })
            {
                foreach (bool sliderAccuracy in new[] { true, false })
                {
                    var rules = new OilyGameRules(noteLock, sliderAccuracy);
                    AddAssert($"{rules} round trips", () => OilyGameRules.FromClientVersion(rules.TagClientVersion(lazer_version)), () => Is.EqualTo(rules));
                }
            }

            var stableTiming = new OilyGameRules(true, true, OilyHitTiming.Stable);
            AddAssert("stable timing round trips", () => OilyGameRules.FromClientVersion(stableTiming.TagClientVersion(lazer_version)), () => Is.EqualTo(stableTiming));
            AddAssert("stable timing tag", () => stableTiming.TagClientVersion(lazer_version), () => Is.EqualTo(lazer_version + "-oily.stabletiming"));

            AddAssert("default tag", () => OilyGameRules.DEFAULT.TagClientVersion(lazer_version), () => Is.EqualTo(lazer_version + "-oily"));
            AddAssert("retagging replaces the tag", () => OilyGameRules.DEFAULT.TagClientVersion(new OilyGameRules(false, false).TagClientVersion(lazer_version)),
                () => Is.EqualTo(lazer_version + "-oily"));
        }

        [Test]
        public void TestStableHitTiming()
        {
            var windows = new osu.Game.Rulesets.Osu.Scoring.OsuHitWindows();
            windows.SetDifficulty(5);

            // OD5: the 300 window is 49.5ms, so a hit 49.6ms late is a 100 for lazer but counts as 49ms (a 300) on stable's clock.
            AddAssert("lazer: 49.6ms late is a 100", () => windows.ResultFor(49.6), () => Is.EqualTo(osu.Game.Rulesets.Scoring.HitResult.Ok));
            AddStep("stable timing", () => windows.StableTiming = true);
            AddAssert("stable: 49.6ms late is a 300", () => windows.ResultFor(49.6), () => Is.EqualTo(osu.Game.Rulesets.Scoring.HitResult.Great));
            AddAssert("stable: 49.6ms early is a 100", () => windows.ResultFor(-49.6), () => Is.EqualTo(osu.Game.Rulesets.Scoring.HitResult.Ok));
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void TestSliderAccuracyRule(bool playedInOily, bool sliderAccuracy)
        {
            var hitObjects = new List<OsuHitObject>
            {
                new Slider
                {
                    StartTime = time_first,
                    Position = position_first,
                    Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }),
                },
            };

            string version = playedInOily ? new OilyGameRules(true, sliderAccuracy).TagClientVersion(lazer_version) : lazer_version;

            performTest(hitObjects, new List<ReplayFrame>(), version);

            bool expectStableSliders = playedInOily && !sliderAccuracy;
            AddAssert("slider judged like stable", () => currentPlayer.GameplayState.Beatmap.HitObjects.OfType<Slider>().Single().ClassicSliderBehaviour,
                () => Is.EqualTo(expectStableSliders));
        }

        /// <summary>
        /// Clicking the second circle after the first circle's start time, while the first circle can still be hit.
        /// Lazer allows it (and misses the first circle). Stable's notelock blocks it.
        /// </summary>
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void TestClickSecondCircleWhileFirstIsHittable(bool playedInOily, bool noteLock)
        {
            var hitObjects = new List<OsuHitObject>
            {
                new HitCircle { StartTime = time_first, Position = position_first },
                new HitCircle { StartTime = time_second, Position = position_second },
            };

            performTest(hitObjects, new List<ReplayFrame>
            {
                new OsuReplayFrame { Time = time_first + 40, Position = position_second, Actions = { OsuAction.LeftButton } },
                new OsuReplayFrame { Time = time_first + 60, Position = position_second },
            }, playedInOily ? new OilyGameRules(noteLock, true).TagClientVersion(lazer_version) : lazer_version);

            AddAssert("first circle missed", () => resultFor(hitObjects[0]), () => Is.EqualTo(HitResult.Miss));

            if (playedInOily && noteLock)
                AddAssert("second circle blocked by notelock", () => resultFor(hitObjects[1]), () => Is.EqualTo(HitResult.Miss));
            else
                AddAssert("second circle hit", () => resultFor(hitObjects[1]), () => Is.Not.EqualTo(HitResult.Miss));
        }

        private HitResult resultFor(HitObject hitObject) => judgementResults.Single(r => r.HitObject == hitObject).Type;

        private void performTest(List<OsuHitObject> hitObjects, List<ReplayFrame> frames, string clientVersion)
        {
            AddStep("load player", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new Beatmap<OsuHitObject>
                {
                    HitObjects = hitObjects,
                    BeatmapInfo =
                    {
                        Difficulty = new BeatmapDifficulty { OverallDifficulty = 5, CircleSize = 4 },
                        Ruleset = new OsuRuleset().RulesetInfo
                    },
                });

                var score = new Score
                {
                    Replay = new Replay { Frames = frames },
                    ScoreInfo = { ClientVersion = clientVersion },
                };

                var p = new ScoreAccessibleReplayPlayer(score);

                p.OnLoadComplete += _ =>
                {
                    p.ScoreProcessor.NewJudgement += result =>
                    {
                        if (currentPlayer == p) judgementResults.Add(result);
                    };
                };

                LoadScreen(currentPlayer = p);
                judgementResults = new List<JudgementResult>();
            });

            AddUntilStep("Beatmap at 0", () => Beatmap.Value.Track.CurrentTime == 0);
            AddUntilStep("Wait until player is loaded", () => currentPlayer.IsCurrentScreen());
            AddAssert("notelock matches replay", () => ((StableNoteLockHitPolicy)currentPlayer.ChildrenOfType<OsuPlayfield>().Single().HitPolicy).Enabled,
                () => Is.EqualTo(OilyGameRules.FromClientVersion(clientVersion).NoteLock));
            AddUntilStep("Wait for completion", () => currentPlayer.ScoreProcessor.HasCompleted.Value);
        }

        private partial class ScoreAccessibleReplayPlayer : ReplayPlayer
        {
            public new ScoreProcessor ScoreProcessor => base.ScoreProcessor;

            protected override bool PauseOnFocusLost => false;

            public ScoreAccessibleReplayPlayer(Score score)
                : base(score, new PlayerConfiguration
                {
                    AllowPause = false,
                    ShowResults = false,
                })
            {
            }
        }
    }
}
