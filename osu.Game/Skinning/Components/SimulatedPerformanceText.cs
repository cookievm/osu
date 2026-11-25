// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Skinning.Components
{
    /// <summary>
    /// oily: shows how much pp a score would give on the selected beatmap, with the selected mods.
    /// The score is set in the component's settings (accuracy, misses, combo).
    /// </summary>
    public partial class SimulatedPerformanceText : FontAdjustableSkinComponent
    {
        [SettingSource("Accuracy", "The accuracy of the pretend score, in percent.")]
        public BindableDouble Accuracy { get; } = new BindableDouble(100)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 0.01,
        };

        [SettingSource("Misses", "How many misses the pretend score has.")]
        public BindableInt Misses { get; } = new BindableInt
        {
            MinValue = 0,
            MaxValue = 1000,
        };

        [SettingSource("Combo", "The pretend score's max combo, in percent of the beatmap's max combo.")]
        public BindableDouble ComboPercent { get; } = new BindableDouble(100)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 0.1,
        };

        [SettingSource("Text", "What to show. {Value} is the pp, {Accuracy} the accuracy, {Misses} the misses.")]
        public Bindable<string> Template { get; } = new Bindable<string>("{Accuracy}% {Misses}x: {Value}pp");

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private readonly OsuSpriteText text;

        private CancellationTokenSource? cancellationSource;
        private ModSettingChangeTracker? modSettingTracker;
        private double? performance;

        public SimulatedPerformanceText()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = text = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmap.BindValueChanged(_ => Scheduler.AddOnce(recalculate));
            ruleset.BindValueChanged(_ => Scheduler.AddOnce(recalculate));
            mods.BindValueChanged(m =>
            {
                modSettingTracker?.Dispose();
                modSettingTracker = new ModSettingChangeTracker(m.NewValue)
                {
                    SettingChanged = _ => Scheduler.AddOnce(recalculate)
                };

                Scheduler.AddOnce(recalculate);
            });

            Accuracy.BindValueChanged(_ => Scheduler.AddOnce(recalculate));
            Misses.BindValueChanged(_ => Scheduler.AddOnce(recalculate));
            ComboPercent.BindValueChanged(_ => Scheduler.AddOnce(recalculate));
            Template.BindValueChanged(_ => updateText());

            recalculate();
        }

        private void recalculate()
        {
            cancellationSource?.Cancel();
            var cts = cancellationSource = new CancellationTokenSource();

            var working = beatmap.Value;
            var rulesetInfo = ruleset.Value;
            var currentMods = mods.Value.ToArray();
            double accuracy = Accuracy.Value / 100;
            int misses = Misses.Value;
            double comboPercent = ComboPercent.Value / 100;

            performance = null;
            updateText();

            if (rulesetInfo == null || working is DummyWorkingBeatmap)
                return;

            Task.Run(async () =>
            {
                var difficulty = await difficultyCache.GetDifficultyAsync(working.BeatmapInfo, rulesetInfo, currentMods, cts.Token).ConfigureAwait(false);

                if (difficulty?.DifficultyAttributes == null || cts.IsCancellationRequested)
                    return null;

                return Calculate(working, rulesetInfo.CreateInstance(), currentMods, difficulty.Value.DifficultyAttributes, accuracy, misses, comboPercent, cts.Token);
            }, cts.Token).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && !cts.IsCancellationRequested)
                {
                    Schedule(() =>
                    {
                        if (cts != cancellationSource) return;

                        performance = t.GetResultSafely();
                        updateText();
                    });
                }
            }, TaskContinuationOptions.None);
        }

        /// <summary>
        /// Works out the pp of a pretend score with the given accuracy, misses and combo.
        /// </summary>
        public static double? Calculate(WorkingBeatmap working, Ruleset ruleset, Mod[] mods, DifficultyAttributes attributes,
                                        double accuracy, int misses, double comboPercent, CancellationToken cancellationToken = default)
        {
            var calculator = ruleset.CreatePerformanceCalculator();

            if (calculator == null)
                return null;

            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods, cancellationToken);

            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = mods;
            scoreProcessor.ApplyBeatmap(playable);

            var maximum = scoreProcessor.MaximumStatistics;
            var statistics = new Dictionary<HitResult, int>(maximum);

            int total = maximum.GetValueOrDefault(HitResult.Great);
            misses = Math.Clamp(misses, 0, total);
            int remaining = total - misses;

            if (ruleset.RulesetInfo.ShortName == "osu")
            {
                // osu! accuracy: (300 * greats + 100 * oks + 50 * mehs) / (300 * objects). Oks make up the missing accuracy.
                int oks = total == 0 ? 0 : (int)Math.Round((6.0 * remaining - 6.0 * accuracy * total) / 4);
                oks = Math.Clamp(oks, 0, remaining);

                statistics[HitResult.Great] = remaining - oks;
                statistics[HitResult.Ok] = oks;
                statistics[HitResult.Meh] = 0;
                statistics[HitResult.Miss] = misses;
            }
            else
            {
                statistics[HitResult.Great] = remaining;
                statistics[HitResult.Miss] = misses;
            }

            int maxCombo = scoreProcessor.MaximumCombo;

            var score = new ScoreInfo(working.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = accuracy,
                Mods = mods,
                MaxCombo = (int)Math.Round(maxCombo * comboPercent),
                Combo = (int)Math.Round(maxCombo * comboPercent),
                Statistics = statistics,
                MaximumStatistics = maximum,
            };

            return calculator.Calculate(score, attributes).Total;
        }

        private void updateText()
        {
            string value = performance == null ? "-" : Math.Round(performance.Value, MidpointRounding.AwayFromZero).ToString("N0");

            text.Text = Template.Value
                                .Replace("{Value}", value)
                                .Replace("{Accuracy}", Accuracy.Value.ToString("0.##"))
                                .Replace("{Misses}", Misses.Value.ToString());
        }

        protected override void SetFont(FontUsage font) => text.Font = font.With(size: 40);

        protected override void SetTextColour(Colour4 textColour) => text.Colour = textColour;

        protected override void Dispose(bool isDisposing)
        {
            cancellationSource?.Cancel();
            modSettingTracker?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
