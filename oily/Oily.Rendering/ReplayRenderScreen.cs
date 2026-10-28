// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Timing;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace Oily.Rendering
{
    /// <summary>
    /// Renders a replay one frame at a time.
    /// </summary>
    /// <remarks>
    /// The replay plays inside a nested screen stack whose clock is a manual "video clock".
    /// Each step moves that clock forward by exactly one frame, waits until the frame has been drawn,
    /// hands it to the <see cref="IFrameCapturer"/>, and only then steps again.
    /// Because time is frozen between steps, how fast the computer runs never changes what ends up in the video.
    /// </remarks>
    public partial class ReplayRenderScreen : OsuScreen, IKeyBindingHandler<GlobalAction>
    {
        public override bool DisallowExternalBeatmapRulesetChanges => true;
        public override bool HideOverlaysOnEnter => true;
        public override bool CursorVisible => false;

        // Hides the on-screen back button, which would otherwise end up in every frame.
        // Escape still cancels the render, see OnPressed.
        public override bool AllowUserExit => false;

        protected override OverlayActivation InitialOverlayActivationMode => OverlayActivation.Disabled;

        public readonly Score Score;
        public readonly RenderSettings Settings;

        /// <summary>
        /// The player doing the rendering, once pushed.
        /// </summary>
        public RenderReplayPlayer Player { get; private set; } = null!;

        /// <summary>
        /// How many frames have been captured so far.
        /// </summary>
        public int FramesCaptured { get; private set; }

        /// <summary>
        /// Whether all frames have been captured.
        /// </summary>
        public bool Completed => state == RenderState.Done;

        /// <summary>
        /// Rough progress from 0 to 1, based on gameplay time against the end of the last hit object.
        /// </summary>
        public event Action<double>? ProgressChanged;

        /// <summary>
        /// Fired once when the video is written. The argument says where it is.
        /// </summary>
        public event Action<string>? Finished;

        /// <summary>
        /// Fired once if the render fails or is cancelled.
        /// </summary>
        public event Action<Exception>? Failed;

        // The frame on which the play ended or failed, after which only the tail is rendered.
        private int? endedAtFrame;
        private int lastReportedPercent = -1;

        private readonly Func<GameHost, Storage, IFrameCapturer>? createCapturer;
        private IFrameCapturer capturer = null!;

        private readonly ManualClock videoClock = new ManualClock { Rate = 1, IsRunning = true };

        private OsuScreenStack playerStack = null!;

        private const int max_preroll_seconds = 10;

        private RenderState state;

        // Frames stepped before gameplay started. These are not captured.
        private int prerollFrames;
        private Task? pendingCapture;

        // Draw thread clock time seen after the last step, used to wait until the stepped frame has been drawn.
        private double lastDrawTime;
        private int drawFramesSinceStep;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        /// <param name="score">The score whose replay is rendered.</param>
        /// <param name="settings">Render settings.</param>
        /// <param name="createCapturer">Creates the capturer. Defaults to <see cref="PngSequenceCapturer"/>.</param>
        public ReplayRenderScreen(Score score, RenderSettings? settings = null, Func<GameHost, Storage, IFrameCapturer>? createCapturer = null)
        {
            if (score.Replay == null)
                throw new ArgumentException($"{nameof(score)} must have a non-null {nameof(score.Replay)}.", nameof(score));

            Score = score;
            Settings = settings ?? new RenderSettings();
            this.createCapturer = createCapturer;
        }

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            capturer = createCapturer?.Invoke(host, storage.GetStorageForDirectory(@"renders"))
                       ?? new PngSequenceCapturer(host, storage.GetStorageForDirectory(@"renders"));

            InternalChild = new NoInputContainer
            {
                RelativeSizeAxes = Axes.Both,
                // Everything inside, including the player's transitions, HUD and background, runs on video time.
                Clock = new FramedClock(videoClock),
                Child = playerStack = new OsuScreenStack { RelativeSizeAxes = Axes.Both },
            };
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            // These are reverted when this screen exits, thanks to the lease.
            Ruleset.Value = Score.ScoreInfo.Ruleset;
            Mods.Value = Score.ScoreInfo.Mods;

            base.OnEntering(e);

            // Phase 0 has no audio; stop the song select preview so it doesn't play over the render.
            musicController.Stop();

            playerStack.Push(Player = new RenderReplayPlayer(Score, Settings));

            Logger.Log($"Replay render started: {Settings.FramesPerSecond} fps, output to {capturer.OutputDescription}");
        }

        protected override void Update()
        {
            base.Update();

            switch (state)
            {
                case RenderState.WaitingForGameplay:
                    if (!Player.IsCurrentScreen())
                        break;

                    if (Player.GameplayClockContainer?.IsRunning == true)
                    {
                        markStepped();
                        break;
                    }

                    // The player's entry transition runs on video time, and gameplay only starts once it is under way.
                    // Step video time without capturing until it does.
                    if (++prerollFrames > Settings.FramesPerSecond * max_preroll_seconds)
                    {
                        fail(new TimeoutException($"Gameplay did not start within {max_preroll_seconds} seconds of video time."));
                        break;
                    }

                    videoClock.CurrentTime = Settings.FrameTime(prerollFrames);
                    break;

                case RenderState.WaitingForDraw:
                    if (frameDrawn())
                    {
                        var frame = new RenderedFrame(FramesCaptured, Settings.FrameTime(FramesCaptured), Player.GameplayClockContainer!.CurrentTime);
                        pendingCapture = capturer.CaptureFrameAsync(frame);
                        state = RenderState.Capturing;
                    }

                    break;

                case RenderState.Capturing:
                    if (!pendingCapture!.IsCompleted)
                        break;

                    if (pendingCapture.IsFaulted)
                    {
                        fail(pendingCapture.Exception!);
                        break;
                    }

                    FramesCaptured++;
                    reportProgress();

                    if (endedAtFrame == null && (Player.GameplayState.HasCompleted || Player.GameplayState.HasFailed))
                        endedAtFrame = FramesCaptured;

                    bool tailDone = endedAtFrame is int ended && (FramesCaptured - ended) * Settings.FrameDuration >= Settings.Tail;

                    if ((Settings.FrameCount is int count && FramesCaptured >= count) || tailDone)
                    {
                        complete();
                        break;
                    }

                    // Step to the next frame.
                    videoClock.CurrentTime = Settings.FrameTime(prerollFrames + FramesCaptured);
                    markStepped();
                    break;
            }
        }

        private void markStepped()
        {
            lastDrawTime = host.DrawThread.Clock.CurrentTime;
            drawFramesSinceStep = 0;
            state = RenderState.WaitingForDraw;
        }

        /// <summary>
        /// Whether the draw thread has finished at least one whole frame since the last step,
        /// so a screenshot now shows the stepped frame.
        /// </summary>
        private bool frameDrawn()
        {
            double drawTime = host.DrawThread.Clock.CurrentTime;

            if (drawTime != lastDrawTime)
            {
                lastDrawTime = drawTime;
                drawFramesSinceStep++;
            }

            // The first draw seen may have started before the step's update finished.
            return drawFramesSinceStep >= 2;
        }

        private void reportProgress()
        {
            double progress;

            if (Settings.FrameCount is int count)
                progress = (double)FramesCaptured / count;
            else
            {
                double span = Player.LastObjectEndTime - Player.RenderStartTime;
                progress = span <= 0 ? 0 : (Player.GameplayClockContainer!.CurrentTime - Player.RenderStartTime) / span;
            }

            progress = Math.Clamp(progress, 0, 1);
            int percent = (int)(progress * 100);

            if (percent != lastReportedPercent)
            {
                lastReportedPercent = percent;
                ProgressChanged?.Invoke(progress);
            }
        }

        private void complete()
        {
            state = RenderState.Done;

            try
            {
                capturer.Complete();
            }
            catch (Exception e)
            {
                fail(e);
                return;
            }

            Finished?.Invoke(capturer.OutputDescription);

            Logger.Log($"Replay render finished: {FramesCaptured} frames to {capturer.OutputDescription}");

            notifications?.Post(new SimpleNotification
            {
                Text = $"Rendered {FramesCaptured} frames. Click to open the folder.",
                Icon = FontAwesome.Solid.Film,
                Activated = () =>
                {
                    (capturer as PngSequenceCapturer)?.Open();
                    return true;
                }
            });

            this.Exit();
        }

        private void fail(Exception exception)
        {
            state = RenderState.Failed;
            Logger.Error(exception, "Replay render failed");
            Failed?.Invoke(exception);
            this.Exit();
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action != GlobalAction.Back || e.Repeat)
                return false;

            Logger.Log($"Replay render cancelled after {FramesCaptured} frames");
            state = RenderState.Failed;
            Failed?.Invoke(new OperationCanceledException("The render was cancelled."));
            this.Exit();
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            // Make sure the nested player cleans up (samples, leases) before this screen goes.
            if (playerStack.CurrentScreen is Player player)
                player.Exit();

            return base.OnExiting(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            capturer?.Dispose();
        }

        /// <summary>
        /// Keeps keyboard and mouse input away from the player, so nothing the user does can change the render.
        /// The replay drives gameplay through its own input handler, which doesn't depend on this.
        /// </summary>
        private partial class NoInputContainer : Container
        {
            public override bool PropagatePositionalInputSubTree => false;
            public override bool PropagateNonPositionalInputSubTree => false;
        }

        private enum RenderState
        {
            WaitingForGameplay,
            WaitingForDraw,
            Capturing,
            Done,

            /// <summary>
            /// Failed or cancelled. Nothing more happens.
            /// </summary>
            Failed,
        }
    }
}
