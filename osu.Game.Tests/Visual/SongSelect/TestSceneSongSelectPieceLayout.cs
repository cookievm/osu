// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Testing;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// oily: skin components that move, resize, hide and colour the pieces of song select.
    /// </summary>
    public partial class TestSceneSongSelectPieceLayout : SongSelectTestScene
    {
        private SongSelectBeatmapListLayout layout = null!;

        private SkinnableContainer skinLayer => SongSelect.ChildrenOfType<SkinnableContainer>().First(c => c.Lookup.Lookup == GlobalSkinnableContainers.SongSelect);

        private SongSelectPieceContainer listPiece => SongSelect.ChildrenOfType<SongSelectPieceContainer>().Single(p => p.Piece == SongSelectPiece.BeatmapList);

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            ImportBeatmapForRuleset(0);
            LoadSongSelect();
        }

        [Test]
        public void TestNewComponentSnapsToPiece()
        {
            Quad natural = default;

            AddStep("remember list position", () => natural = Carousel.ScreenSpaceDrawQuad);
            addLikeSkinEditor();

            AddAssert("component covers the list", () => Vector2.Distance(layout.ScreenSpaceDrawQuad.TopLeft, listPiece.NaturalScreenSpaceQuad.TopLeft), () => Is.LessThan(1));
            AddAssert("list did not move", () => Vector2.Distance(Carousel.ScreenSpaceDrawQuad.TopLeft, natural.TopLeft), () => Is.LessThan(1));
        }

        [Test]
        public void TestMoveScaleAndStyle()
        {
            Quad natural = default;

            AddStep("remember list position", () => natural = Carousel.ScreenSpaceDrawQuad);
            addLikeSkinEditor();

            AddStep("move left and shrink", () =>
            {
                layout.X -= 200;
                layout.Scale = new Vector2(0.5f);
            });

            AddAssert("list moved left", () => Carousel.ScreenSpaceDrawQuad.TopLeft.X, () => Is.LessThan(natural.TopLeft.X - 50));
            AddAssert("list is half size", () => Carousel.ScreenSpaceDrawQuad.Width / natural.Width, () => Is.EqualTo(0.5f).Within(0.02f));

            AddStep("tint and fade", () =>
            {
                layout.Tint.Value = Color4.Red;
                layout.PieceOpacity.Value = 0.5f;
            });
            AddAssert("list tinted", () => listPiece.Child.Colour.TopLeft.Linear.G, () => Is.LessThan(0.1f));
            AddAssert("list faded", () => listPiece.Child.Alpha, () => Is.EqualTo(0.5f));

            AddStep("hide", () => layout.Hidden.Value = true);
            AddAssert("list hidden", () => listPiece.Child.Alpha, () => Is.Zero);

            AddStep("remove component", () => skinLayer.Remove(layout, true));
            AddAssert("list back in place", () => Vector2.Distance(Carousel.ScreenSpaceDrawQuad.TopLeft, natural.TopLeft), () => Is.LessThan(1));
            AddAssert("list visible", () => listPiece.Child.Alpha, () => Is.EqualTo(1));
        }

        private void addLikeSkinEditor()
        {
            AddStep("add list layout like the skin editor", () =>
            {
                layout = new SongSelectBeatmapListLayout
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = skinLayer.DrawSize.Y / 2,
                };
                skinLayer.Add(layout);
            });
            AddUntilStep("wait for layout", () => layout.IsLoaded);
            AddWaitStep("let it settle", 2);
        }
    }
}
