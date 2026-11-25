// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: the pp an SS gives on each beatmap, with the selected ruleset and mods. Used to sort and group song select by pp.
    /// </summary>
    /// <remarks>
    /// Values are worked out in the background (using <see cref="BeatmapDifficultyCache"/>) and start again when the ruleset or mods change.
    /// <see cref="Updated"/> fires every few seconds while values come in, so song select can sort again.
    /// </remarks>
    public partial class SSPerformanceStore : Component
    {
        /// <summary>
        /// Fires (on the update thread) when new values are ready.
        /// </summary>
        public event Action? Updated;

        /// <summary>
        /// Whether values are still being worked out.
        /// </summary>
        public bool IsCalculating { get; private set; }

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        private ConcurrentDictionary<Guid, double> values = new ConcurrentDictionary<Guid, double>();

        private CancellationTokenSource? cancellationSource;
        private ModSettingChangeTracker? modSettingTracker;
        private IReadOnlyList<BeatmapInfo> wanted = Array.Empty<BeatmapInfo>();
        private bool pendingUpdate;
        private double lastUpdateTime;

        private const double update_interval = 2000;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => Scheduler.AddOnce(restart));
            mods.BindValueChanged(m =>
            {
                modSettingTracker?.Dispose();
                modSettingTracker = new ModSettingChangeTracker(m.NewValue)
                {
                    SettingChanged = _ => Scheduler.AddOnce(restart)
                };

                Scheduler.AddOnce(restart);
            });
        }

        /// <summary>
        /// The SS pp of a beatmap, or <c>null</c> if it isn't worked out yet. Safe to call from any thread.
        /// </summary>
        public double? Get(BeatmapInfo beatmap) => values.TryGetValue(beatmap.ID, out double pp) ? pp : null;

        /// <summary>
        /// Starts working out values for these beatmaps (if they are not already being worked out).
        /// </summary>
        public void Request(IEnumerable<BeatmapInfo> beatmaps)
        {
            var list = beatmaps.ToList();

            Schedule(() =>
            {
                if (list.Count == wanted.Count && list.All(b => values.ContainsKey(b.ID)))
                    return;

                wanted = list;
                restart(keepValues: true);
            });
        }

        private void restart() => restart(keepValues: false);

        private void restart(bool keepValues)
        {
            cancellationSource?.Cancel();
            var cts = cancellationSource = new CancellationTokenSource();

            if (!keepValues)
            {
                values = new ConcurrentDictionary<Guid, double>();
                pendingUpdate = true;
            }

            var currentValues = values;
            var rulesetInfo = ruleset.Value;
            var currentMods = mods.Value.ToArray();
            var beatmaps = wanted.Where(b => !currentValues.ContainsKey(b.ID)).ToList();

            if (rulesetInfo == null || beatmaps.Count == 0)
                return;

            IsCalculating = true;

            Task.Run(async () =>
            {
                foreach (var beatmap in beatmaps)
                {
                    if (cts.IsCancellationRequested)
                        return;

                    try
                    {
                        var difficulty = await difficultyCache.GetDifficultyAsync(beatmap, rulesetInfo, currentMods, cts.Token).ConfigureAwait(false);
                        currentValues[beatmap.ID] = difficulty?.PerformanceAttributes?.Total ?? 0;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"Could not work out SS pp for {beatmap}: {e.Message}");
                        currentValues[beatmap.ID] = 0;
                    }

                    pendingUpdate = true;
                }
            }, cts.Token).ContinueWith(_ => Schedule(() =>
            {
                if (cts != cancellationSource)
                    return;

                IsCalculating = false;
                pendingUpdate = true;
                lastUpdateTime = double.MinValue;
            }));
        }

        protected override void Update()
        {
            base.Update();

            if (pendingUpdate && Time.Current - lastUpdateTime >= update_interval)
            {
                pendingUpdate = false;
                lastUpdateTime = Time.Current;
                Updated?.Invoke();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellationSource?.Cancel();
            modSettingTracker?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
