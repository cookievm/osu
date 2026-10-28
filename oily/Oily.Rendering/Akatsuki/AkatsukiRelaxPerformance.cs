// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace Oily.Rendering.Akatsuki
{
    /// <summary>
    /// Akatsuki's relax pp for a play, ported from <c>osu_2019::OsuPP</c> in https://github.com/osuAkatsuki/akatsuki-pp-rs (MIT).
    /// </summary>
    public static class AkatsukiRelaxPerformance
    {
        /// <summary>
        /// The pp of a play.
        /// </summary>
        /// <param name="attributes">The difficulty of the part of the map played so far.</param>
        /// <param name="play">The play so far.</param>
        public static double Calculate(AkatsukiRelaxAttributes attributes, AkatsukiRelaxPlay play)
        {
            int objectCount = attributes.ObjectCount;

            if (objectCount == 0)
                return 0;

            int n300 = play.Count300;
            int n100 = play.Count100;
            int n50 = play.Count50;
            int misses = play.Misses;

            // Anything not judged yet counts as a 300, as theirs does.
            int remaining = objectCount - n300 - n100 - n50 - misses;
            if (remaining > 0)
                n300 += remaining;

            float accuracy = (n50 + n100 * 2 + n300 * 6) / (float)objectCount / 6;
            float totalHits = Math.Min(n300 + n100 + n50 + misses, objectCount);

            float multiplier = 1.09f;
            float effectiveMisses = effectiveMissCount(attributes, play.MaxCombo, n100, n50, misses);

            if (play.SpunOut)
                multiplier *= 1 - MathF.Pow(attributes.SpinnerCount / totalHits, 0.85f);

            float aimValue = computeAimValue(attributes, play, totalHits, effectiveMisses, accuracy);
            float speedValue = computeSpeedValue(attributes, play, totalHits, effectiveMisses, accuracy, n50);
            float accuracyValue = computeAccuracyValue(attributes, play, totalHits, n300, n100, n50);

            float accuracyDepression = 1;

            double streamsNerf = Math.Round(attributes.AimStrain / (double)attributes.SpeedStrain * 100) / 100;

            if (streamsNerf < 1.09)
            {
                float accuracyFactor = Math.Abs(1 - accuracy);
                accuracyDepression = Math.Max(0.86f - accuracyFactor, 0.5f);

                if (accuracyDepression > 0)
                    aimValue *= accuracyDepression;
            }

            float pp = MathF.Pow(MathF.Pow(aimValue, 1.185f)
                                 + MathF.Pow(speedValue, 0.83f * accuracyDepression)
                                 + MathF.Pow(accuracyValue, 1.14f), 1 / 1.1f)
                       * multiplier;

            return float.IsFinite(pp) ? pp : 0;
        }

        private static float lengthBonus(float totalHits)
            => 0.88f + 0.4f * Math.Min(totalHits / 2000, 1) + (totalHits > 2000 ? 0.5f * MathF.Log10(totalHits / 2000) : 0);

        private static float computeAimValue(AkatsukiRelaxAttributes attributes, AkatsukiRelaxPlay play, float totalHits, float effectiveMisses, float accuracy)
        {
            float rawAim = play.TouchDevice ? MathF.Pow(attributes.AimStrain, 0.8f) : attributes.AimStrain;

            float aimValue = MathF.Pow(5 * Math.Max(rawAim / 0.0675f, 1) - 4, 3) / 100_000;

            float lenBonus = lengthBonus(totalHits);
            aimValue *= lenBonus;

            if (effectiveMisses > 0)
                aimValue *= missPenalty(effectiveMisses, totalHits);

            double ar = attributes.ApproachRate;
            double arFactor = ar > 10.33 ? 0.3 * (ar - 10.33) : 0;

            if (ar < 8)
                arFactor = 0.025 * (8 - ar);

            aimValue *= 1 + (float)arFactor * lenBonus;

            if (play.Hidden)
                aimValue *= 1 + 0.05f * (float)(11 - ar);

            if (play.Flashlight)
            {
                aimValue *= 1
                            + 0.3f * Math.Min(totalHits / 200, 1)
                            + (totalHits > 200 ? 0.25f * Math.Min((totalHits - 200) / 300, 1) : 0)
                            + (totalHits > 500 ? (totalHits - 500) / 1600 : 0);
            }

            float od = (float)attributes.OverallDifficulty;

            aimValue *= 0.3f + accuracy / 2;
            aimValue *= 0.98f + od * od / 2500;

            return aimValue;
        }

        private static float computeSpeedValue(AkatsukiRelaxAttributes attributes, AkatsukiRelaxPlay play, float totalHits, float effectiveMisses, float accuracy, int n50)
        {
            float speedValue = MathF.Pow(5 * Math.Max(attributes.SpeedStrain / 0.0675f, 1) - 4, 3) / 100_000;

            float lenBonus = lengthBonus(totalHits);
            speedValue *= lenBonus;

            if (effectiveMisses > 0)
                speedValue *= missPenalty(effectiveMisses, totalHits);

            double ar = attributes.ApproachRate;

            if (ar > 10.33)
                speedValue *= 1 + (float)(0.3 * (ar - 10.33)) * lenBonus;

            if (play.Hidden)
                speedValue *= 1 + 0.05f * (float)(11 - ar);

            float od = (float)attributes.OverallDifficulty;

            speedValue *= (0.93f + od * od / 750) * MathF.Pow(accuracy, (14.5f - Math.Max(od, 8)) / 2);
            speedValue *= MathF.Pow(0.98f, n50 < totalHits / 500 ? 0 : n50 - totalHits / 500);

            return speedValue;
        }

        private static float computeAccuracyValue(AkatsukiRelaxAttributes attributes, AkatsukiRelaxPlay play, float totalHits, int n300, int n100, int n50)
        {
            float circles = attributes.CircleCount;

            float betterAccuracy = circles > 0
                ? Math.Max(((n300 - (totalHits - circles)) * 6 + n100 * 2 + n50) / (circles * 6), 0)
                : 0;

            float accuracyValue = MathF.Pow(1.52163f, (float)attributes.OverallDifficulty) * MathF.Pow(betterAccuracy, 24) * 2.83f;

            accuracyValue *= Math.Min(MathF.Pow(circles / 1000, 0.3f), 1.15f);

            if (play.Hidden)
                accuracyValue *= 1.08f;

            if (play.Flashlight)
                accuracyValue *= 1.02f;

            return accuracyValue;
        }

        private static float missPenalty(float effectiveMisses, float totalHits)
            => 0.97f * MathF.Pow(1 - MathF.Pow(effectiveMisses / totalHits, 0.5f), 1 + effectiveMisses / 1.5f);

        private static float effectiveMissCount(AkatsukiRelaxAttributes attributes, int combo, int n100, int n50, int misses)
        {
            float comboBasedMisses = 0;

            if (attributes.SliderCount > 0)
            {
                float fullComboThreshold = attributes.MaxCombo - 0.1f * attributes.SliderCount;

                if (combo < fullComboThreshold)
                    comboBasedMisses = fullComboThreshold / Math.Max(combo, 1);
            }

            comboBasedMisses = Math.Min(comboBasedMisses, n100 + n50 + misses);
            return Math.Max(comboBasedMisses, misses);
        }
    }

    /// <summary>
    /// A relax play, as Akatsuki's pp sees it.
    /// </summary>
    public record AkatsukiRelaxPlay
    {
        public int Count300 { get; init; }
        public int Count100 { get; init; }
        public int Count50 { get; init; }
        public int Misses { get; init; }
        public int MaxCombo { get; init; }

        public bool Hidden { get; init; }
        public bool Flashlight { get; init; }
        public bool TouchDevice { get; init; }
        public bool SpunOut { get; init; }
    }
}
