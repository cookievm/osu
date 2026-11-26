// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Difficulty;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Play.HUD;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning.Components;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
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
            AddAssert("beatmap attribute text not offered", () => SongSelectSkinComponents.IsAllowed(typeof(BeatmapAttributeText)), () => Is.False);
            AddAssert("simulated pp text not offered (cards show it)", () => SongSelectSkinComponents.IsAllowed(typeof(SimulatedPerformanceText)), () => Is.False);
            AddAssert("pp simulator colours not offered (own scene)", () => SongSelectSkinComponents.IsAllowed(typeof(PpSimulatorColours)), () => Is.False);
            AddAssert("song select pieces offered", () => SongSelectSkinComponents.IsAllowed(typeof(SongSelectDifficultyStatsLayout)), () => Is.True);
        }

        [Test]
        public void TestPiecesSelectableByDefault()
        {
            Quad natural = default;

            AddUntilStep("every piece has a layout", () =>
                SongSelect.ChildrenOfType<SongSelectPieceContainer>().All(p => skinLayer.Components.OfType<SongSelectPieceLayout>().Any(l => l.Piece == p.Piece && l.FollowLazer.Value)));
            AddStep("remember list position", () => natural = listPiece.NaturalScreenSpaceQuad);
            AddWaitStep("let it settle", 2);
            AddAssert("list layout covers the list", () => Vector2.Distance(listLayout.ScreenSpaceDrawQuad.TopLeft, listPiece.NaturalScreenSpaceQuad.TopLeft), () => Is.LessThan(1));
            AddAssert("list in lazer's place", () => listPiece.Child.Position == Vector2.Zero && listPiece.Child.Scale == Vector2.One);

            AddStep("move it like the skin editor", () => listLayout.X -= 200);
            AddUntilStep("stops following lazer", () => listLayout.FollowLazer.Value, () => Is.False);
            AddUntilStep("list moved left", () => Carousel.ScreenSpaceDrawQuad.TopLeft.X, () => Is.LessThan(natural.TopLeft.X - 50));
        }

        [Test]
        public void TestRecolourParts()
        {
            SongSelectSearchBarLayout searchLayout = null!;

            AddUntilStep("search bar layout added", () => (searchLayout = skinLayer.Components.OfType<SongSelectSearchBarLayout>().FirstOrDefault()!) != null);

            AddStep("red search box text", () => searchLayout.SearchBoxText.Value = Colour4.Red);
            AddUntilStep("search box text is red", () => SongSelect.ChildrenOfType<FilterControl>().Single().SearchBox.ChildrenOfType<OsuSpriteText>()
                                                                   .Where(t => t.Alpha > 0).All(t => t.Colour.TopLeft.SRGB.G < 0.01f && t.Colour.TopLeft.SRGB.R > 0.99f));
            AddAssert("sort dropdown text untouched", () => SongSelect.ChildrenOfType<FilterControl>().Single().SortDropdown.ChildrenOfType<OsuSpriteText>()
                                                                      .Any(t => t.Colour.TopLeft.SRGB.G > 0.5f));

            AddStep("half see-through sort dropdown", () => searchLayout.SortOpacity.Value = 0.5f);
            AddUntilStep("sort dropdown faded", () => SongSelect.ChildrenOfType<FilterControl>().Single().SortDropdown.Alpha, () => Is.EqualTo(0.5f).Within(0.01f));

            AddStep("back to lazer's look", () =>
            {
                searchLayout.SearchBoxText.Value = Colour4.White;
                searchLayout.SortOpacity.Value = 1;
            });
            AddUntilStep("sort dropdown back", () => SongSelect.ChildrenOfType<FilterControl>().Single().SortDropdown.Alpha, () => Is.EqualTo(1));
            AddUntilStep("search box text back", () => SongSelect.ChildrenOfType<FilterControl>().Single().SearchBox.ChildrenOfType<OsuSpriteText>()
                                                                 .Any(t => t.Colour.TopLeft.SRGB.G > 0.5f));
        }

        [Test]
        public void TestPpSimulatorScene()
        {
            AddUntilStep("simulator layer has its colours", () => SongSelect.ChildrenOfType<SkinnableContainer>().Single(c => c.Lookup.Lookup == GlobalSkinnableContainers.PpSimulator)
                                                                           .Components.OfType<PpSimulatorColours>().Any());
            AddAssert("song select layer offered", () => skinLayer.AvailableInEditor!(), () => Is.True);
            AddAssert("simulator layer not offered", () => SongSelect.ChildrenOfType<SkinnableContainer>().Single(c => c.Lookup.Lookup == GlobalSkinnableContainers.PpSimulator).AvailableInEditor!(),
                () => Is.False);
            AddStep("open simulator scene", () => SongSelect.ShowPerformanceSimulatorScene());
            AddUntilStep("simulator layer offered", () => SongSelect.ChildrenOfType<SkinnableContainer>().Single(c => c.Lookup.Lookup == GlobalSkinnableContainers.PpSimulator).AvailableInEditor!());
            AddAssert("song select layer not offered", () => skinLayer.AvailableInEditor!(), () => Is.False);

            AddStep("add a second colours component, like an older skin", () => simulatorLayer.Add(new PpSimulatorColours()));
            AddStep("make the first one's save button red", () => simulatorLayer.Components.OfType<PpSimulatorColours>().First().SaveButton.Value = Colour4.Red);
            AddUntilStep("save button red", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().ChildrenOfType<RoundedButton>()
                                                            .First(b => b.Text.ToString() == "Save for this beatmap").BackgroundColour, () => Is.EqualTo((Color4)Colour4.Red));
            AddStep("make the box green", () => simulatorLayer.Components.OfType<PpSimulatorColours>().First().Box.Value = Colour4.Green);
            AddUntilStep("box green", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().ChildrenOfType<Box>()
                                                    .Any(b => b.Colour.TopLeft.SRGB.G > 0.4f && b.Colour.TopLeft.SRGB.R < 0.01f && b.Colour.TopLeft.SRGB.B < 0.01f));
            AddStep("reset", () => simulatorLayer.Components.OfType<PpSimulatorColours>().First().SaveButton.Value = Colour4.White);
            AddStep("close it", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().Hide());
        }

        private SkinnableContainer simulatorLayer => SongSelect.ChildrenOfType<SkinnableContainer>().Single(c => c.Lookup.Lookup == GlobalSkinnableContainers.PpSimulator);

        private SongSelectBeatmapListLayout listLayout => skinLayer.Components.OfType<SongSelectBeatmapListLayout>().First();

        [Test]
        public void TestMovedMapDetailsNotCutOff()
        {
            AddUntilStep("every piece has a layout", () =>
                SongSelect.ChildrenOfType<SongSelectPieceContainer>().All(p => skinLayer.Components.OfType<SongSelectPieceLayout>().Any(l => l.Piece == p.Piece && l.FollowLazer.Value)));
            AddAssert("info box cuts off its content", () => SongSelect.ChildrenOfType<BeatmapTitleWedge>().Single().Masking, () => Is.True);

            AddStep("move difficulty stats", () => skinLayer.Components.OfType<SongSelectDifficultyStatsLayout>().First().Y += 300);
            AddUntilStep("info box doesn't cut off moved stats", () => SongSelect.ChildrenOfType<BeatmapTitleWedge>().Single().Masking, () => Is.False);
            AddAssert("difficulty card doesn't either", () => SongSelect.ChildrenOfType<BeatmapTitleWedge.DifficultyDisplay>().Single().Masking, () => Is.False);

            AddStep("put it back in lazer's place", () => skinLayer.Components.OfType<SongSelectDifficultyStatsLayout>().First().FollowLazer.Value = true);
            AddUntilStep("info box cuts off again", () => SongSelect.ChildrenOfType<BeatmapTitleWedge>().Single().Masking, () => Is.True);
        }

        [Test]
        public void TestCropAndResizeWithoutStretching()
        {
            Quad natural = default;

            AddUntilStep("every piece has a layout", () =>
                SongSelect.ChildrenOfType<SongSelectPieceContainer>().All(p => skinLayer.Components.OfType<SongSelectPieceLayout>().Any(l => l.Piece == p.Piece && l.FollowLazer.Value)));
            AddStep("remember list position", () => natural = listPiece.NaturalScreenSpaceQuad);

            AddStep("crop the left half", () => listLayout.CropLeft.Value = 0.5f);
            AddUntilStep("list cut off on the left", () => listPiece.Child.ScreenSpaceDrawQuad.TopLeft.X, () => Is.EqualTo(natural.TopLeft.X + natural.Width / 2).Within(2));
            AddAssert("cut off part hidden", () => ((Container)listPiece.Child).Masking);
            AddAssert("right edge stays", () => listPiece.Child.ScreenSpaceDrawQuad.TopRight.X, () => Is.EqualTo(natural.TopRight.X).Within(2));
            AddUntilStep("layout covers what's left", () => listLayout.ScreenSpaceDrawQuad.Width, () => Is.EqualTo(natural.Width / 2).Within(2));
            AddStep("no crop", () => listLayout.CropLeft.Value = 0);
            AddUntilStep("nothing cut off", () => ((Container)listPiece.Child).Masking, () => Is.False);

            AddStep("make it half as wide, like a window", () =>
            {
                listLayout.FollowLazer.Value = false;
                listLayout.Width /= 2;
            });
            AddUntilStep("cards laid out at half width", () => Carousel.DrawWidth, () => Is.EqualTo(listPiece.DrawWidth / 2).Within(2));
            AddAssert("cards not squashed", () => listPiece.Child.Scale, () => Is.EqualTo(Vector2.One));

            AddStep("zoom out", () => listLayout.Scale = new Vector2(0.5f));
            AddUntilStep("cards squashed evenly", () => listPiece.Child.Scale, () => Is.EqualTo(new Vector2(0.5f)));
        }

        [Test]
        public void TestCroppedSearchBarKeepsItsSlantedEnds()
        {
            SongSelectSearchBarLayout searchLayout = null!;
            SongSelectPieceContainer searchPiece = null!;

            AddUntilStep("search bar layout added", () => (searchLayout = skinLayer.Components.OfType<SongSelectSearchBarLayout>().FirstOrDefault()!) != null);
            AddStep("find search bar", () => searchPiece = SongSelect.ChildrenOfType<SongSelectPieceContainer>().Single(p => p.Piece == SongSelectPiece.SearchBar));
            AddStep("crop a little off the bottom", () => searchLayout.CropBottom.Value = 0.1f);
            AddUntilStep("cut off", () => ((Container)searchPiece.Child).Masking);
            AddUntilStep("slanted bottom left not cut off", () => SongSelect.ChildrenOfType<FilterControl>().Single().ScreenSpaceDrawQuad.BottomLeft.X,
                () => Is.GreaterThanOrEqualTo(searchPiece.Child.ScreenSpaceDrawQuad.TopLeft.X - 1));
            AddUntilStep("right end not cut off", () => SongSelect.ChildrenOfType<FilterControl>().Single().ScreenSpaceDrawQuad.TopRight.X,
                () => Is.LessThanOrEqualTo(searchPiece.Child.ScreenSpaceDrawQuad.TopRight.X + 1));
            AddUntilStep("top not cut off", () => SongSelect.ChildrenOfType<FilterControl>().Single().ScreenSpaceDrawQuad.TopRight.Y,
                () => Is.GreaterThanOrEqualTo(searchPiece.Child.ScreenSpaceDrawQuad.TopRight.Y - 1));
            AddStep("no crop", () => searchLayout.CropBottom.Value = 0);
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
        public void TestStableCards()
        {
            addLikeSkinEditor();

            AddUntilStep("cards shown", () => Carousel.ChildrenOfType<PanelBeatmapSet>().Any(p => p.Item != null));
            AddAssert("lazer cards by default", () => Carousel.ChildrenOfType<StablePanelContent>().All(c => c.Alpha == 0));

            AddStep("stable style", () => layout.Style.Value = SongSelectPieceStyle.Stable);
            AddUntilStep("stable cards shown", () => Carousel.ChildrenOfType<PanelBeatmapSet>().Where(p => p.Item != null)
                                                             .All(p => p.ChildrenOfType<StablePanelContent>().Single().Alpha == 1));
            AddUntilStep("difficulty cards as tall as set cards", () => Carousel.ChildrenOfType<PanelBeatmap>().Where(p => p.Item != null)
                                                                                .All(p => System.Math.Abs(p.DrawHeight - Panel.STABLE_HEIGHT) < 0.5f));
            AddUntilStep("carousel spaced for stable cards", () => Carousel.ChildrenOfType<PanelBeatmap>().Where(p => p.Item != null)
                                                                           .All(p => System.Math.Abs(p.Item!.DrawHeight - Panel.STABLE_HEIGHT) < 0.5f));
            AddUntilStep("selected difficulty is white", () => Carousel.ChildrenOfType<PanelBeatmap>().Where(p => p.Item != null && p.Selected.Value)
                                                                       .All(p => p.ChildrenOfType<StablePanelContent>().Single().Active));

            AddStep("lazer style", () => layout.Style.Value = SongSelectPieceStyle.Lazer);
            AddUntilStep("lazer cards back", () => Carousel.ChildrenOfType<StablePanelContent>().All(c => c.Alpha == 0));
        }

        [Test]
        public void TestStableBeatmapInfo()
        {
            SongSelectBeatmapInfoLayout info = null!;

            AddUntilStep("default layouts added", () => skinLayer.Components.OfType<SongSelectBeatmapInfoLayout>().Any());
            AddStep("find beatmap info layout", () => info = skinLayer.Components.OfType<SongSelectBeatmapInfoLayout>().First());

            AddStep("stable style", () => info.Style.Value = SongSelectPieceStyle.Stable);
            AddUntilStep("stable text block shown", () => SongSelect.ChildrenOfType<StableBeatmapInfo>().Single().Parent!.IsPresent);
            AddUntilStep("shows length and objects", () => SongSelect.ChildrenOfType<StableBeatmapInfo>().Single().ChildrenOfType<OsuSpriteText>()
                                                                      .Any(t => t.Text.ToString().StartsWith("Length:", System.StringComparison.Ordinal)));

            AddStep("lazer style", () => info.Style.Value = SongSelectPieceStyle.Lazer);
            AddUntilStep("stable text block hidden", () => !SongSelect.ChildrenOfType<StableBeatmapInfo>().Single().Parent!.IsPresent);
        }

        [Test]
        public void TestStableSearchBar()
        {
            SongSelectSearchBarLayout search = null!;

            AddUntilStep("default layouts added", () => skinLayer.Components.OfType<SongSelectSearchBarLayout>().Any());
            AddStep("find search bar layout", () => search = skinLayer.Components.OfType<SongSelectSearchBarLayout>().First());

            AddStep("stable style", () => search.Style.Value = SongSelectPieceStyle.Stable);
            AddUntilStep("filter in stable look", () => SongSelect.ChildrenOfType<FilterControl>().Single().StableLook);
            AddUntilStep("shows type to search", () => SongSelect.ChildrenOfType<FilterControl>().Single().ChildrenOfType<OsuSpriteText>()
                                                                 .Any(t => t.Text.ToString() == "type to search!"));
            AddStep("type a search", () => ((osu.Framework.Graphics.UserInterface.IHasCurrentValue<string>)SongSelect.ChildrenOfType<FilterControl>().Single().SearchBox).Current.Value = "hello");
            AddUntilStep("shows the search", () => SongSelect.ChildrenOfType<FilterControl>().Single().ChildrenOfType<OsuSpriteText>()
                                                              .Any(t => t.Text.ToString() == "hello"));

            AddStep("lazer style", () => search.Style.Value = SongSelectPieceStyle.Lazer);
            AddUntilStep("filter in lazer look", () => !SongSelect.ChildrenOfType<FilterControl>().Single().StableLook);
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
        public void TestPausedWorkStillDoesSelectedMap()
        {
            ImportBeatmapForRuleset(0);
            AddStep("pause pp work", () => SongSelect.ChildrenOfType<SSPerformanceStore>().Single().WorkPaused.Value = true);
            ChangeMods(new OsuModHardRock());

            AddStep("ask for every beatmap", () => SongSelect.ChildrenOfType<SSPerformanceStore>().Single()
                                                             .Request(Carousel.GetCarouselItems()!.Select(i => i.Model).OfType<GroupedBeatmap>().Select(b => b.Beatmap).ToList()));
            AddUntilStep("selected map worked out", () => SongSelect.ChildrenOfType<SSPerformanceStore>().Single().Get(Beatmap.Value.BeatmapInfo), () => Is.Not.Null);
            AddUntilStep("other maps wait", () =>
            {
                var store = SongSelect.ChildrenOfType<SSPerformanceStore>().Single();
                var others = Carousel.GetCarouselItems()!.Select(i => i.Model).OfType<GroupedBeatmap>().Select(b => b.Beatmap)
                                     .Where(b => b.BeatmapSet!.ID != Beatmap.Value.BeatmapSetInfo.ID).ToList();
                return others.Any(b => store.IsWaiting(b));
            });

            AddAssert("menu offers resume, not the simulator", () =>
            {
                var texts = SongSelect.GetForwardActions(Beatmap.Value.BeatmapInfo).Select(i => i.Text.Value.ToString()).ToList();
                return texts.Contains("Resume pp sorting work") && !texts.Any(t => t.Contains("pp simulation"));
            });
            AddStep("resume pp work", () => SongSelect.ChildrenOfType<SSPerformanceStore>().Single().WorkPaused.Value = false);
            AddUntilStep("everything worked out", () => SongSelect.ChildrenOfType<SSPerformanceStore>().Single().Remaining, () => Is.Zero);
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

            AddStep("press the PP Simulator button", () => this.ChildrenOfType<FooterButtonPerformanceSimulator>().Single().TriggerClick());
            AddUntilStep("popup shown", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().State.Value == Visibility.Visible);
            AddAssert("titled Universal PP Simulator", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single()
                                                                       .ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "Universal PP Simulator"));
            AddAssert("no combo slider", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single()
                                                         .ChildrenOfType<FormSliderBar<double>>().All(s => !s.Caption.ToString().Contains("Combo")));
            AddUntilStep("button lit", () => this.ChildrenOfType<FooterButtonPerformanceSimulator>().Single().OverlayState.Value == Visibility.Visible);
            AddStep("press it again", () => this.ChildrenOfType<FooterButtonPerformanceSimulator>().Single().TriggerClick());
            AddUntilStep("popup closed", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().State.Value == Visibility.Hidden);

            AddStep("colour the skin's simulator", () =>
            {
                var style = SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().Style!;
                style.SaveButton.Value = Colour4.Red;
                style.AccuracyBar.Value = Colour4.Green;
            });
            AddAssert("save button red", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().ChildrenOfType<RoundedButton>()
                                                         .First(b => b.Text.ToString() == "Save for this beatmap").BackgroundColour, () => Is.EqualTo((Color4)Colour4.Red));
            AddAssert("accuracy bar green", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().ChildrenOfType<FormSliderBar<double>>().First().FillColour,
                () => Is.EqualTo((Color4?)Colour4.Green));
            AddStep("reset skin colours", () => SongSelect.ChildrenOfType<PerformanceSimulationPopup>().Single().Style!.Reset());

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
            AddUntilStep("default layouts added", () => skinLayer.Components.OfType<SongSelectBeatmapListLayout>().Any());
            AddStep("add list layout like the skin editor", () =>
            {
                foreach (var existing in skinLayer.Components.OfType<SongSelectBeatmapListLayout>().ToArray())
                    skinLayer.Remove(existing, true);

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
