// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

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

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        /// <summary>
        /// Whether to show a notification with how many beatmaps have their pp worked out (song select sets this while sorting or grouping by pp).
        /// </summary>
        public bool ShowProgress { get; set; }

        /// <summary>
        /// How many beatmaps were asked for.
        /// </summary>
        public int Total => wanted.Count;

        private ProgressNotification? progressNotification;

        /// <summary>
        /// The last notification posted about working out pp, for tests.
        /// </summary>
        public Notification? LastNotification { get; private set; }
        private int notifiedGeneration = -1;
        private double lastBusyTime;

        /// <summary>
        /// Bump when o!ily's pp changes, so old saved values are thrown away.
        /// </summary>
        private const int cache_version = 2;

        private const string cache_filename = "oily-ss-pp.dat";
        private const int max_saved_values = 5_000_000;
        private const double update_interval = 2000;
        private const double save_interval = 30000;

        /// <summary>
        /// oily: the most threads the pp work can use: the computer's threads, minus 2 for the game itself.
        /// </summary>
        public static readonly int MAX_THREADS = Math.Max(1, Environment.ProcessorCount - 2);

        /// <summary>
        /// oily: how many threads work out pp (the player's choice, saved; 1 by default).
        /// </summary>
        public readonly BindableInt Threads = new BindableInt(1) { MinValue = 1, MaxValue = MAX_THREADS };

        private volatile int threadCount = 1;

        private readonly ThreadedTaskScheduler workScheduler = new ThreadedTaskScheduler(MAX_THREADS, nameof(SSPerformanceStore));

        private readonly object queueLock = new object();
        private readonly LinkedList<BeatmapInfo> queue = new LinkedList<BeatmapInfo>();
        private readonly HashSet<Guid> queued = new HashSet<Guid>();

        /// <summary>
        /// oily: the selected beatmap's difficulties. They are worked out first, even while the work is paused (see <see cref="WorkPaused"/>).
        /// </summary>
        private HashSet<Guid> urgent = new HashSet<Guid>();

        private readonly LinkedList<BeatmapInfo> urgentQueue = new LinkedList<BeatmapInfo>();

        /// <summary>
        /// oily: pauses working out pp for every beatmap (the player's choice, saved). The selected beatmap is still worked out.
        /// </summary>
        public readonly Bindable<bool> WorkPaused = new BindableBool();

        private volatile bool workPaused;

        private SavedPerformance saved = new SavedPerformance();

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

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager? config)
        {
            config?.BindWith(OsuSetting.OilyPpWorkPaused, WorkPaused);
            config?.BindWith(OsuSetting.OilyPpThreads, Threads);
            Threads.BindValueChanged(t =>
            {
                threadCount = Math.Clamp(t.NewValue, 1, MAX_THREADS);
                Schedule(() => startWorkers(current));
            }, true);
            WorkPaused.BindValueChanged(p =>
            {
                workPaused = p.NewValue;
                pendingUpdate = true;

                if (!p.NewValue)
                    Schedule(() => startWorkers(current));
            }, true);

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

        /// <summary>
        /// oily: the selected beatmap's difficulties, worked out before anything else and even while the work is paused,
        /// so their pp is always there before playing.
        /// </summary>
        public void RequestSelected(IEnumerable<BeatmapInfo> beatmaps)
        {
            var list = beatmaps.ToList();

            Schedule(() =>
            {
                lock (queueLock)
                {
                    urgent = list.Select(b => b.ID).ToHashSet();

                    // Ones already waiting move over.
                    for (var node = queue.First; node != null;)
                    {
                        var next = node.Next;

                        if (urgent.Contains(node.Value.ID))
                        {
                            queue.Remove(node);
                            urgentQueue.AddLast(node);
                        }

                        node = next;
                    }
                }

                enqueue(list, first: true);
            });
        }

        /// <summary>
        /// oily: whether this beatmap is waiting to be worked out (for tests).
        /// </summary>
        public bool IsWaiting(BeatmapInfo beatmap)
        {
            lock (queueLock)
                return queued.Contains(beatmap.ID);
        }

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

                    if (!generation.Values.ContainsKey(beatmap.ID) && saved.TryGet(generation.Key, beatmap, out double pp))
                    {
                        generation.Values[beatmap.ID] = pp;
                        foundSaved = true;
                    }

                    if (!generation.SimulatedValues.ContainsKey(beatmap.ID) && saved.TryGet(simulationGroup(generation.Key, simulationFor(beatmap)), beatmap, out double simulatedPP))
                    {
                        generation.SimulatedValues[beatmap.ID] = simulatedPP;
                        foundSaved = true;
                    }

                    if (generation.Values.ContainsKey(beatmap.ID) && generation.SimulatedValues.ContainsKey(beatmap.ID))
                        continue;

                    if (queued.Add(beatmap.ID))
                    {
                        if (urgent.Contains(beatmap.ID))
                            urgentQueue.AddLast(beatmap);
                        else if (first)
                            queue.AddFirst(beatmap);
                        else
                            queue.AddLast(beatmap);

                        Interlocked.Increment(ref remaining);
                    }
                    else if (first && !urgent.Contains(beatmap.ID))
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
                urgentQueue.Clear();
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

            string key = rulesetInfo == null ? string.Empty : modsKey(rulesetInfo, currentMods);

            return new CalculationGeneration(number, rulesetInfo, currentMods, key);
        }

        /// <summary>
        /// Mods that never change pp, left out of the saved values' keys so they share values with the same mods without them.
        /// </summary>
        private static readonly HashSet<string> mods_without_pp_effect = new HashSet<string> { "SD", "PF" };

        private static string modsKey(RulesetInfo rulesetInfo, IEnumerable<Mod> mods)
        {
            var apiMods = mods.Where(m => !mods_without_pp_effect.Contains(m.Acronym))
                              .Select(m => new APIMod(m))
                              .Select(m =>
                              {
                                  // Nightcore plays like Double Time for pp.
                                  if (m.Acronym == "NC")
                                      m.Acronym = "DT";
                                  return m;
                              })
                              .OrderBy(m => m.Acronym);

            var list = apiMods.ToList();

            // Mods on their default settings (the usual case) get a short key, like "osu|HDHRRX".
            if (list.All(m => m.Settings.Count == 0))
                return $"{rulesetInfo.ShortName}|{string.Concat(list.Select(m => m.Acronym))}";

            return $"{rulesetInfo.ShortName}|{JsonConvert.SerializeObject(list)}";
        }

        #region Working out every ranked mod combination in the background

        /// <summary>
        /// Stops all work (for example while a map is being played, so it doesn't take the CPU from gameplay).
        /// </summary>
        public bool Paused
        {
            get => pauseBackground;
            set
            {
                pauseBackground = value;

                if (!value)
                    Schedule(() => startWorkers(current));
            }
        }

        /// <summary>
        /// How many beatmap and mod combinations are still waiting to be worked out in the background.
        /// </summary>
        public int BackgroundRemaining
        {
            get
            {
                lock (queueLock)
                    return backgroundQueue.Count;
            }
        }

        private volatile bool pauseBackground;

        private readonly Queue<(BeatmapInfo Beatmap, BackgroundCombination Combination)> backgroundQueue = new Queue<(BeatmapInfo, BackgroundCombination)>();
        private RulesetInfo? backgroundRuleset;
        private int backgroundBuiltFor = -1;
        private int backgroundTotal;
        private double lastBackgroundBuild = double.MinValue;
        private bool backgroundBuilding;
        private ProgressNotification? backgroundNotification;
        private double lastBackgroundBusyTime;

        private record BackgroundCombination(Ruleset Ruleset, Mod[] Mods, string Key);

        /// <summary>
        /// Every combination of the ranked mods that change pp, most played first.
        /// </summary>
        private static List<Mod[]> rankedCombinations(Ruleset ruleset)
        {
            Mod? mod(string acronym) => ruleset.CreateModFromAcronym(acronym);

            var difficulty = new[] { null, mod("EZ"), mod("HR") };
            var speed = new[] { null, mod("DT"), mod("HT") };
            var hidden = new[] { null, mod("HD") };
            var flashlight = new[] { null, mod("FL") };
            var relax = new[] { null, mod("RX") };

            var combinations = new List<Mod[]>();

            foreach (var d in difficulty)
            foreach (var sp in speed)
            foreach (var h in hidden)
            foreach (var f in flashlight)
            foreach (var r in relax)
            {
                var set = new[] { d, sp, h, f, r }.OfType<Mod>().ToArray();

                if (set.Length > 0 && !ModUtils.CheckCompatibleSet(set))
                    continue;

                if (combinations.Any(c => c.Length == set.Length && c.All(m => set.Any(o => o.Acronym == m.Acronym))))
                    continue;

                combinations.Add(set);
            }

            // Common combinations first: Easy, Half Time and Flashlight are rarely played, so they go last.
            return combinations.OrderBy(c => c.Sum(m => m.Acronym is "EZ" or "HT" or "FL" ? 3 : 1)).ToList();
        }

        private void updateBackground()
        {
            var rulesetInfo = current.Ruleset;

            if (rulesetInfo == null || wanted.Count == 0 || backgroundBuilding)
                return;

            bool rulesetChanged = !rulesetInfo.Equals(backgroundRuleset);

            if (!rulesetChanged && (backgroundBuiltFor == wanted.Count || Time.Current - lastBackgroundBuild < 10000))
                return;

            backgroundBuilding = true;
            lastBackgroundBuild = Time.Current;

            var beatmaps = wanted.Values.ToList();

            Task.Run(() =>
            {
                var rulesetInstance = rulesetInfo.CreateInstance();
                var items = new List<(BeatmapInfo, BackgroundCombination)>();

                foreach (var combination in rankedCombinations(rulesetInstance))
                {
                    var entry = new BackgroundCombination(rulesetInstance, combination, modsKey(rulesetInfo, combination));

                    foreach (var beatmap in beatmaps)
                    {
                        if (!saved.TryGet(entry.Key, beatmap, out _))
                            items.Add((beatmap, entry));
                    }
                }

                return items;
            }).ContinueWith(t => Schedule(() =>
            {
                backgroundBuilding = false;

                if (!t.IsCompletedSuccessfully)
                    return;

                var items = t.GetResultSafely();

                lock (queueLock)
                {
                    backgroundQueue.Clear();

                    foreach (var item in items)
                        backgroundQueue.Enqueue(item);
                }

                backgroundRuleset = rulesetInfo;
                backgroundBuiltFor = beatmaps.Count;
                backgroundTotal = items.Count;

                startWorkers(current);
            }));
        }

        private void workBackground((BeatmapInfo Beatmap, BackgroundCombination Combination) item)
        {
            var (beatmap, combination) = item;
            var simulation = simulationFor(beatmap);

            double pp, simulatedPP;

            try
            {
                var working = beatmapManager.GetWorkingBeatmap(beatmap);
                var result = OilyPerformance.Calculate(combination.Ruleset, working, combination.Mods, simulation);
                pp = result.SS ?? 0;
                simulatedPP = result.Simulated ?? 0;
            }
            catch (Exception e)
            {
                Logger.Log($"Could not work out SS pp for {beatmap} with {string.Join("", combination.Mods.Select(m => m.Acronym))}: {e.Message}");
                pp = simulatedPP = 0;
            }

            saved.Set(combination.Key, beatmap, pp);
            saved.Set(simulationGroup(combination.Key, simulation), beatmap, simulatedPP);
            savedDirty = true;
        }

        private void updateBackgroundNotification()
        {
            int left = BackgroundRemaining;

            if (left > 0)
                lastBackgroundBusyTime = Time.Current;

            if (backgroundNotification?.Ongoing == true)
            {
                if (left == 0 && Time.Current - lastBackgroundBusyTime >= 1000)
                {
                    backgroundNotification.CompletionText = "Worked out pp for every ranked mod combination. Changing mods is instant now.";
                    backgroundNotification.State = ProgressNotificationState.Completed;
                    backgroundNotification = null;
                }
                else if (left > 0)
                    setBackgroundProgress(backgroundNotification, left);

                return;
            }

            backgroundNotification = null;

            if (left == 0 || notifications == null || Paused || workPaused)
                return;

            // Only once each time the list is built (closing it hides it, the work goes on).
            if (backgroundNotifiedFor == backgroundTotal + backgroundBuiltFor * 7919)
                return;

            backgroundNotifiedFor = backgroundTotal + backgroundBuiltFor * 7919;

            backgroundNotification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                CancelRequested = () => true,
            };

            setBackgroundProgress(backgroundNotification, left);
            notifications.Post(backgroundNotification);
        }

        private int backgroundNotifiedFor = -1;

        private void setBackgroundProgress(ProgressNotification notification, int left)
        {
            int done = Math.Max(0, backgroundTotal - left);
            notification.Text = $"Working out pp for every ranked mod combination (once only): {done:N0} / {backgroundTotal:N0}";
            notification.Progress = backgroundTotal == 0 ? 0 : (float)done / backgroundTotal;
        }

        #endregion

        private void startWorkers(CalculationGeneration generation)
        {
            while (true)
            {
                int active = Volatile.Read(ref activeWorkers);

                if (active >= threadCount || Paused || (Remaining == 0 && BackgroundRemaining == 0))
                    return;

                if (workPaused)
                {
                    lock (queueLock)
                    {
                        if (urgentQueue.Count == 0)
                            return;
                    }
                }

                if (Interlocked.CompareExchange(ref activeWorkers, active + 1, active) != active)
                    continue;

                Task.Factory.StartNew(() => runWorker(generation), generation.Cancellation.Token, TaskCreationOptions.HideScheduler, workScheduler)
                    .ContinueWith(t =>
                    {
                        // A worker that never ran still counted itself.
                        if (t.IsCanceled)
                            Interlocked.Decrement(ref activeWorkers);
                    });
            }
        }

        [ThreadStatic]
        private static bool stoppedForFewerThreads;

        private void runWorker(CalculationGeneration generation)
        {
            stoppedForFewerThreads = false;

            try
            {
                work(generation);
            }
            finally
            {
                // A worker stopped for fewer threads already counted itself out.
                if (!stoppedForFewerThreads)
                    Interlocked.Decrement(ref activeWorkers);
            }
        }

        private void work(CalculationGeneration generation)
        {
            var token = generation.Cancellation.Token;
            var rulesetInstance = generation.Ruleset!.CreateInstance();

            while (!token.IsCancellationRequested)
            {
                BeatmapInfo? beatmap;
                (BeatmapInfo, BackgroundCombination)? backgroundItem = null;

                lock (queueLock)
                {
                    if (generation != current || Paused)
                        return;

                    // Fewer threads chosen: extra workers stop, one at a time.
                    if (activeWorkers > threadCount)
                    {
                        Interlocked.Decrement(ref activeWorkers);
                        stoppedForFewerThreads = true;
                        return;
                    }

                    if (urgentQueue.Count > 0)
                    {
                        beatmap = urgentQueue.First!.Value;
                        urgentQueue.RemoveFirst();
                    }
                    else if (workPaused)
                        return;
                    else if (queue.Count > 0)
                    {
                        beatmap = queue.First!.Value;
                        queue.RemoveFirst();
                    }
                    else if (backgroundQueue.Count > 0)
                    {
                        beatmap = null;
                        backgroundItem = backgroundQueue.Dequeue();
                    }
                    else
                        return;
                }

                // Values for the selected mods come first; other combinations are worked out when there's nothing else to do.
                if (backgroundItem != null)
                {
                    workBackground(backgroundItem.Value);
                    continue;
                }

                Debug.Assert(beatmap != null);

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
                saved.Set(generation.Key, beatmap, pp);

                // The simulation may have changed while this was worked out: then it's worked out again.
                if (simulation == simulationFor(beatmap))
                    generation.SimulatedValues[beatmap.ID] = simulatedPP;
                saved.Set(simulationGroup(generation.Key, simulation), beatmap, simulatedPP);
                savedDirty = true;

                lock (queueLock)
                {
                    if (generation == current)
                    {
                        if (simulation != simulationFor(beatmap))
                        {
                            if (urgent.Contains(beatmap.ID))
                                urgentQueue.AddFirst(beatmap);
                            else
                                queue.AddFirst(beatmap);
                        }
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

        private static string simulationGroup(string modsKey, OilySimulation simulation)
            => $"{modsKey}|sim {simulation.Accuracy:R} {simulation.Misses} {simulation.ComboPercent:R}";

        protected override void Update()
        {
            base.Update();

            if (pendingUpdate && Time.Current - lastUpdateTime >= update_interval)
            {
                pendingUpdate = false;
                lastUpdateTime = Time.Current;
                Updated?.Invoke();
            }

            updateProgressNotification();
            updateBackground();
            updateBackgroundNotification();

            if (savedDirty && Time.Current - lastSaveTime >= save_interval)
            {
                lastSaveTime = Time.Current;
                Task.Run(writeSaved);
            }
        }

        private void updateProgressNotification()
        {
            int left = Remaining;
            int total = Total;

            // A running notification stays until everything is worked out (even if the sort is changed meanwhile), so it doesn't vanish.
            if (left > 0)
                lastBusyTime = Time.Current;

            if (progressNotification?.Ongoing == true)
            {
                // Waits a moment before finishing, as more beatmaps may be on their way (the queue can empty for a moment).
                if (left == 0 && Time.Current - lastBusyTime >= 1000)
                {
                    progressNotification.CompletionText = $"Worked out pp for all {total:N0} beatmaps.";
                    progressNotification.State = ProgressNotificationState.Completed;
                    progressNotification = null;
                }
                else if (left > 0)
                    setProgress(progressNotification, left, total, workPaused, threadCount);

                return;
            }

            progressNotification = null;

            // One notification for each set of mods, while sorting or grouping by pp.
            if (!ShowProgress || total == 0 || notifications == null || notifiedGeneration == Generation)
                return;

            notifiedGeneration = Generation;

            if (left == 0)
            {
                // Everything came straight back from the saved values.
                notifications.Post(LastNotification = new SimpleNotification
                {
                    Text = $"pp for sorting is ready: {total:N0} / {total:N0} beatmaps.",
                    Icon = FontAwesome.Solid.Check,
                });

                return;
            }

            progressNotification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                // Closing it only hides it; the pp keeps being worked out.
                CancelRequested = () => true,
            };

            setProgress(progressNotification, left, total, workPaused, threadCount);
            notifications.Post(LastNotification = progressNotification);
        }

        private static void setProgress(ProgressNotification notification, int left, int total, bool paused, int threads)
        {
            int done = Math.Max(0, total - left);
            notification.Text = paused
                ? $"pp for sorting is paused: {done:N0} / {total:N0} beatmaps (the selected map is still worked out). Resume it from song select's Options."
                : $"Working out pp for sorting: {done:N0} / {total:N0} beatmaps ({threads} {(threads == 1 ? "thread" : "threads")}, change in settings)";
            notification.Progress = total == 0 ? 0 : (float)done / total;
        }

        #region Saving to disk

        private readonly object saveLock = new object();

        private void loadSaved()
        {
            try
            {
                if (!storage.Exists(cache_filename))
                    return;

                using (var stream = storage.GetStream(cache_filename))
                    saved = SavedPerformance.Read(stream, cache_version) ?? new SavedPerformance();
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
                        saved = new SavedPerformance();

                    string temp = cache_filename + ".tmp";

                    using (var stream = storage.CreateFileSafely(temp))
                        saved.Write(stream, cache_version);

                    storage.Move(temp, cache_filename);
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not save SS pp: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Saved pp values: for each group (ruleset, mods and maybe a pretend score), each beatmap's value by its MD5 hash.
        /// Written in a small binary format, as there can be millions of them.
        /// </summary>
        private class SavedPerformance
        {
            private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, float>> groups = new ConcurrentDictionary<string, ConcurrentDictionary<string, float>>();

            private int count;

            public int Count => count;

            public bool TryGet(string group, BeatmapInfo beatmap, out double pp)
            {
                pp = 0;

                if (!groups.TryGetValue(group, out var values) || !values.TryGetValue(beatmap.MD5Hash, out float value))
                    return false;

                pp = value;
                return true;
            }

            public void Set(string group, BeatmapInfo beatmap, double pp)
            {
                var values = groups.GetOrAdd(group, _ => new ConcurrentDictionary<string, float>());

                if (values.TryAdd(beatmap.MD5Hash, (float)(double.IsFinite(pp) ? pp : 0)))
                    Interlocked.Increment(ref count);
                else
                    values[beatmap.MD5Hash] = (float)(double.IsFinite(pp) ? pp : 0);
            }

            public void Write(Stream stream, int version)
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(version);

                    var snapshot = groups.ToArray();
                    writer.Write(snapshot.Length);

                    foreach (var (group, values) in snapshot)
                    {
                        var entries = values.ToArray();

                        writer.Write(group);
                        writer.Write(entries.Length);

                        foreach (var (hash, pp) in entries)
                        {
                            writer.Write(hash);
                            writer.Write(pp);
                        }
                    }
                }
            }

            public static SavedPerformance? Read(Stream stream, int version)
            {
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != version)
                        return null;

                    var result = new SavedPerformance();
                    int groupCount = reader.ReadInt32();

                    for (int g = 0; g < groupCount; g++)
                    {
                        string group = reader.ReadString();
                        int entries = reader.ReadInt32();
                        var values = new ConcurrentDictionary<string, float>();

                        for (int e = 0; e < entries; e++)
                            values[reader.ReadString()] = reader.ReadSingle();

                        result.groups[group] = values;
                        result.count += values.Count;
                    }

                    return result;
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
