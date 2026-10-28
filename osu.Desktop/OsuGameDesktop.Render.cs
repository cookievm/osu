// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using Oily.Rendering;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;
using osu.Game.Screens;

namespace osu.Desktop
{
    // oily: renders started from the command line (oily --render-job=job.json), for the o!ily companion app.
    internal partial class OsuGameDesktop
    {
        /// <summary>
        /// Set when the game was started to render one replay and then quit.
        /// </summary>
        public RenderJob? RenderJob { get; init; }

        private RenderJobReporter? renderReporter;

        // Gives up if the render screen hasn't appeared this long after the imports finish.
        private const int render_start_timeout_seconds = 120;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        protected override System.Collections.Generic.IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults()
        {
            var defaults = base.GetFrameworkConfigDefaults();

            if (RenderJob != null)
            {
                // Start straight in a window the size of the video, instead of fullscreen.
                defaults[FrameworkSetting.WindowMode] = Framework.Configuration.WindowMode.Windowed;
                defaults[FrameworkSetting.WindowedSize] = new System.Drawing.Size(RenderJob.Width, RenderJob.Height);
            }

            return defaults;
        }

        private void prepareRenderJob()
        {
            if (RenderJob == null)
                return;

            renderReporter = new RenderJobReporter(Console.Out);

            // This instance has its own data folder, so these don't touch the user's own settings.
            LocalConfig.SetValue(OsuSetting.ShowFirstRunSetup, false);
            LocalConfig.SetValue(OsuSetting.MenuMusic, false);
            frameworkConfig.SetValue(FrameworkSetting.WindowMode, Framework.Configuration.WindowMode.Windowed);
            frameworkConfig.SetValue(FrameworkSetting.WindowedSize, new System.Drawing.Size(RenderJob.Width, RenderJob.Height));

            // No audio in video yet; keep the render silent.
            Audio.Volume.Value = 0;
        }

        private void startRenderJob()
        {
            if (RenderJob == null)
                return;

            Task.Run(runRenderJob);
        }

        private async Task runRenderJob()
        {
            var job = RenderJob!;

            try
            {
                if (!string.IsNullOrEmpty(job.Beatmap))
                {
                    renderReporter!.Status("Importing the beatmap");
                    await BeatmapManager.Import(new ImportTask(job.Beatmap)).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(job.Skin))
                {
                    renderReporter!.Status("Importing the skin");
                    var skin = await SkinManager.Import(new ImportTask(job.Skin)).ConfigureAwait(false);
                    if (skin != null)
                        Schedule(() => SkinManager.CurrentSkinInfo.Value = skin);
                }

                renderReporter!.Status("Importing the replay");
                var scores = await ScoreManager.Import(new ProgressNotification(), new[] { new ImportTask(job.Replay) }).ConfigureAwait(false);
                var score = scores.FirstOrDefault()
                            ?? throw new InvalidOperationException("The replay couldn't be imported. Is its beatmap missing?");

                renderReporter.Status("Starting the render");
                var detached = score.PerformRead(s => s.Detach());
                Schedule(() => PresentScore(detached, ScorePresentType.Render));

                await Task.Delay(TimeSpan.FromSeconds(render_start_timeout_seconds)).ConfigureAwait(false);

                if (activeRenderScreen == null)
                    throw new TimeoutException("The render didn't start. The replay's beatmap may be missing.");
            }
            catch (Exception e)
            {
                finishRenderJob(e);
            }
        }

        private ReplayRenderScreen? activeRenderScreen;
        private bool renderJobFinished;

        private Screen createRenderJobScreen(Score score)
        {
            var job = RenderJob!;
            var screen = new ReplayRenderScreen(score, job.ToRenderSettings(), (host, _) => new FfmpegPipeCapturer(host, job));

            screen.ProgressChanged += p => renderReporter!.Progress(p);
            screen.Finished += path => finishRenderJob(null, path);
            screen.Failed += e => finishRenderJob(e);

            activeRenderScreen = screen;
            return screen;
        }

        private void finishRenderJob(Exception? error, string? outputPath = null)
        {
            lock (this)
            {
                if (renderJobFinished)
                    return;

                renderJobFinished = true;
            }

            if (error == null)
            {
                renderReporter!.Done(outputPath ?? RenderJob!.Output);
                Environment.ExitCode = 0;
            }
            else
            {
                Logger.Error(error, "Render job failed");
                renderReporter!.Error(error.Message);
                Environment.ExitCode = 1;
            }

            Schedule(() => Host.Exit());
        }
    }
}
