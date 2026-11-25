// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: the pretend score used for simulated pp. Each beatmap can have its own (set by right-clicking it in song select),
    /// which stays until it is changed. Beatmaps without their own use the default from the settings.
    /// </summary>
    public partial class PerformanceSimulationStore : Component
    {
        /// <summary>
        /// Fires (on the update thread) when a simulation changes: the beatmap whose own one changed, or <c>null</c> when the default changed.
        /// </summary>
        public event Action<BeatmapInfo?>? Changed;

        public readonly BindableDouble DefaultAccuracy = new BindableDouble();
        public readonly BindableInt DefaultMisses = new BindableInt();
        public readonly BindableDouble DefaultComboPercent = new BindableDouble();

        [Resolved]
        private Storage storage { get; set; } = null!;

        private const string filename = "oily-pp-simulations.json";

        private readonly ConcurrentDictionary<Guid, OilySimulation> own = new ConcurrentDictionary<Guid, OilySimulation>();

        private volatile OilySimulation defaultSimulation = OilySimulation.DEFAULT;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager? config)
        {
            if (config != null)
            {
                config.BindWith(OsuSetting.OilySimulationAccuracy, DefaultAccuracy);
                config.BindWith(OsuSetting.OilySimulationMisses, DefaultMisses);
                config.BindWith(OsuSetting.OilySimulationComboPercent, DefaultComboPercent);
            }
            else
            {
                DefaultAccuracy.Value = OilySimulation.DEFAULT.Accuracy;
                DefaultMisses.Value = OilySimulation.DEFAULT.Misses;
                DefaultComboPercent.Value = OilySimulation.DEFAULT.ComboPercent;
            }

            defaultSimulation = currentDefault();

            try
            {
                if (storage.Exists(filename))
                {
                    using (var stream = storage.GetStream(filename))
                    using (var reader = new StreamReader(stream))
                    {
                        var values = JsonConvert.DeserializeObject<Dictionary<Guid, OilySimulation>>(reader.ReadToEnd());

                        if (values != null)
                        {
                            foreach (var (id, simulation) in values)
                                own[id] = simulation;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Could not read saved pp simulations: {e.Message}");
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            DefaultAccuracy.BindValueChanged(_ => Scheduler.AddOnce(defaultChanged));
            DefaultMisses.BindValueChanged(_ => Scheduler.AddOnce(defaultChanged));
            DefaultComboPercent.BindValueChanged(_ => Scheduler.AddOnce(defaultChanged));
        }

        /// <summary>
        /// The default simulation, for beatmaps without their own. Safe to call from any thread.
        /// </summary>
        public OilySimulation Default => defaultSimulation;

        /// <summary>
        /// The simulation for a beatmap: its own, or the default. Safe to call from any thread.
        /// </summary>
        public OilySimulation Get(BeatmapInfo beatmap) => own.TryGetValue(beatmap.ID, out var simulation) ? simulation : defaultSimulation;

        /// <summary>
        /// Whether a beatmap has its own simulation.
        /// </summary>
        public bool HasOwn(BeatmapInfo beatmap) => own.ContainsKey(beatmap.ID);

        /// <summary>
        /// Gives a beatmap its own simulation, or (with <c>null</c>) makes it use the default again.
        /// </summary>
        public void Set(BeatmapInfo beatmap, OilySimulation? simulation)
        {
            if (simulation == null)
                own.TryRemove(beatmap.ID, out _);
            else
                own[beatmap.ID] = simulation;

            save();
            Changed?.Invoke(beatmap);
        }

        /// <summary>
        /// Changes the default simulation.
        /// </summary>
        public void SetDefault(OilySimulation simulation)
        {
            DefaultAccuracy.Value = simulation.Accuracy;
            DefaultMisses.Value = simulation.Misses;
            DefaultComboPercent.Value = simulation.ComboPercent;
        }

        private void defaultChanged()
        {
            var simulation = currentDefault();

            if (simulation == defaultSimulation)
                return;

            defaultSimulation = simulation;
            Changed?.Invoke(null);
        }

        private OilySimulation currentDefault() => new OilySimulation(DefaultAccuracy.Value, DefaultMisses.Value, DefaultComboPercent.Value);

        private void save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(new Dictionary<Guid, OilySimulation>(own));
                string temp = filename + ".tmp";

                using (var stream = storage.CreateFileSafely(temp))
                using (var writer = new StreamWriter(stream))
                    writer.Write(json);

                storage.Move(temp, filename);
            }
            catch (Exception e)
            {
                Logger.Log($"Could not save pp simulations: {e.Message}");
            }
        }
    }
}
