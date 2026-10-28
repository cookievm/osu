// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Utils;

namespace Oily.Rendering.Akatsuki
{
    /// <summary>
    /// Shows Akatsuki's relax pp in the game's PP counter.
    /// </summary>
    public class AkatsukiRelaxLiveCalculator : ILivePerformanceCalculator
    {
        private readonly AkatsukiRelaxDifficulty difficulty;
        private readonly Dictionary<OsuHitObject, int> objectIndex = new Dictionary<OsuHitObject, int>();
        private readonly HitResult?[] results;

        // Without the Classic mod, lazer doesn't grade sliders, so they're graded here from their parts, like stable does.
        private readonly Dictionary<OsuHitObject, Slider> sliderOfPart = new Dictionary<OsuHitObject, Slider>();
        private readonly Dictionary<Slider, int> sliderPartsHit = new Dictionary<Slider, int>();

        private readonly AkatsukiRelaxPlay mods;

        private AkatsukiRelaxLiveCalculator(GameplayState gameplayState)
        {
            var playMods = gameplayState.Mods;

            difficulty = new AkatsukiRelaxDifficulty(gameplayState.Beatmap, ModUtils.CalculateRateWithMods(playMods));

            for (int i = 0; i < difficulty.Objects.Count; i++)
            {
                objectIndex[difficulty.Objects[i]] = i;

                if (difficulty.Objects[i] is Slider slider)
                {
                    foreach (var part in slider.NestedHitObjects.OfType<OsuHitObject>())
                        sliderOfPart[part] = slider;
                }
            }

            results = new HitResult?[difficulty.Objects.Count];

            mods = new AkatsukiRelaxPlay
            {
                Hidden = has(playMods, "HD"),
                Flashlight = has(playMods, "FL"),
                TouchDevice = has(playMods, "TD"),
                SpunOut = has(playMods, "SO"),
            };
        }

        /// <summary>
        /// A calculator for osu! plays with Relax, or null for anything else, which keeps the game's own pp.
        /// </summary>
        public static ILivePerformanceCalculator? CreateFor(GameplayState gameplayState)
        {
            if (gameplayState.Ruleset.ShortName != "osu" || !has(gameplayState.Mods, "RX"))
                return null;

            return new AkatsukiRelaxLiveCalculator(gameplayState);
        }

        public double Calculate(ScoreInfo score, JudgementResult judgement)
        {
            if (judgement.HitObject is OsuHitObject hitObject)
            {
                // Only whole objects count, as on stable: slider ticks and heads don't.
                if (objectIndex.TryGetValue(hitObject, out int index))
                    results[index] = hitObject is Slider slider ? gradeSlider(slider, judgement.Type) : judgement.Type;
                else if (judgement.IsHit && sliderOfPart.TryGetValue(hitObject, out var parent))
                    sliderPartsHit[parent] = sliderPartsHit.GetValueOrDefault(parent) + 1;
            }

            int judged = 0, n300 = 0, n100 = 0, n50 = 0, misses = 0, last = -1;

            for (int i = 0; i < results.Length; i++)
            {
                if (results[i] is not HitResult result)
                    continue;

                judged++;
                last = i;

                switch (result)
                {
                    case HitResult.Great:
                    case HitResult.Perfect:
                        n300++;
                        break;

                    case HitResult.Ok:
                    case HitResult.Good:
                        n100++;
                        break;

                    case HitResult.Meh:
                        n50++;
                        break;

                    default:
                        misses++;
                        break;
                }
            }

            if (judged == 0)
                return 0;

            var play = mods with
            {
                Count300 = n300,
                Count100 = n100,
                Count50 = n50,
                Misses = misses,
                MaxCombo = score.MaxCombo,
            };

            return AkatsukiRelaxPerformance.Calculate(difficulty.AfterObject[last], play);
        }

        /// <summary>
        /// The slider's grade: lazer's own with the Classic mod, otherwise stable's rule from how many of its parts were hit.
        /// </summary>
        private HitResult gradeSlider(Slider slider, HitResult result)
        {
            if (result is HitResult.Great or HitResult.Ok or HitResult.Meh or HitResult.Miss)
                return result;

            int total = slider.NestedHitObjects.Count;
            int hit = sliderPartsHit.GetValueOrDefault(slider);

            if (hit >= total)
                return HitResult.Great;

            if (hit == 0)
                return HitResult.Miss;

            return (double)hit / total >= 0.5 ? HitResult.Ok : HitResult.Meh;
        }

        private static bool has(IEnumerable<Mod> mods, string acronym) => mods.Any(m => m.Acronym == acronym);
    }
}
