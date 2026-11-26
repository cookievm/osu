// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osuTK;

namespace osu.Game.Graphics.Backgrounds
{
    /// <summary>
    /// oily: soft hearts drifting upwards, used in place of lazer's triangles behind the o!ily logo.
    /// </summary>
    public partial class OilyHearts : CompositeDrawable
    {
        /// <summary>
        /// How fast the hearts rise (same scale as <see cref="TrianglesV2.Velocity"/>).
        /// </summary>
        public float Velocity = 1;

        private const float base_velocity = 50;

        /// <summary>
        /// How many hearts there are for the area.
        /// </summary>
        public int Count { get; init; } = 14;

        private Texture? heartTexture;

        public OilyHearts()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [Resolved(canBeNull: true)]
        private OilyTextures? oilyTextures { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            heartTexture = oilyTextures?.Get(@"heart");

            for (int i = 0; i < Count; i++)
                AddInternal(createHeart(RNG.NextSingle()));
        }

        private Heart createHeart(float y)
        {
            float size = 0.1f + RNG.NextSingle() * 0.22f;

            return new Heart
            {
                Texture = heartTexture,
                RelativeSizeAxes = Axes.Both,
                RelativePositionAxes = Axes.Both,
                FillMode = FillMode.Fit,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Size = new Vector2(size),
                Rotation = RNG.NextSingle(-14, 14),
                Position = new Vector2(RNG.NextSingle(), y),
                SpeedMultiplier = 0.5f + size * 3,
            };
        }

        protected override void Update()
        {
            base.Update();

            if (DrawHeight <= 0)
                return;

            float moved = (float)Time.Elapsed / 1000 * Velocity * base_velocity / DrawHeight;

            foreach (var heart in InternalChildren.OfType<Heart>().ToArray())
            {
                heart.Y -= Math.Max(0.5f, heart.SpeedMultiplier) * moved;

                // Gone off the top: start again just under the bottom.
                if (heart.Y < -heart.Height)
                {
                    RemoveInternal(heart, true);
                    AddInternal(createHeart(1 + 0.25f));
                }
            }
        }

        private partial class Heart : Sprite
        {
            public float SpeedMultiplier;
        }
    }
}
