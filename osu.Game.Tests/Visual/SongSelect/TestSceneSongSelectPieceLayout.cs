// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Difficulty;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning.Components;
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

        [Test]
        public void TestOnlySongSelectComponentsOffered()
        {
            AddAssert("gameplay combo counter not offered", () => SongSelectSkinComponents.IsAllowed(typeof(ArgonComboCounter)), () => Is.False);
            AddAssert("health bar not offered", () => SongSelectSkinComponents.IsAllowed(typeof(ArgonHealthDisplay)), () => Is.False);
            AddAssert("beatmap attribute text offered", () => SongSelectSkinComponents.IsAllowed(typeof(BeatmapAttributeText)), () => Is.True);
            AddAssert("simulated pp offered", () => SongSelectSkinComponents.IsAllowed(typeof(SimulatedPerformanceText)), () => Is.True);
            AddAssert("song select pieces offered", () => SongSelectSkinComponents.IsAllowed(typeof(SongSelectDifficultyStatsLayout)), () => Is.True);
        }

        [Test]
        public void TestDetailPiecesRegistered()
        {
            foreach (var piece in System.Enum.GetValues<SongSelectPiece>())
                AddAssert($"{piece} can be skinned", () => SongSelect.ChildrenOfType<SongSelectPieceContainer>().Any(p => p.Piece == piece));
        }

        [Test]
        public void TestCardStyle()
        {
            addLikeSkinEditor();

            AddUntilStep("cards shown", () => Carousel.ChildrenOfType<Panel>().Any(p => p.Item != null));

            AddStep("flip, narrow and tilt cards", () =>
            {
                layout.FlipCards.Value = true;
                layout.CardWidth.Value = 0.6f;
                layout.CardRotation.Value = 5;
            });
            AddUntilStep("cards flipped", () => Carousel.ChildrenOfType<Panel>().Where(p => p.Item != null).All(p => p.Anchor == Anchor.TopLeft));
            AddUntilStep("cards narrow", () => Carousel.ChildrenOfType<Panel>().Where(p => p.Item != null).All(p => p.Width == 0.6f));
            AddUntilStep("cards tilted", () => Carousel.ChildrenOfType<Panel>().Where(p => p.Item != null).All(p => p.Rotation == 5));

            float heightBefore = 0;
            AddStep("remember card height", () => heightBefore = Carousel.ChildrenOfType<PanelBeatmapSet>().First(p => p.Item != null).DrawHeight);
            AddStep("taller cards", () => layout.CardHeight.Value = 1.5f);
            AddUntilStep("cards taller", () => Carousel.ChildrenOfType<PanelBeatmapSet>().First(p => p.Item != null).DrawHeight, () => Is.EqualTo(heightBefore * 1.5f).Within(0.5f));
            AddUntilStep("carousel spaced for taller cards", () => Carousel.ChildrenOfType<PanelBeatmapSet>().First(p => p.Item != null).Item!.DrawHeight,
                () => Is.EqualTo(heightBefore * 1.5f).Within(0.5f));

            AddStep("remove component", () => skinLayer.Remove(layout, true));
            AddUntilStep("cards back to normal", () => Carousel.ChildrenOfType<Panel>().Where(p => p.Item != null).All(p => p.Anchor == Anchor.TopRight && p.Width == 1 && p.Rotation == 0));
        }

        [Test]
        public void TestPerformanceGroups()
        {
            AddAssert("unknown", () => BeatmapCarouselFilterGrouping.DefineGroupByPerformance(null).Title.ToString(), () => Is.EqualTo("Working out pp..."));
            AddAssert("12pp", () => BeatmapCarouselFilterGrouping.DefineGroupByPerformance(12).Title.ToString(), () => Is.EqualTo("0-50pp"));
            AddAssert("50pp", () => BeatmapCarouselFilterGrouping.DefineGroupByPerformance(50).Title.ToString(), () => Is.EqualTo("0-50pp"));
            AddAssert("75pp", () => BeatmapCarouselFilterGrouping.DefineGroupByPerformance(75).Title.ToString(), () => Is.EqualTo("51-100pp"));
            AddAssert("150pp", () => BeatmapCarouselFilterGrouping.DefineGroupByPerformance(150).Title.ToString(), () => Is.EqualTo("100-200pp"));
            AddAssert("2950pp", () => BeatmapCarouselFilterGrouping.DefineGroupByPerformance(2950).Title.ToString(), () => Is.EqualTo("2900-3000pp"));
            AddAssert("4000pp", () => BeatmapCarouselFilterGrouping.DefineGroupByPerformance(4000).Title.ToString(), () => Is.EqualTo("3000+pp"));
            AddAssert("groups in order", () => BeatmapCarouselFilterGrouping.DefineGroupByPerformance(75).Order,
                () => Is.LessThan(BeatmapCarouselFilterGrouping.DefineGroupByPerformance(150).Order));
        }

        [Test]
        public void TestGroupAndSortByPerformance()
        {
            GroupBy(GroupMode.PerformanceSS);
            WaitForFiltering();

            AddUntilStep("pp progress notification posted", () => SongSelect.ChildrenOfType<SSPerformanceStore>().Single().LastNotification?.Text.ToString(),
                () => Does.Contain("pp for sorting"));

            AddUntilStep("beatmaps get pp groups", () => grouping.GroupItems.Keys.Any(g => g.Title.ToString().EndsWith("pp", System.StringComparison.Ordinal)));
            AddUntilStep("nothing left to work out", () => grouping.GroupItems.Keys.All(g => g.Title.ToString() != "Working out pp..."));

            SortBy(SortMode.PerformanceSS);
            WaitForFiltering();

            AddAssert("difficulties sorted by pp", () =>
            {
                var store = SongSelect.ChildrenOfType<SSPerformanceStore>().Single();
                var pps = Carousel.GetCarouselItems()!.Select(i => i.Model).OfType<GroupedBeatmap>().Select(b => store.Get(b.Beatmap) ?? 0).ToList();
                return pps.SequenceEqual(pps.OrderBy(p => p));
            });

            // Test beatmaps have no hit objects (0pp), so check that the values are worked out again for the new mods.
            int updates = 0;
            AddStep("count updates", () => SongSelect.ChildrenOfType<SSPerformanceStore>().Single().Updated += () => updates++);
            ChangeMods(new OsuModDoubleTime());
            AddUntilStep("pp worked out again for DT", () => updates, () => Is.GreaterThan(0));
            AddUntilStep("all values back", () => grouping.GroupItems.Keys.All(g => g.Title.ToString() != "Working out pp..."));
        }

        [Test]
        public void TestSimulatedPerformance()
        {
            AddUntilStep("cards show the default simulation", () => Carousel.ChildrenOfType<PanelPerformanceDisplay>().Any(d => d.SimulatedText.StartsWith("98% 0x", System.StringComparison.Ordinal)));

            AddStep("give the selected beatmap its own simulation", () =>
            {
                var store = SongSelect.ChildrenOfType<PerformanceSimulationStore>().Single();
                store.Set(Beatmap.Value.BeatmapInfo, new OilySimulation(95.5, 3, 100));
            });

            AddUntilStep("its card shows it", () => Carousel.ChildrenOfType<PanelPerformanceDisplay>().Any(d => d.SimulatedText.StartsWith("95.5% 3x", System.StringComparison.Ordinal)));

            AddStep("open the popup", () => SongSelect.ShowPerformanceSimulation(Beatmap.Value.BeatmapInfo));
            AddUntilStep("popup shown", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().State.Value == Visibility.Visible);
            AddStep("close it", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().Hide());

            AddStep("back to the default", () => SongSelect.ChildrenOfType<PerformanceSimulationStore>().Single().Set(Beatmap.Value.BeatmapInfo, null));
            AddUntilStep("no card shows it", () => Carousel.ChildrenOfType<PanelPerformanceDisplay>().All(d => !d.SimulatedText.StartsWith("95.5%", System.StringComparison.Ordinal)));

            GroupBy(GroupMode.PerformanceSimulated);
            SortBy(SortMode.PerformanceSimulated);
            WaitForFiltering();

            AddUntilStep("simulated pp groups", () => grouping.GroupItems.Keys.Any(g => g.Title.ToString().EndsWith("pp", System.StringComparison.Ordinal)));
        }

        private BeatmapCarouselFilterGrouping grouping => Carousel.Filters.OfType<BeatmapCarouselFilterGrouping>().Single();

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
