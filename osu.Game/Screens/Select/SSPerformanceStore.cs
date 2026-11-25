// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: the o!ily pp an SS gives on each beatmap, and the simulated pp (see <see cref="PerformanceSimulationStore"/>), with the selected ruleset and mods (see <see cref="OilyPerformance"/>).
    /// Used to sort and group song select by pp, and shown on the beatmap cards.
    /// </summary>
    /// <remarks>
    /// Values are worked out in the background on several threads and start again when the ruleset or mods change.
    /// They are also saved to disk, so they come back straight away next time.
    /// <see cref="Updated"/> fires every few seconds while values come in, so song select can sort again.
    /// </remarks>
    public partial class SSPerformanceStore : Component
    {
        /// <summary>
        /// Fires (on the update thread) when new values are ready.
        /// </summary>
        public event Action? Updated;

        /// <summary>
        /// How many requested beatmaps are still being worked out.
        /// </summary>
        public int Remaining => Math.Max(0, Volatile.Read(ref remaining));

        /// <summary>
        /// Whether values are still being worked out.
        /// </summary>
        public bool IsCalculating => Remaining > 0;

        /// <summary>
        /// Goes up each time the ruleset or mods change (and all values start again).
        /// </summary>
        public int Generation => current.Number;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private PerformanceSimulationStore? simulations { get; set; }

        /// <summary>
        /// Bump when o!ily's pp changes, so old saved values are thrown away.
        /// </summary>
        private const int cache_version = 1;

        private const string cache_filename = "oily-ss-pp.json";
        private const int max_saved_values = 500_000;
        private const double update_interval = 2000;
        private const double save_interval = 15000;

        private static readonly int worker_count = Math.Clamp(Environment.ProcessorCount / 2, 1, 6);

        private readonly ThreadedTaskScheduler workScheduler = new ThreadedTaskScheduler(worker_count, nameof(SSPerformanceStore));

        private readonly object queueLock = new object();
        private readonly LinkedList<BeatmapInfo> queue = new LinkedList<BeatmapInfo>();
        private readonly HashSet<Guid> queued = new HashSet<Guid>();

        private ConcurrentDictionary<string, double> saved = new ConcurrentDictionary<string, double>();

        /// <summary>
        /// Every beatmap asked for, so it can be asked for again when the mods change. Update thread only.
        /// </summary>
        private readonly Dictionary<Guid, BeatmapInfo> wanted = new Dictionary<Guid, BeatmapInfo>();

        private volatile CalculationGeneration current = new CalculationGeneration(0, null, Array.Empty<Mod>(), string.Empty);

        private ModSettingChangeTracker? modSettingTracker;
        private int remaining;
        private int activeWorkers;
        private bool pendingUpdate;
        private bool savedDirty;
        private double lastUpdateTime;
        private double lastSaveTime;

        [BackgroundDependencyLoader]
        private void load()
        {
            loadSaved();
            current = createGeneration(1);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (simulations != null)
                simulations.Changed += simulationChanged;

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
        public double? Get(BeatmapInfo beatmap) => get(current, g => g.Values, beatmap);

        /// <summary>
        /// Like <see cref="Get"/>, but while values for new mods are still being worked out, gives the value for the mods before.
        /// Used for sorting, so the list doesn't jump around while it's reorganised.
        /// </summary>
        public double? GetForSorting(BeatmapInfo beatmap) => get(current, g => g.Values, beatmap) ?? get(current.Previous, g => g.Values, beatmap);

        /// <summary>
        /// Like <see cref="GetSimulated"/>, but falls back to the value for the mods before (see <see cref="GetForSorting"/>).
        /// </summary>
        public double? GetSimulatedForSorting(BeatmapInfo beatmap)
            => get(current, g => g.SimulatedValues, beatmap) ?? get(current.Previous, g => g.SimulatedValues, beatmap);

        private static double? get(CalculationGeneration? generation, Func<CalculationGeneration, ConcurrentDictionary<Guid, double>> values, BeatmapInfo beatmap)
            => generation != null && values(generation).TryGetValue(beatmap.ID, out double pp) ? pp : null;

        /// <summary>
        /// The simulated pp of a beatmap (see <see cref="PerformanceSimulationStore"/>), or <c>null</c> if it isn't worked out yet. Safe to call from any thread.
        /// </summary>
        public double? GetSimulated(BeatmapInfo beatmap) => get(current, g => g.SimulatedValues, beatmap);

        /// <summary>
        /// Starts working out values for these beatmaps (if they are not already worked out).
        /// </summary>
        public void Request(IEnumerable<BeatmapInfo> beatmaps)
        {
            var list = beatmaps.ToList();
            Schedule(() => enqueue(list, first: false));
        }

        /// <summary>
        /// Works out this beatmap's value before anything else (for example, because its card is on screen).
        /// </summary>
        public void RequestFirst(BeatmapInfo beatmap) => Schedule(() => enqueue(new[] { beatmap }, first: true));

        private void simulationChanged(BeatmapInfo? beatmap)
        {
            if (beatmap == null)
            {
                // The default changed: work out every simulated value again (SS values come straight back from the saved ones).
                restart();
                return;
            }

            current.SimulatedValues.TryRemove(beatmap.ID, out _);
            pendingUpdate = true;
            lastUpdateTime = double.MinValue;
            enqueue(new[] { beatmap }, first: true);
        }

        private OilySimulation simulationFor(BeatmapInfo beatmap) => simulations?.Get(beatmap) ?? OilySimulation.DEFAULT;

        private void enqueue(IReadOnlyList<BeatmapInfo> beatmaps, bool first)
        {
            var generation = current;

            if (generation.Ruleset == null)
                return;

            bool foundSaved = false;

            lock (queueLock)
            {
                foreach (var beatmap in beatmaps)
                {
                    wanted[beatmap.ID] = beatmap;

                    if (!generation.Values.ContainsKey(beatmap.ID) && saved.TryGetValue(savedKey(generation, beatmap), out double pp))
                    {
                        generation.Values[beatmap.ID] = pp;
                        foundSaved = true;
                    }

                    if (!generation.SimulatedValues.ContainsKey(beatmap.ID) && saved.TryGetValue(savedKey(generation, beatmap, simulationFor(beatmap)), out double simulatedPP))
                    {
                        generation.SimulatedValues[beatmap.ID] = simulatedPP;
                        foundSaved = true;
                    }

                    if (generation.Values.ContainsKey(beatmap.ID) && generation.SimulatedValues.ContainsKey(beatmap.ID))
                        continue;

                    if (queued.Add(beatmap.ID))
                    {
                        if (first)
                            queue.AddFirst(beatmap);
                        else
                            queue.AddLast(beatmap);

                        Interlocked.Increment(ref remaining);
                    }
                    else if (first)
                    {
                        // Already waiting: move it to the front.
                        for (var node = queue.First; node != null; node = node.Next)
                        {
                            if (node.Value.ID != beatmap.ID)
                                continue;

                            queue.Remove(node);
                            queue.AddFirst(node);
                            break;
                        }
                    }
                }
            }

            if (foundSaved)
                pendingUpdate = true;

            startWorkers(generation);
        }

        private void restart()
        {
            CalculationGeneration old = current;
            old.Cancellation.Cancel();

            lock (queueLock)
            {
                queue.Clear();
                queued.Clear();
                Interlocked.Exchange(ref remaining, 0);
            }

            current = createGeneration(old.Number + 1);
            current.Previous = old;
            old.Previous = null;
            pendingUpdate = true;
            lastUpdateTime = double.MinValue;

            enqueue(wanted.Values.ToList(), first: false);
        }

        private CalculationGeneration createGeneration(int number)
        {
            var rulesetInfo = ruleset.Value;
            var currentMods = mods.Value.ToArray();

            string key = rulesetInfo == null
                ? string.Empty
                : $"{rulesetInfo.ShortName}|{JsonConvert.SerializeObject(currentMods.OrderBy(m => m.Acronym).Select(m => new APIMod(m)))}";

            return new CalculationGeneration(number, rulesetInfo, currentMods, key);
        }

        private void startWorkers(CalculationGeneration generation)
        {
            while (true)
            {
                int active = Volatile.Read(ref activeWorkers);

                if (active >= worker_count || Remaining == 0)
                    return;

                if (Interlocked.CompareExchange(ref activeWorkers, active + 1, active) != active)
                    continue;

                Task.Factory.StartNew(() => work(generation), generation.Cancellation.Token, TaskCreationOptions.HideScheduler, workScheduler)
                    .ContinueWith(_ => Interlocked.Decrement(ref activeWorkers));
            }
        }

        private void work(CalculationGeneration generation)
        {
            var token = generation.Cancellation.Token;
            var rulesetInstance = generation.Ruleset!.CreateInstance();

            while (!token.IsCancellationRequested)
            {
                BeatmapInfo? beatmap;

                lock (queueLock)
                {
                    if (generation != current || queue.Count == 0)
                        return;

                    beatmap = queue.First!.Value;
                    queue.RemoveFirst();
                }

                double pp, simulatedPP;
                var simulation = simulationFor(beatmap);

                try
                {
                    var working = beatmapManager.GetWorkingBeatmap(beatmap);
                    var result = OilyPerformance.Calculate(rulesetInstance, working, generation.Mods, simulation, null, token);
                    pp = result.SS ?? 0;
                    simulatedPP = result.Simulated ?? 0;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not work out SS pp for {beatmap}: {e.Message}");
                    pp = simulatedPP = 0;
                }

                if (token.IsCancellationRequested)
                    return;

                if (!double.IsFinite(pp))
                    pp = 0;
                if (!double.IsFinite(simulatedPP))
                    simulatedPP = 0;

                generation.Values[beatmap.ID] = pp;
                saved[savedKey(generation, beatmap)] = pp;

                // The simulation may have changed while this was worked out: then it's worked out again.
                if (simulation == simulationFor(beatmap))
                    generation.SimulatedValues[beatmap.ID] = simulatedPP;
                saved[savedKey(generation, beatmap, simulation)] = simulatedPP;
                savedDirty = true;

                lock (queueLock)
                {
                    if (generation == current)
                    {
                        if (simulation != simulationFor(beatmap))
                            queue.AddFirst(beatmap);
                        else
                        {
                            queued.Remove(beatmap.ID);
                            Interlocked.Decrement(ref remaining);
                        }
                    }
                }

                pendingUpdate = true;
            }
        }

        private static string savedKey(CalculationGeneration generation, BeatmapInfo beatmap) => $"{generation.Key}|{beatmap.MD5Hash}";

        private static string savedKey(CalculationGeneration generation, BeatmapInfo beatmap, OilySimulation simulation)
            => $"{generation.Key}|{beatmap.MD5Hash}|sim {simulation.Accuracy:R} {simulation.Misses} {simulation.ComboPercent:R}";

        protected override void Update()
        {
            base.Update();

            if (pendingUpdate && Time.Current - lastUpdateTime >= update_interval)
            {
                pendingUpdate = false;
                lastUpdateTime = Time.Current;
                Updated?.Invoke();
            }

            if (savedDirty && Time.Current - lastSaveTime >= save_interval)
            {
                lastSaveTime = Time.Current;
                Task.Run(writeSaved);
            }
        }

        #region Saving to disk

        private class SavedValues
        {
            public int Version { get; set; }

            public Dictionary<string, double> Values { get; set; } = new Dictionary<string, double>();
        }

        private readonly object saveLock = new object();

        private void loadSaved()
        {
            try
            {
                if (!storage.Exists(cache_filename))
                    return;

                using (var stream = storage.GetStream(cache_filename))
                using (var reader = new StreamReader(stream))
                {
                    var file = JsonConvert.DeserializeObject<SavedValues>(reader.ReadToEnd());

                    if (file?.Version == cache_version)
                        saved = new ConcurrentDictionary<string, double>(file.Values);
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Could not read saved SS pp: {e.Message}");
            }
        }

        private void writeSaved()
        {
            lock (saveLock)
            {
                try
                {
                    savedDirty = false;

                    if (saved.Count > max_saved_values)
                        saved.Clear();

                    string json = JsonConvert.SerializeObject(new SavedValues { Version = cache_version, Values = new Dictionary<string, double>(saved) });
                    string temp = cache_filename + ".tmp";

                    using (var stream = storage.CreateFileSafely(temp))
                    using (var writer = new StreamWriter(stream))
                        writer.Write(json);

                    storage.Move(temp, cache_filename);
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not save SS pp: {e.Message}");
                }
            }
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            current.Cancellation.Cancel();
            modSettingTracker?.Dispose();

            if (simulations != null)
                simulations.Changed -= simulationChanged;

            if (savedDirty)
                writeSaved();

            workScheduler.Dispose();
            base.Dispose(isDisposing);
        }

        private class CalculationGeneration
        {
            public readonly int Number;
            public readonly RulesetInfo? Ruleset;
            public readonly Mod[] Mods;
            public readonly string Key;
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            public readonly ConcurrentDictionary<Guid, double> Values = new ConcurrentDictionary<Guid, double>();
            public readonly ConcurrentDictionary<Guid, double> SimulatedValues = new ConcurrentDictionary<Guid, double>();

            /// <summary>
            /// The values for the mods before, while these are worked out.
            /// </summary>
            public CalculationGeneration? Previous;

            public CalculationGeneration(int number, RulesetInfo? ruleset, Mod[] mods, string key)
            {
                Number = number;
                Ruleset = ruleset;
                Mods = mods;
                Key = key;
            }
        }
    }
}
