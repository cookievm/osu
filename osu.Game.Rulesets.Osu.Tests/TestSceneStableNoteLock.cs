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
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using Oily.Identity;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// oily: o!ily plays osu! standard with stable notelock, but plays back official lazer replays with lazer's rules.
    /// </summary>
    public partial class TestSceneStableNoteLock : RateAdjustedBeatmapTestScene
    {
        private const double time_first = 1000;
        private const double time_second = 1100;

        private static readonly Vector2 position_first = new Vector2(100, 192);
        private static readonly Vector2 position_second = new Vector2(400, 192);

        private ScoreAccessibleReplayPlayer currentPlayer = null!;
        private List<JudgementResult> judgementResults = new List<JudgementResult>();

        [Test]
        public void TestClientVersionTag()
        {
            AddAssert("plain lazer version has no notelock", () => OilyIdentity.PlayedWithStableNoteLock("2026.1005.0"), () => Is.False);
            AddAssert("empty version has no notelock", () => OilyIdentity.PlayedWithStableNoteLock(null), () => Is.False);
            AddAssert("tagged version has notelock", () => OilyIdentity.PlayedWithStableNoteLock(OilyIdentity.TagClientVersion("2026.1005.0")), () => Is.True);
            AddAssert("tagging twice adds one tag", () => OilyIdentity.TagClientVersion(OilyIdentity.TagClientVersion("1.0")), () => Is.EqualTo(OilyIdentity.TagClientVersion("1.0")));
        }

        /// <summary>
        /// Clicking the second circle after the first circle's start time, while the first circle can still be hit.
        /// Lazer allows it (and misses the first circle). Stable's notelock blocks it.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void TestClickSecondCircleWhileFirstIsHittable(bool playedInOily)
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
            }, playedInOily);

            AddAssert("first circle missed", () => resultFor(hitObjects[0]), () => Is.EqualTo(HitResult.Miss));

            if (playedInOily)
                AddAssert("second circle blocked by notelock", () => resultFor(hitObjects[1]), () => Is.EqualTo(HitResult.Miss));
            else
                AddAssert("second circle hit", () => resultFor(hitObjects[1]), () => Is.Not.EqualTo(HitResult.Miss));
        }

        private HitResult resultFor(HitObject hitObject) => judgementResults.Single(r => r.HitObject == hitObject).Type;

        private void performTest(List<OsuHitObject> hitObjects, List<ReplayFrame> frames, bool playedInOily)
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
                    ScoreInfo = { ClientVersion = playedInOily ? OilyIdentity.TagClientVersion("2026.1005.0") : "2026.1005.0" },
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
                () => Is.EqualTo(playedInOily));
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
