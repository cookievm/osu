// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using Oily.Rendering.Akatsuki;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace Oily.Rendering.Tests
{
    [TestFixture]
    public class AkatsukiRelaxTest
    {
        [Test]
        public void TestTwoCirclesMatchTheFormula()
        {
            // Worked out by hand from akatsuki-pp-rs: CS 4, 100 px apart, 200 ms apart.
            var difficulty = new AkatsukiRelaxDifficulty(beatmap(4, new HitCircle { StartTime = 0, Position = Vector2.Zero }, new HitCircle { StartTime = 200, Position = new Vector2(100, 0) }), 1);

            var attributes = difficulty.AfterObject[1];

            Assert.That(attributes.AimStrain, Is.EqualTo(0.290233).Within(1e-4));
            Assert.That(attributes.SpeedStrain, Is.EqualTo(0.256464).Within(1e-4));
            Assert.That(attributes.StarRating, Is.EqualTo(0.563582).Within(1e-4));
        }

        [Test]
        public void TestFirstObjectHasNoDifficulty()
        {
            var difficulty = new AkatsukiRelaxDifficulty(beatmap(4, new HitCircle { StartTime = 0, Position = Vector2.Zero }), 1);

            Assert.That(difficulty.AfterObject[0].StarRating, Is.Zero);
            Assert.That(difficulty.AfterObject[0].CircleCount, Is.EqualTo(1));
        }

        [Test]
        public void TestCountsAndComboGrowWithTheMap()
        {
            var slider = new Slider
            {
                StartTime = 500,
                Position = new Vector2(100, 100),
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }),
            };

            var difficulty = new AkatsukiRelaxDifficulty(beatmap(4,
                new HitCircle { StartTime = 0, Position = Vector2.Zero },
                slider,
                new Spinner { StartTime = 5000, EndTime = 7000, Position = new Vector2(256, 192) }), 1);

            Assert.That(difficulty.AfterObject[0].MaxCombo, Is.EqualTo(1));

            // Head, one tick (140 px apart on a 200 px slider) and the tail.
            Assert.That(difficulty.AfterObject[1].MaxCombo, Is.EqualTo(4));
            Assert.That(difficulty.AfterObject[1].SliderCount, Is.EqualTo(1));

            Assert.That(difficulty.AfterObject[2].MaxCombo, Is.EqualTo(5));
            Assert.That(difficulty.AfterObject[2].SpinnerCount, Is.EqualTo(1));
            Assert.That(difficulty.AfterObject[2].ObjectCount, Is.EqualTo(3));
        }

        [Test]
        public void TestDoubleTimeAdjustsApproachRateAndOverallDifficulty()
        {
            var map = beatmap(4, new HitCircle { StartTime = 0 }, new HitCircle { StartTime = 200, Position = new Vector2(100, 0) });
            map.Difficulty.ApproachRate = 9;
            map.Difficulty.OverallDifficulty = 8;

            var attributes = new AkatsukiRelaxDifficulty(map, 1.5).AfterObject[1];

            Assert.That(attributes.ApproachRate, Is.EqualTo(10.333).Within(1e-3));
            Assert.That(attributes.OverallDifficulty, Is.EqualTo(9.778).Within(1e-3));
            Assert.That(attributes.StarRating, Is.GreaterThan(new AkatsukiRelaxDifficulty(map, 1).AfterObject[1].StarRating));
        }

        [Test]
        public void TestMissesAndHiddenChangePp()
        {
            var attributes = streamDifficulty();
            int objects = attributes.ObjectCount;

            double fullCombo = AkatsukiRelaxPerformance.Calculate(attributes, new AkatsukiRelaxPlay { Count300 = objects, MaxCombo = attributes.MaxCombo });
            double withMisses = AkatsukiRelaxPerformance.Calculate(attributes, new AkatsukiRelaxPlay { Count300 = objects - 3, Misses = 3, MaxCombo = attributes.MaxCombo / 3 });
            double hidden = AkatsukiRelaxPerformance.Calculate(attributes, new AkatsukiRelaxPlay { Count300 = objects, MaxCombo = attributes.MaxCombo, Hidden = true });

            Assert.That(fullCombo, Is.GreaterThan(0));
            Assert.That(withMisses, Is.LessThan(fullCombo));
            Assert.That(hidden, Is.GreaterThan(fullCombo));
        }

        [Test]
        public void TestNothingPlayedIsZero()
        {
            Assert.That(AkatsukiRelaxPerformance.Calculate(new AkatsukiRelaxAttributes(), new AkatsukiRelaxPlay()), Is.Zero);
        }

        private static AkatsukiRelaxAttributes streamDifficulty()
        {
            var objects = new List<HitObject>();

            for (int i = 0; i < 200; i++)
                objects.Add(new HitCircle { StartTime = i * 100, Position = new Vector2(100 + i % 2 * 120, 192) });

            var difficulty = new AkatsukiRelaxDifficulty(beatmap(4, objects.ToArray()), 1);
            return difficulty.AfterObject[^1];
        }

        private static Beatmap beatmap(float circleSize, params HitObject[] objects)
        {
            var map = new Beatmap
            {
                Difficulty = new BeatmapDifficulty { CircleSize = circleSize, ApproachRate = 9, OverallDifficulty = 8 },
                HitObjects = new List<HitObject>(objects),
            };

            foreach (var h in objects)
                h.ApplyDefaults(map.ControlPointInfo, map.Difficulty);

            return map;
        }
    }
}
