// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace Oily.Rendering.Akatsuki
{
    /// <summary>
    /// Akatsuki's relax star rating: the 2019 osu! difficulty calculation they use for relax,
    /// ported from <c>osu_2019</c> in https://github.com/osuAkatsuki/akatsuki-pp-rs (MIT, a fork of rosu-pp).
    /// </summary>
    /// <remarks>
    /// Kept close to the original, single precision included, so the numbers match theirs.
    /// Like theirs, stack leniency is ignored.
    /// </remarks>
    public class AkatsukiRelaxDifficulty
    {
        private const float object_radius = 64;
        private const float section_length = 400;
        private const float difficulty_multiplier = 0.0675f;
        private const float normalised_radius = 52;

        private const double legacy_last_tick_offset = 36;

        /// <summary>
        /// The difficulty after each object: entry <c>i</c> is for a play that has reached object <c>i</c>.
        /// </summary>
        public IReadOnlyList<AkatsukiRelaxAttributes> AfterObject { get; }

        /// <summary>
        /// The map's objects in order, for finding where a play is.
        /// </summary>
        public IReadOnlyList<OsuHitObject> Objects { get; }

        /// <param name="beatmap">The playable beatmap, with the play's mods already applied to its difficulty.</param>
        /// <param name="clockRate">The play's speed, for example 1.5 for Double Time.</param>
        public AkatsukiRelaxDifficulty(IBeatmap beatmap, double clockRate)
        {
            var objects = new List<OsuHitObject>();

            foreach (var h in beatmap.HitObjects)
            {
                if (h is OsuHitObject osu)
                    objects.Add(osu);
            }

            Objects = objects;

            float cs = beatmap.Difficulty.CircleSize;
            double ar = adjustApproachRate(beatmap.Difficulty.ApproachRate, clockRate);
            double od = adjustOverallDifficulty(beatmap.Difficulty.OverallDifficulty, clockRate);

            float radius = object_radius * (1 - 0.7f * (cs - 5) / 5) / 2;
            float scalingFactor = normalised_radius / radius;

            if (radius < 30)
                scalingFactor *= 1 + Math.Min(30 - radius, 5) / 50;

            var after = new AkatsukiRelaxAttributes[objects.Count];
            AfterObject = after;
            var counts = new AkatsukiRelaxAttributes { ApproachRate = ar, OverallDifficulty = od };

            var difficultyObjects = new List<StrainObject>(objects.Count);
            var tickTimes = new List<double>();

            foreach (var h in objects)
                difficultyObjects.Add(createObject(h, radius, scalingFactor, tickTimes, ref counts, after, difficultyObjects.Count));

            if (objects.Count < 2)
                return;

            float sectionLength = section_length * (float)clockRate;

            var aim = new Skill(SkillKind.Aim);
            var speed = new Skill(SkillKind.Speed);

            float currentSectionEnd = MathF.Ceiling((float)objects[0].StartTime / sectionLength) * sectionLength;

            StrainObject? prevPrev = null;
            var prev = difficultyObjects[0];
            (float jumpDistance, float strainTime)? prevValues = null;

            for (int i = 1; i < difficultyObjects.Count; i++)
            {
                var curr = difficultyObjects[i];
                var h = new DifficultyObject(curr, prev, prevValues, prevPrev, (float)clockRate, scalingFactor);

                // The second object starts the first section; later ones save each section they pass.
                while (h.Base.Time > currentSectionEnd)
                {
                    if (i > 1)
                    {
                        aim.SaveCurrentPeak();
                        aim.StartNewSectionFrom(currentSectionEnd);
                        speed.SaveCurrentPeak();
                        speed.StartNewSectionFrom(currentSectionEnd);
                    }

                    currentSectionEnd += sectionLength;
                }

                aim.Process(h);
                speed.Process(h);

                prevPrev = prev;
                prevValues = (h.JumpDistance, h.StrainTime);
                prev = curr;

                float aimStrain = MathF.Sqrt(aim.DifficultyValueWithCurrentPeak()) * difficulty_multiplier;
                float speedStrain = MathF.Sqrt(speed.DifficultyValueWithCurrentPeak()) * difficulty_multiplier;

                after[i] = after[i] with
                {
                    AimStrain = aimStrain,
                    SpeedStrain = speedStrain,
                    StarRating = aimStrain + speedStrain + Math.Abs(aimStrain - speedStrain) / 2,
                };
            }
        }

        // rosu-pp's clock rate adjusted approach rate: the preempt time scaled by the rate, turned back into AR.
        private static double adjustApproachRate(double ar, double clockRate)
        {
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(ar, 1800, 1200, 450) / clockRate;
            return preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5;
        }

        private static double adjustOverallDifficulty(double od, double clockRate)
        {
            double greatWindow = IBeatmapDifficultyInfo.DifficultyRange(od, 80, 50, 20) / clockRate;
            return (80 - greatWindow) / 6;
        }

        private StrainObject createObject(OsuHitObject h, float radius, float scalingFactor, List<double> tickTimes,
                                          ref AkatsukiRelaxAttributes counts, AkatsukiRelaxAttributes[] after, int index)
        {
            counts = counts with { MaxCombo = counts.MaxCombo + 1 };

            StrainObject result;

            switch (h)
            {
                case Slider slider:
                    counts = counts with { SliderCount = counts.SliderCount + 1 };
                    result = createSlider(slider, radius, scalingFactor, tickTimes, ref counts);
                    break;

                case Spinner:
                    counts = counts with { SpinnerCount = counts.SpinnerCount + 1 };
                    result = new StrainObject((float)h.StartTime, h.Position, h.Position, null);
                    break;

                default:
                    counts = counts with { CircleCount = counts.CircleCount + 1 };
                    result = new StrainObject((float)h.StartTime, h.Position, h.Position, 0);
                    break;
            }

            after[index] = counts;
            return result;
        }

        private static StrainObject createSlider(Slider slider, float radius, float scalingFactor, List<double> tickTimes, ref AkatsukiRelaxAttributes counts)
        {
            double startTime = slider.StartTime;
            double velocity = slider.Velocity;
            double spanCount = slider.RepeatCount + 1;
            double distance = slider.Path.Distance;

            double totalDuration = spanCount * distance / velocity;
            double spanDuration = totalDuration / spanCount;

            Vector2 endPosition = slider.Position;
            float travelDistance = 0;
            float followRadius = radius * 3;
            int combo = 0;

            void computeVertex(double time)
            {
                combo++;

                double progress = (time - startTime) / spanDuration;

                if (progress % 2 >= 1)
                    progress = 1 - progress % 1;
                else
                    progress %= 1;

                var position = slider.Position + slider.Path.PositionAt(progress);
                var diff = position - endPosition;
                float dist = diff.Length;

                if (dist > followRadius)
                {
                    dist -= followRadius;
                    endPosition += diff.Normalized() * dist;
                    travelDistance += dist;
                }
            }

            double length = Math.Min(distance, 100_000);
            double tickDistance = Math.Clamp(slider.TickDistance, 0, length);
            double minDistanceFromEnd = velocity * 10;

            if (tickDistance != 0)
            {
                // Ticks of the first span.
                for (double d = tickDistance; d < length - minDistanceFromEnd; d += tickDistance)
                {
                    double time = startTime + d / length * spanDuration;
                    computeVertex(time);
                    tickTimes.Add(time);
                }

                for (int span = 1; span <= slider.RepeatCount; span++)
                {
                    // The repeat point.
                    computeVertex(startTime + spanDuration * span);

                    double spanOffset = span * spanDuration;

                    if ((span & 1) == 1)
                    {
                        double reversedBase = startTime + startTime + spanDuration;

                        for (int t = tickTimes.Count - 1; t >= 0; t--)
                            computeVertex(spanOffset + reversedBase - tickTimes[t]);
                    }
                    else
                    {
                        foreach (double time in tickTimes)
                            computeVertex(spanOffset + time);
                    }
                }

                tickTimes.Clear();
            }

            // The tail, at its legacy time.
            double finalSpanStart = startTime + slider.RepeatCount * spanDuration;
            computeVertex(Math.Max(startTime + totalDuration / 2, finalSpanStart + spanDuration - legacy_last_tick_offset));

            counts = counts with { MaxCombo = counts.MaxCombo + combo };

            return new StrainObject((float)startTime, slider.Position, endPosition, travelDistance * scalingFactor);
        }

        // TravelDistance is 0 for circles, the scaled cursor travel for sliders, and null for spinners.
        private record StrainObject(float Time, Vector2 Position, Vector2 EndPosition, float? TravelDistance)
        {
            public bool IsSpinner => TravelDistance == null;
        }

        private class DifficultyObject
        {
            public readonly StrainObject Base;
            public readonly (float jumpDistance, float strainTime)? Prev;
            public readonly float JumpDistance;
            public readonly float TravelDistance;
            public readonly float? Angle;
            public readonly float Delta;
            public readonly float StrainTime;

            public DifficultyObject(StrainObject current, StrainObject prev, (float, float)? prevValues, StrainObject? prevPrev, float clockRate, float scalingFactor)
            {
                Base = current;
                Prev = prevValues;

                Delta = (current.Time - prev.Time) / clockRate;
                StrainTime = Math.Max(Delta, 50);

                TravelDistance = prev.TravelDistance ?? 0;
                JumpDistance = current.IsSpinner ? 0 : ((current.Position - prev.EndPosition) * scalingFactor).Length;

                if (prevPrev != null)
                {
                    var v1 = prevPrev.EndPosition - prev.Position;
                    var v2 = current.Position - prev.EndPosition;

                    float dot = Vector2.Dot(v1, v2);
                    float det = v1.X * v2.Y - v1.Y * v2.X;

                    Angle = MathF.Abs(MathF.Atan2(det, dot));
                }
            }
        }

        private enum SkillKind
        {
            Aim,
            Speed,
        }

        private class Skill
        {
            private const float single_spacing_threshold = 125;
            private const float speed_angle_bonus_begin = 5 * MathF.PI / 6;
            private const float min_speed_bonus = 75;
            private const float max_speed_bonus = 45;
            private const float speed_balancing_factor = 40;
            private const float aim_angle_bonus_begin = MathF.PI / 3;
            private const float timing_threshold = 107;
            private const float decay_weight = 0.9f;

            private readonly SkillKind kind;
            private readonly List<float> strainPeaks = new List<float>();
            private readonly List<float> sortedPeaks = new List<float>();

            private float currentStrain = 1;
            private float currentSectionPeak = 1;
            private float? prevTime;

            public Skill(SkillKind kind)
            {
                this.kind = kind;
            }

            private float skillMultiplier => kind == SkillKind.Aim ? 26.25f : 1400;

            private float strainDecayBase => kind == SkillKind.Aim ? 0.15f : 0.3f;

            public void SaveCurrentPeak() => strainPeaks.Add(currentSectionPeak);

            public void StartNewSectionFrom(float time) => currentSectionPeak = currentStrain * strainDecay(time - prevTime!.Value);

            public void Process(DifficultyObject current)
            {
                currentStrain *= strainDecay(current.Delta);
                currentStrain += strainValueOf(current) * skillMultiplier;

                currentSectionPeak = Math.Max(currentSectionPeak, currentStrain);
                prevTime = current.Base.Time;
            }

            /// <summary>
            /// The difficulty if the map ended here: every saved section, plus the one in progress.
            /// </summary>
            public float DifficultyValueWithCurrentPeak()
            {
                sortedPeaks.Clear();
                sortedPeaks.AddRange(strainPeaks);
                sortedPeaks.Add(currentSectionPeak);
                sortedPeaks.Sort((a, b) => b.CompareTo(a));

                float difficulty = 0;
                float weight = 1;

                foreach (float strain in sortedPeaks)
                {
                    difficulty += strain * weight;
                    weight *= decay_weight;
                }

                return difficulty;
            }

            private float strainDecay(float ms) => MathF.Pow(strainDecayBase, ms / 1000);

            private float strainValueOf(DifficultyObject current)
            {
                if (current.Base.IsSpinner)
                    return 0;

                return kind == SkillKind.Aim ? aimValueOf(current) : speedValueOf(current);
            }

            private static float aimValueOf(DifficultyObject current)
            {
                float result = 0;

                if (current.Prev is (float prevJumpDistance, float prevStrainTime) && current.Angle is float angle && angle > aim_angle_bonus_begin)
                {
                    const float scale = 90;

                    float angleBonus = MathF.Sqrt(MathF.Pow(MathF.Sin(angle - aim_angle_bonus_begin), 2)
                                                  * Math.Max(prevJumpDistance - scale, 0)
                                                  * Math.Max(current.JumpDistance - scale, 0));

                    result = 1.5f * diminishingExp(Math.Max(angleBonus, 0)) / Math.Max(timing_threshold, prevStrainTime);
                }

                float jumpDistanceExp = diminishingExp(current.JumpDistance);
                float travelDistanceExp = diminishingExp(current.TravelDistance);

                float distanceExp = jumpDistanceExp + travelDistanceExp + MathF.Sqrt(travelDistanceExp * jumpDistanceExp);

                return Math.Max(result + distanceExp / Math.Max(current.StrainTime, timing_threshold), distanceExp / current.StrainTime);
            }

            private static float speedValueOf(DifficultyObject current)
            {
                float distance = Math.Min(single_spacing_threshold, current.TravelDistance + current.JumpDistance);
                float deltaTime = Math.Max(max_speed_bonus, current.Delta);

                float speedBonus = 1;

                if (deltaTime < min_speed_bonus)
                {
                    float expBase = (min_speed_bonus - deltaTime) / speed_balancing_factor;
                    speedBonus += expBase * expBase;
                }

                float angleBonus = 1;

                if (current.Angle is float angle && angle < speed_angle_bonus_begin)
                {
                    float expBase = MathF.Sin(1.5f * (speed_angle_bonus_begin - angle));
                    angleBonus = 1 + expBase * expBase / 3.57f;

                    if (angle < MathF.PI / 2)
                    {
                        angleBonus = 1.28f;

                        if (distance < 90 && angle < MathF.PI / 4)
                            angleBonus += (1 - angleBonus) * Math.Min((90 - distance) / 10, 1);
                        else if (distance < 90)
                            angleBonus += (1 - angleBonus) * Math.Min((90 - distance) / 10, 1) * MathF.Sin((MathF.PI / 2 - angle) / (MathF.PI / 4));
                    }
                }

                return (1 + (speedBonus - 1) * 0.75f)
                       * angleBonus
                       * (0.95f + speedBonus * MathF.Pow(distance / single_spacing_threshold, 3.5f))
                       / current.StrainTime;
            }

            private static float diminishingExp(float value) => MathF.Pow(value, 0.99f);
        }
    }

    /// <summary>
    /// The relax difficulty of a map, or of the part of it played so far.
    /// </summary>
    public record AkatsukiRelaxAttributes
    {
        public float AimStrain { get; init; }
        public float SpeedStrain { get; init; }
        public float StarRating { get; init; }

        /// <summary>Approach rate, adjusted for the play's speed.</summary>
        public double ApproachRate { get; init; }

        /// <summary>Overall difficulty, adjusted for the play's speed.</summary>
        public double OverallDifficulty { get; init; }

        public int CircleCount { get; init; }
        public int SliderCount { get; init; }
        public int SpinnerCount { get; init; }

        /// <summary>The highest combo possible, the stable way: one per circle, slider head, tick, repeat, tail and spinner.</summary>
        public int MaxCombo { get; init; }

        public int ObjectCount => CircleCount + SliderCount + SpinnerCount;
    }
}
