// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// oily: the hit policy o!ily uses for osu! standard gameplay.
    /// When <see cref="Enabled"/>, hits follow stable's notelock (<see cref="LegacyHitPolicy"/>, over the full hit window),
    /// and slider heads that were already hit keep blocking objects underneath them until the slider is fully judged.
    /// When not enabled (turned off in the Gamerules settings, or a replay from official lazer), lazer's <see cref="StartTimeOrderedHitPolicy"/> is used.
    /// </summary>
    public class StableNoteLockHitPolicy : IHitPolicy
    {
        /// <summary>
        /// Whether stable notelock applies.
        /// </summary>
        public bool Enabled { get; init; } = true;

        private readonly LegacyHitPolicy stable;
        private readonly StartTimeOrderedHitPolicy lazer = new StartTimeOrderedHitPolicy();

        private IHitObjectContainer? hitObjectContainer;

        public StableNoteLockHitPolicy(double hittableRange = OsuHitWindows.MISS_WINDOW)
        {
            stable = new LegacyHitPolicy(hittableRange);
        }

        public IHitObjectContainer? HitObjectContainer
        {
            get => hitObjectContainer;
            set
            {
                hitObjectContainer = value;
                stable.HitObjectContainer = value;
                lazer.HitObjectContainer = value;
            }
        }

        private IHitPolicy current => Enabled ? stable : lazer;

        public ClickAction CheckHittable(DrawableHitObject hitObject, double time, HitResult result) => current.CheckHittable(hitObject, time, result);

        public void HandleHit(DrawableHitObject hitObject) => current.HandleHit(hitObject);
    }
}
