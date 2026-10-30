// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace Oily.Rendering
{
    /// <summary>
    /// Plays a replay on the render loop's clock, with no user interaction and no replay controls on screen.
    /// </summary>
    public partial class RenderReplayPlayer : ReplayPlayer
    {
        private readonly RenderSettings settings;

        /// <summary>
        /// The gameplay clock, once the player has loaded.
        /// </summary>
        public new RenderGameplayClockContainer? GameplayClockContainer => base.GameplayClockContainer as RenderGameplayClockContainer;

        /// <summary>
        /// Gameplay time the render starts at, once the player has loaded.
        /// </summary>
        public double RenderStartTime { get; private set; }

        /// <summary>
        /// Gameplay time when the last hit object ends, once the player has loaded.
        /// </summary>
        public double LastObjectEndTime { get; private set; }

        /// <summary>
        /// The HUD (score, combo, health...), for <see cref="HudPreviewCapturer"/>.
        /// </summary>
        public HUDOverlay Hud => HUDOverlay;

        /// <summary>
        /// Hides the playfield and cursor, leaving only the HUD over the background.
        /// </summary>
        public void HideGameplay() => DrawableRuleset.Alpha = 0;

        public RenderReplayPlayer(Score score, RenderSettings settings)
            : base(score, new PlayerConfiguration
            {
                AllowPause = false,
                AllowRestart = false,
                AllowSkipping = false,
                AllowUserInteraction = false,
                ShowResults = false,
            })
        {
            this.settings = settings;
        }

        protected override GameplayClockContainer CreateGameplayClockContainer(WorkingBeatmap beatmap, double gameplayStart)
        {
            double firstObjectTime = beatmap.Beatmap.HitObjects.FirstOrDefault()?.StartTime ?? 0;

            // Never start later than the ruleset wants gameplay introduced.
            double startTime = Math.Min(gameplayStart, firstObjectTime - settings.LeadIn);

            RenderStartTime = startTime;
            LastObjectEndTime = beatmap.Beatmap.HitObjects.Count > 0 ? beatmap.Beatmap.HitObjects.Max(h => h.GetEndTime()) : firstObjectTime;

            return new RenderGameplayClockContainer(startTime, gameplayStart);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!LoadedBeatmapSuccessfully)
                return;

            // The "watching X play Y" message and playback controls don't belong in a video.
            ReplayOverlay.Alpha = 0;
        }
    }
}
