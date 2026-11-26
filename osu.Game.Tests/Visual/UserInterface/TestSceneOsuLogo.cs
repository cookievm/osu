// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Screens.Menu;
using osu.Game.Seasonal;
using osuTK;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneOsuLogo : OsuTestScene
    {
        private OsuLogo? logo;

        private float scale = 1;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            AddSliderStep("scale", 0.1, 2, 1, scale =>
            {
                if (logo != null)
                    Child.Scale = new Vector2(this.scale = (float)scale);
            });
        }

        [Test]
        public void TestBasic()
        {
            AddStep("Add logo", () =>
            {
                Child = logo = new OsuLogo
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Scale = new Vector2(scale),
                };
            });
        }

        [Test]
        public void TestOilyLogo()
        {
            AddStep("Add logo", () =>
            {
                Child = logo = new OsuLogo
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Scale = new Vector2(scale),
                };
            });

            // oily (ilo, 2026-11-26): the logo says o!ily and has hearts instead of triangles.
            AddAssert("o!ily logo texture found", () => Dependencies.Get<OilyTextures>().Get(@"logo"), () => Is.Not.Null);
            AddAssert("heart texture found", () => Dependencies.Get<OilyTextures>().Get(@"heart"), () => Is.Not.Null);
            AddUntilStep("hearts drifting", () => logo!.ChildrenOfType<OilyHearts>().Single().ChildrenOfType<Sprite>().Count(), () => Is.GreaterThan(5));
        }

        [Test]
        public void TestChristmas()
        {
            AddStep("Add logo", () =>
            {
                Child = logo = new OsuLogoChristmas
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Scale = new Vector2(scale),
                };
            });
        }

        [Test]
        public void TestHighBPM()
        {
            AddStep("Add logo", () =>
            {
                Child = new HighBPMContainer
                {
                    Child = logo = new OsuLogoChristmas
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Scale = new Vector2(scale),
                    }
                };
            });
        }

        public partial class HighBPMContainer : Container, IBeatSyncProvider
        {
            public ChannelAmplitudes CurrentAmplitudes { get; } = new ChannelAmplitudes();

            public ControlPointInfo? ControlPoints { get; } = new ControlPointInfo();

            IClock IBeatSyncProvider.Clock { get; } = new StopwatchClock(true);

            public HighBPMContainer()
            {
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                RelativeSizeAxes = Axes.Both;

                ControlPoints.Add(0, new TimingControlPoint
                {
                    BeatLength = 60000 / 10000.0,
                });
            }
        }
    }
}
