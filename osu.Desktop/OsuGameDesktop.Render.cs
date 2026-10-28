// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Oily.Rendering;
using Oily.Rendering.Audio;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
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
                defaults[FrameworkSetting.FrameSync] = FrameSync.Unlimited;
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

            // Frames are drawn off-screen at the job's size, so the window only needs the right shape.
            var windowSize = RenderJob.WindowSizeFor(Host.Window?.CurrentDisplayMode.Value.Size ?? System.Drawing.Size.Empty);
            frameworkConfig.SetValue(FrameworkSetting.WindowedSize, windowSize);
            Logger.Log($"Render job: {RenderJob.Width}x{RenderJob.Height} video from a {windowSize.Width}x{windowSize.Height} window, renderer {Host.Renderer.GetType().Name}");

            // Each video frame waits for two drawn frames, so any frame rate cap slows the render down.
            // Without these, VSync holds it to half the screen's refresh rate, and a window in the background
            // (behind Discord, a browser...) drops to 60 draws a second, so 30 video frames a second at most.
            Host.AllowBenchmarkUnlimitedFrames = true;
            Host.MaximumInactiveHz = 0;
            frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Unlimited);

            // The video's audio is mixed afterwards from what the render recorded, so the window itself stays silent.
            Audio.Volume.Value = 0;
        }

        /// <summary>
        /// Keeps the window from ever appearing. Called before the first frame is drawn.
        /// </summary>
        /// <remarks>
        /// The framework creates the window hidden and shows it on the first draw. It has no public switch for that, so the flag is turned off by reflection.
        /// The window keeps drawing while hidden; only minimising stops it.
        /// </remarks>
        private void hideRenderWindow(GameHost host)
        {
            if (RenderJob?.Hidden != true || host.Window == null)
                return;

            for (var type = host.Window.GetType(); type != null; type = type.BaseType)
            {
                var firstDraw = type.GetField("firstDraw", BindingFlags.Instance | BindingFlags.NonPublic);

                if (firstDraw?.FieldType == typeof(bool))
                {
                    firstDraw.SetValue(host.Window, false);
                    Logger.Log("Render job: window hidden");
                    return;
                }
            }

            Logger.Log("Render job: couldn't hide the window, it will stay visible", LoggingTarget.Runtime, LogLevel.Important);
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
                    await BeatmapManager.Import(new ImportTask(copyForImport(job.Beatmap))).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(job.Skin))
                {
                    renderReporter!.Status("Importing the skin");
                    var skin = await SkinManager.Import(new ImportTask(copyForImport(job.Skin))).ConfigureAwait(false);
                    if (skin != null)
                        Schedule(() => SkinManager.CurrentSkinInfo.Value = skin);
                }

                renderReporter!.Status("Importing the replay");
                var scores = await ScoreManager.Import(new ProgressNotification(), new[] { new ImportTask(copyForImport(job.Replay)) }).ConfigureAwait(false);
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

        /// <summary>
        /// lazer deletes .osk and .osz files once it has imported them. The job's files belong to the user, so import a copy.
        /// Folders are never deleted and are imported as they are.
        /// </summary>
        private string copyForImport(string path)
        {
            if (!File.Exists(path))
                return path;

            string folder = Path.Combine(Path.GetTempPath(), "oily-render", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            temporaryFolders.Add(folder);

            string copy = Path.Combine(folder, Path.GetFileName(path));
            File.Copy(path, copy);
            return copy;
        }

        private readonly List<string> temporaryFolders = new List<string>();

        private void deleteTemporaryFolders()
        {
            foreach (string folder in temporaryFolders)
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (Exception e)
                {
                    Logger.Log($"Couldn't delete {folder}: {e.Message}");
                }
            }
        }

        private ReplayRenderScreen? activeRenderScreen;
        private bool renderJobFinished;

        private Screen createRenderJobScreen(Score score)
        {
            var job = RenderJob!;
            ReplayRenderScreen screen = null!;
            screen = new ReplayRenderScreen(score, job.ToRenderSettings(), (_, _) => new FfmpegPipeCapturer(screen.GrabFrameAsync, job));

            screen.ProgressChanged += p => renderReporter!.Progress(p);
            screen.Finished += path =>
            {
                var beatmap = screen.Beatmap.Value;
                double length = screen.FramesCaptured * screen.Settings.FrameDuration;

                Task.Run(() =>
                {
                    if (screen.Audio != null)
                        addAudio(screen.Audio, beatmap, length, path);

                    finishRenderJob(null, path);
                });
            };
            screen.Failed += e => finishRenderJob(e);

            activeRenderScreen = screen;
            return screen;
        }

        /// <summary>
        /// Mixes the recorded hitsounds with the beatmap's music and puts them into the video.
        /// If that fails, the video stays as it is, without sound.
        /// </summary>
        private void addAudio(RenderAudioRecorder audio, WorkingBeatmap beatmap, double lengthMs, string video)
        {
            var job = RenderJob!;
            string folder = Path.Combine(Path.GetTempPath(), "oily-render", Guid.NewGuid().ToString("N"));
            temporaryFolders.Add(folder);
            Directory.CreateDirectory(folder);

            string silentVideo = Path.Combine(folder, "video" + Path.GetExtension(video));

            try
            {
                renderReporter!.Status("Adding the music and hitsounds");
                var stopwatch = Stopwatch.StartNew();

                string hitsounds = Path.Combine(folder, "hitsounds.wav");
                using (var file = File.Create(hitsounds))
                    AudioMixdown.WriteWav(file, audio.Mixdown.Mix(lengthMs));

                var music = audio.ExtractMusic(beatmap, folder);

                File.Move(video, silentVideo);

                var startInfo = new ProcessStartInfo(job.Ffmpeg)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                foreach (string arg in RenderAudioMuxer.BuildArguments(silentVideo, hitsounds, music, video))
                    startInfo.ArgumentList.Add(arg);

                Logger.Log($"Adding audio: {job.Ffmpeg} {string.Join(' ', startInfo.ArgumentList)}");

                using var ffmpeg = Process.Start(startInfo) ?? throw new InvalidOperationException("ffmpeg didn't start.");

                var errors = new System.Text.StringBuilder();
                ffmpeg.ErrorDataReceived += (_, e) => errors.AppendLine(e.Data);
                ffmpeg.OutputDataReceived += (_, _) => { };
                ffmpeg.BeginErrorReadLine();
                ffmpeg.BeginOutputReadLine();
                ffmpeg.WaitForExit();

                if (ffmpeg.ExitCode != 0)
                    throw new InvalidOperationException($"ffmpeg failed with code {ffmpeg.ExitCode}: {errors}");

                int openLoops = audio.Mixdown.Plays.Count(p => p.Looping && p.EndMs == null);
                Logger.Log($"Added audio: {audio.Mixdown.Plays.Count} samples ({openLoops} loops never stopped) in {stopwatch.ElapsedMilliseconds} ms, music {(music == null ? "none" : $"from {music.StartTime:N0} ms at {music.Rate}x")}");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Couldn't add audio to the video");
                renderReporter!.Status($"The video has no sound: {e.Message}");

                if (File.Exists(silentVideo))
                {
                    if (File.Exists(video))
                        File.Delete(video);

                    File.Move(silentVideo, video);
                }
            }
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

            deleteTemporaryFolders();
            Schedule(() => Host.Exit());
        }
    }
}
