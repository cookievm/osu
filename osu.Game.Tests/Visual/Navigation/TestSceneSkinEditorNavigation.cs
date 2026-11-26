// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Toolbar;
using osu.Game.Configuration;
using osu.Game.Screens.Footer;
using osu.Game.Beatmaps;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osu.Game.Tests.Beatmaps.IO;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneSkinEditorNavigation : OsuGameTestScene
    {
        private SoloSongSelect songSelect;
        private ModSelectOverlay modSelect => Game.ChildrenOfType<ModSelectOverlay>().First();

        private SkinEditor skinEditor => Game.ChildrenOfType<SkinEditor>().FirstOrDefault();

        [Test]
        public void TestEditComponentFromGameplayScene()
        {
            advanceToSongSelect();
            openSkinEditor();

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            switchToGameplayScene();

            ArgonAccuracyCounter accuracyCounter = null;

            AddUntilStep("select argon accuracy counter blueprint", () =>
            {
                var blueprint = skinEditor.ChildrenOfType<SkinBlueprint>().FirstOrDefault(b => b.Item is ArgonAccuracyCounter);

                if (blueprint == null)
                    return false;

                accuracyCounter = (ArgonAccuracyCounter)blueprint.Item;
                skinEditor.SelectedComponents.Clear();
                skinEditor.SelectedComponents.Add(blueprint.Item);
                return true;
            });

            AddAssert("value is default", () => accuracyCounter.WireframeOpacity.IsDefault);

            AddStep("hover first slider", () =>
            {
                InputManager.MoveMouseTo(
                    skinEditor.ChildrenOfType<SkinSettingsToolbox>().First()
                              .ChildrenOfType<SettingsSlider<float>>().First()
                              .ChildrenOfType<SliderBar<float>>().First()
                );
            });

            AddStep("adjust slider via keyboard", () => InputManager.Key(Key.Left));

            AddAssert("value is less than default", () => accuracyCounter.WireframeOpacity.Value < accuracyCounter.WireframeOpacity.Default);
        }

        [Test]
        public void TestMutateProtectedSkinDuringGameplay()
        {
            advanceToSongSelect();
            AddStep("set default skin", () => Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.SetDefault());

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("enable NF", () => Game.SelectedMods.Value = new[] { new OsuModNoFail() });
            AddStep("enter gameplay", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return Game.ScreenStack.CurrentScreen is Player;
            });

            openSkinEditor();
            AddUntilStep("current skin is mutable", () => !Game.Dependencies.Get<SkinManager>().CurrentSkin.Value.SkinInfo.Value.Protected);
        }

        [Test]
        public void TestMutateProtectedSkinFromMainMenu_UndoToInitialStateIsCorrect()
        {
            AddStep("set default skin", () => Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.SetDefault());
            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());

            openSkinEditor();
            AddUntilStep("current skin is mutable", () => !Game.Dependencies.Get<SkinManager>().CurrentSkin.Value.SkinInfo.Value.Protected);

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return Game.ScreenStack.CurrentScreen is Player;
            });

            string state = string.Empty;

            AddUntilStep("wait for accuracy counter", () => Game.ChildrenOfType<ArgonAccuracyCounter>().Any(counter => counter.Position != new Vector2()));
            AddStep("dump state of accuracy meter", () => state = JsonConvert.SerializeObject(Game.ChildrenOfType<ArgonAccuracyCounter>().First().CreateSerialisedInfo()));
            AddStep("add any component", () => Game.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>().First().TriggerClick());
            AddStep("undo", () => InputManager.Keys(PlatformAction.Undo));
            AddUntilStep("only one accuracy meter left",
                () => Game.ChildrenOfType<Player>().Single().ChildrenOfType<ArgonAccuracyCounter>().Count(),
                () => Is.EqualTo(1));
            AddAssert("accuracy meter state unchanged",
                () => JsonConvert.SerializeObject(Game.ChildrenOfType<ArgonAccuracyCounter>().First().CreateSerialisedInfo()),
                () => Is.EqualTo(state));
        }

        [Test]
        public void TestMutateProtectedSkinFromPlayer_UndoToInitialStateIsCorrect()
        {
            AddStep("set default skin", () => Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.SetDefault());
            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            advanceToSongSelect();
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("enable NF", () => Game.SelectedMods.Value = new[] { new OsuModNoFail() });
            AddStep("enter gameplay", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return Game.ScreenStack.CurrentScreen is Player;
            });
            openSkinEditor();

            string state = string.Empty;

            AddUntilStep("wait for accuracy counter", () => Game.ChildrenOfType<ArgonAccuracyCounter>().Any(counter => counter.Position != new Vector2()));
            AddStep("dump state of accuracy meter", () => state = JsonConvert.SerializeObject(Game.ChildrenOfType<ArgonAccuracyCounter>().First().CreateSerialisedInfo()));
            AddStep("add any component", () => Game.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>().First().TriggerClick());
            AddStep("undo", () => InputManager.Keys(PlatformAction.Undo));
            AddUntilStep("only one accuracy meter left",
                () => Game.ChildrenOfType<Player>().Single().ChildrenOfType<ArgonAccuracyCounter>().Count(),
                () => Is.EqualTo(1));
            AddAssert("accuracy meter state unchanged",
                () => JsonConvert.SerializeObject(Game.ChildrenOfType<ArgonAccuracyCounter>().First().CreateSerialisedInfo()),
                () => Is.EqualTo(state));
        }

        [Test]
        public void TestComponentsDeselectedOnSkinEditorHide()
        {
            advanceToSongSelect();
            openSkinEditor();

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            switchToGameplayScene();

            // oily: song select's own pieces have blueprints too, so wait for gameplay's.
            AddUntilStep("wait for components", () => skinEditor.ChildrenOfType<SkinBlueprint>().Any(b => b.Item is not Screens.Select.SongSelectPieceLayout));

            AddStep("select all components", () => InputManager.Keys(PlatformAction.SelectAll));

            AddUntilStep("components selected", () => skinEditor.SelectedComponents.Count > 0);

            toggleSkinEditor();

            AddUntilStep("no components selected", () => skinEditor.SelectedComponents.Count == 0);
        }

        [Test]
        public void TestSwitchScreenWhileDraggingComponent()
        {
            Vector2 firstBlueprintCentre = Vector2.Zero;
            ScheduledDelegate movementDelegate = null;

            advanceToSongSelect();

            openSkinEditor();

            AddStep("add skinnable component", () =>
            {
                skinEditor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>().First().TriggerClick();
            });

            AddUntilStep("newly added component selected", () => skinEditor.SelectedComponents.Count == 1);

            AddStep("start drag", () =>
            {
                firstBlueprintCentre = skinEditor.ChildrenOfType<SkinBlueprint>().First().ScreenSpaceDrawQuad.Centre;

                InputManager.MoveMouseTo(firstBlueprintCentre);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("start movement", () => movementDelegate = Scheduler.AddDelayed(() => { InputManager.MoveMouseTo(firstBlueprintCentre += new Vector2(1)); }, 10, true));

            toggleSkinEditor();
            AddStep("exit song select", () => songSelect.Exit());

            AddUntilStep("wait for blueprints removed", () => !skinEditor.ChildrenOfType<SkinBlueprint>().Any());

            AddStep("stop drag", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
                movementDelegate?.Cancel();
            });
        }

        [Test]
        public void TestAutoplayCompatibleModsRetainedOnEnteringGameplay()
        {
            advanceToSongSelect();
            openSkinEditor();
            AddStep("select DT", () => Game.SelectedMods.Value = new Mod[] { new OsuModDoubleTime() });

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            switchToGameplayScene();

            AddAssert("DT still selected", () => ((Player)Game.ScreenStack.CurrentScreen).Mods.Value.Single() is OsuModDoubleTime);
        }

        [Test]
        public void TestAutoplayIncompatibleModsRemovedOnEnteringGameplay()
        {
            advanceToSongSelect();
            openSkinEditor();
            AddStep("select relax and spun out", () => Game.SelectedMods.Value = new Mod[] { new OsuModRelax(), new OsuModSpunOut() });

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            switchToGameplayScene();

            AddAssert("no mod selected", () => !((Player)Game.ScreenStack.CurrentScreen).Mods.Value.Any());
        }

        [Test]
        public void TestDuplicateAutoplayModRemovedOnEnteringGameplay()
        {
            advanceToSongSelect();
            openSkinEditor();
            AddStep("select autoplay", () => Game.SelectedMods.Value = new Mod[] { new OsuModAutoplay() });

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            switchToGameplayScene();

            AddAssert("no mod selected", () => !((Player)Game.ScreenStack.CurrentScreen).Mods.Value.Any());
        }

        [Test]
        public void TestGameplaySettingsDoesNotExpandWhenSkinOverlayPresent()
        {
            advanceToSongSelect();
            openSkinEditor();
            AddUntilStep("skin editor visible", () => skinEditor.State.Value == Visibility.Visible);

            AddStep("select autoplay", () => Game.SelectedMods.Value = new Mod[] { new OsuModAutoplay() });
            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);
            switchToGameplayScene();

            AddUntilStep("wait for settings", () => getPlayerSettingsOverlay() != null);
            AddAssert("settings not visible", () => getPlayerSettingsOverlay().DrawWidth, () => Is.EqualTo(0));

            AddStep("move cursor to right of screen", () => InputManager.MoveMouseTo(InputManager.ScreenSpaceDrawQuad.TopRight));
            AddAssert("settings not visible", () => getPlayerSettingsOverlay().DrawWidth, () => Is.EqualTo(0));

            toggleSkinEditor();
            AddUntilStep("skin editor hidden", () => skinEditor.State.Value == Visibility.Hidden);

            AddStep("move cursor slightly", () => InputManager.MoveMouseTo(InputManager.ScreenSpaceDrawQuad.TopRight + new Vector2(2)));
            AddUntilStep("settings visible", () => getPlayerSettingsOverlay().DrawWidth, () => Is.GreaterThan(0));

            AddStep("move cursor to right of screen too far", () => InputManager.MoveMouseTo(InputManager.ScreenSpaceDrawQuad.TopRight + new Vector2(10240, 0)));
            AddUntilStep("settings not visible", () => getPlayerSettingsOverlay().DrawWidth, () => Is.EqualTo(0));

            ReplaySettingsOverlay getPlayerSettingsOverlay() => ((Player)Game.ScreenStack.CurrentScreen).ChildrenOfType<ReplaySettingsOverlay>().SingleOrDefault();
        }

        [Test]
        public void TestCinemaModRemovedOnEnteringGameplay()
        {
            advanceToSongSelect();
            openSkinEditor();
            AddStep("select cinema", () => Game.SelectedMods.Value = new Mod[] { new OsuModCinema() });

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            switchToGameplayScene();

            AddAssert("no mod selected", () => !((Player)Game.ScreenStack.CurrentScreen).Mods.Value.Any());
        }

        [Test]
        public void TestModOverlayClosesOnOpeningSkinEditor()
        {
            advanceToSongSelect();
            AddStep("open mod overlay", () => modSelect.Show());

            openSkinEditor();
            AddUntilStep("mod overlay closed", () => modSelect.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestResizeAndCropSongSelectPartWithHandles()
        {
            SongSelectBeatmapListLayout layout = null!;
            Quad cardsBefore = default;
            float cardsWidth = 0;

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            advanceToSongSelect();
            openSkinEditor();

            AddUntilStep("find beatmap list layout", () => (layout = Game.ChildrenOfType<SongSelectBeatmapListLayout>().FirstOrDefault(l => l.FollowLazer.Value)!) != null);
            AddStep("select it", () =>
            {
                skinEditor.SelectedComponents.Clear();
                skinEditor.SelectedComponents.Add(layout);
            });
            AddAssert("no crop sliders", () => skinEditor.ChildrenOfType<ISettingsItem>().OfType<Drawable>()
                                                         .Any(d => d.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().StartsWith("Crop", StringComparison.Ordinal))), () => Is.False);

            AddStep("remember cards", () =>
            {
                var carousel = Game.ChildrenOfType<BeatmapCarousel>().Single();
                cardsBefore = carousel.ScreenSpaceDrawQuad;
                cardsWidth = carousel.DrawWidth;
            });

            dragHandle(Anchor.CentreLeft, new Vector2(200, 0));

            AddUntilStep("cards laid out narrower", () => Game.ChildrenOfType<BeatmapCarousel>().Single().DrawWidth, () => Is.LessThan(cardsWidth - 50));
            AddAssert("cards not squashed", () => Game.ChildrenOfType<BeatmapCarousel>().Single().DrawInfo.Matrix.ExtractScale().X,
                () => Is.EqualTo(cardsBefore.Width / cardsWidth).Within(0.01f));
            AddAssert("not cropped", () => layout.CropLeft.Value, () => Is.EqualTo(0));

            AddStep("remember cards", () => cardsBefore = Game.ChildrenOfType<BeatmapCarousel>().Single().ScreenSpaceDrawQuad);
            AddStep("hold ctrl", () => InputManager.PressKey(Key.ControlLeft));
            dragHandle(Anchor.CentreLeft, new Vector2(100, 0));
            AddStep("let go of ctrl", () => InputManager.ReleaseKey(Key.ControlLeft));

            AddAssert("left side cropped", () => layout.CropLeft.Value, () => Is.GreaterThan(0.05f));
            AddUntilStep("cards stay where they were", () => Vector2.Distance(Game.ChildrenOfType<BeatmapCarousel>().Single().ScreenSpaceDrawQuad.TopLeft, cardsBefore.TopLeft), () => Is.LessThan(2));
            AddAssert("cut off part hidden", () => ((Container)Game.ChildrenOfType<SongSelectPieceContainer>().Single(p => p.Piece == SongSelectPiece.BeatmapList).Child).Masking);

            AddStep("hold ctrl", () => InputManager.PressKey(Key.ControlLeft));
            dragHandle(Anchor.CentreLeft, new Vector2(-500, 0));
            AddStep("let go of ctrl", () => InputManager.ReleaseKey(Key.ControlLeft));
            AddAssert("uncropped, but no further", () => layout.CropLeft.Value, () => Is.EqualTo(0));
            AddUntilStep("cards still where they were", () => Vector2.Distance(Game.ChildrenOfType<BeatmapCarousel>().Single().ScreenSpaceDrawQuad.TopLeft, cardsBefore.TopLeft), () => Is.LessThan(2));

            void dragHandle(Anchor handle, Vector2 by)
            {
                AddStep($"drag {handle} handle", () =>
                {
                    var h = skinEditor.ChildrenOfType<SelectionBoxScaleHandle>().Single(s => s.Anchor == handle);
                    InputManager.MoveMouseTo(h);
                    InputManager.PressButton(MouseButton.Left);
                });
                AddStep("move", () => InputManager.MoveMouseTo(InputManager.CurrentState.Mouse.Position + by));
                AddStep("let go", () => InputManager.ReleaseButton(MouseButton.Left));
            }
        }

        [Test]
        public void TestInfoBoxShownWithDraggedToolbar()
        {
            // A dragged toolbar doesn't push the screen down, so the hidden info box starts exactly on the screen's top edge.
            AddStep("drag the toolbar", () =>
            {
                var layout = ToolbarLayout.CreateDefault();
                layout.Bars[0].OffsetY = 0.9f;
                Game.LocalConfig.SetValue(OsuSetting.OilyToolbarLayout, layout.Serialise());
            });

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            advanceToSongSelect();

            AddUntilStep("info box shown", () => Game.ChildrenOfType<BeatmapTitleWedge>().Single().Alpha, () => Is.EqualTo(1));
            AddUntilStep("details shown", () => Game.ChildrenOfType<BeatmapDetailsArea>().Single().Alpha, () => Is.EqualTo(1));

            AddStep("put the toolbar back", () => Game.LocalConfig.SetValue(OsuSetting.OilyToolbarLayout, string.Empty));
        }

        [Test]
        public void TestScreenShrinksEvenlyWithToolbars()
        {
            Vector2 screenSize = Vector2.Zero;

            advanceToSongSelect();
            AddStep("remember screen size", () => screenSize = Game.ScreenStack.DrawSize);
            openSkinEditor();

            AddUntilStep("screen shrunk", () => screenPreview.Scale.X, () => Is.LessThan(0.95f));
            AddAssert("screen keeps its shape", () => screenPreview.Scale.X, () => Is.EqualTo(screenPreview.Scale.Y));
            AddAssert("screen not resized", () => Game.ScreenStack.DrawSize, () => Is.EqualTo(screenSize));
            AddAssert("toolbars still shown", () => Game.Toolbar.State.Value, () => Is.EqualTo(Visibility.Visible));
            AddUntilStep("toolbars shrunk the same", () => toolbarPreview.ScreenSpaceDrawQuad.AlmostEquals(screenPreview.ScreenSpaceDrawQuad));
            AddAssert("toolbars can't be clicked", () => Game.Toolbar.PropagatePositionalInputSubTree, () => Is.False);

            AddStep("click toolbars tab", () => skinEditor.ChildrenOfType<SkinEditorSceneLibrary.SceneButton>().Single(b => b.Text == "Toolbars").TriggerClick());
            AddUntilStep("toolbar options shown", () => skinEditor.ChildrenOfType<ToolbarLayoutEditor>().Count(), () => Is.EqualTo(1));
            AddUntilStep("bottom bar options shown", () => skinEditor.ChildrenOfType<BottomBarLayoutEditor>().Count(), () => Is.EqualTo(1));
            AddUntilStep("drag boxes shown", () => Game.ChildrenOfType<BarEditLayer.DragBox>().Count(), () => Is.GreaterThan(1));
            AddAssert("background opacity sliders shown", () => skinEditor.ChildrenOfType<PanelOpacitySlider>().Count(), () => Is.EqualTo(2));
            AddStep("make the toolbars transparent", () => skinEditor.ChildrenOfType<PanelOpacitySlider>().First().ChildrenOfType<FormSliderBar<float>>().Single().Current.Value = 0);
            AddUntilStep("toolbars transparent", () => OilyPanelColours.Parse(Game.LocalConfig.Get<string>(OsuSetting.OilyPanelColours)).Get(OilyPanel.Toolbar).Opacity, () => Is.Zero);
            AddStep("toolbars back to normal", () => Game.LocalConfig.SetValue(OsuSetting.OilyPanelColours, string.Empty));

            AddStep("drag the toolbar down", () =>
            {
                var box = Game.ChildrenOfType<BarEditLayer.DragBox>().First(b => b.Target is Toolbar.ToolbarStrip);
                InputManager.MoveMouseTo(box);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(box.ScreenSpaceDrawQuad.Centre + new Vector2(0, 100));
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddUntilStep("toolbar moved and saved", () => ToolbarLayout.Parse(Game.LocalConfig.Get<string>(OsuSetting.OilyToolbarLayout)).Bars[0].OffsetY, () => Is.GreaterThan(0));

            AddStep("drag the mods button up", () =>
            {
                var box = Game.ChildrenOfType<BarEditLayer.DragBox>().First(b => b.Target is FooterButtonMods);
                InputManager.MoveMouseTo(box);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(box.ScreenSpaceDrawQuad.Centre - new Vector2(0, 100));
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddUntilStep("mods button moved and saved", () => FooterLayout.Parse(Game.LocalConfig.Get<string>(OsuSetting.OilyFooterLayout)).Buttons.Single(b => b.Item == FooterItem.Mods).OffsetY, () => Is.LessThan(0));
            AddUntilStep("mods button drawn higher", () => Game.ChildrenOfType<FooterButtonMods>().Single().Margin.Top, () => Is.LessThan(0));

            AddStep("drag the back button up", () =>
            {
                var box = Game.ChildrenOfType<BarEditLayer.DragBox>().First(b => b.Target is ScreenBackButton);
                InputManager.MoveMouseTo(box);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(box.ScreenSpaceDrawQuad.Centre - new Vector2(0, 100));
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddUntilStep("back button moved and saved", () => FooterLayout.Parse(Game.LocalConfig.Get<string>(OsuSetting.OilyFooterLayout)).BackOffsetY, () => Is.LessThan(0));
            AddUntilStep("back button drawn higher", () => Game.ChildrenOfType<ScreenBackButton>().Single().Y, () => Is.LessThan(0));

            AddStep("put settings button on the bottom bar", () =>
            {
                var layout = ToolbarLayout.Parse(Game.LocalConfig.Get<string>(OsuSetting.OilyToolbarLayout));
                layout.Buttons.Single(b => b.Item == ToolbarItem.Settings).Bar = ToolbarLayout.BOTTOM_BAR;
                Game.LocalConfig.SetValue(OsuSetting.OilyToolbarLayout, layout.Serialise());
            });
            AddUntilStep("settings button on the bottom bar", () => Game.ChildrenOfType<ScreenFooter>().Single().ChildrenOfType<ToolbarSettingsButton>().Count(), () => Is.EqualTo(1));
            AddUntilStep("settings button not on the toolbar", () => Game.Toolbar.ChildrenOfType<ToolbarSettingsButton>().Count(), () => Is.Zero);
            AddStep("drag the toolbar buttons left", () =>
            {
                var footer = Game.ChildrenOfType<ScreenFooter>().Single();
                var box = Game.ChildrenOfType<BarEditLayer.DragBox>().First(b => b.Target == footer.ToolbarButtons);
                InputManager.MoveMouseTo(box);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(box.ScreenSpaceDrawQuad.Centre - new Vector2(100, 0));
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddUntilStep("toolbar buttons moved and saved", () => FooterLayout.Parse(Game.LocalConfig.Get<string>(OsuSetting.OilyFooterLayout)).ToolbarButtonsOffsetX, () => Is.LessThan(0));

            AddStep("put things back", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.OilyToolbarLayout, string.Empty);
                Game.LocalConfig.SetValue(OsuSetting.OilyFooterLayout, string.Empty);
            });

            AddStep("select a component", () => skinEditor.SelectedComponents.Add(skinEditor.ChildrenOfType<SkinBlueprint>().First().Item));
            AddUntilStep("toolbar options gone", () => skinEditor.ChildrenOfType<ToolbarLayoutEditor>().Count(), () => Is.Zero);
            AddUntilStep("drag boxes gone", () => Game.ChildrenOfType<BarEditLayer.DragBox>().Count(), () => Is.Zero);

            toggleSkinEditor();
            AddUntilStep("screen full size again", () => screenPreview.Scale.X, () => Is.EqualTo(1));
            AddAssert("toolbars clickable again", () => Game.Toolbar.PropagatePositionalInputSubTree, () => Is.True);
        }

        private EditorPreviewContainer screenPreview => Game.ChildrenOfType<EditorPreviewContainer>().First(p => p.ChildrenOfType<OsuScreenStack>().Any());
        private EditorPreviewContainer toolbarPreview => Game.ChildrenOfType<EditorPreviewContainer>().First(p => p.ChildrenOfType<Toolbar>().Any());

        [Test]
        public void TestChangeToNonSkinnableScreen()
        {
            advanceToSongSelect();
            openSkinEditor();
            AddAssert("blueprint container present", () => skinEditor.ChildrenOfType<SkinBlueprintContainer>().Count(), () => Is.EqualTo(1));
            AddAssert("placeholder not present", () => skinEditor.ChildrenOfType<NonSkinnableScreenPlaceholder>().Count(), () => Is.Zero);
            AddAssert("editor sidebars not empty", () => skinEditor.ChildrenOfType<EditorSidebar>().SelectMany(sidebar => sidebar.Children).Count(), () => Is.GreaterThan(0));

            AddStep("add skinnable component", () =>
            {
                skinEditor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>().First().TriggerClick();
            });
            AddUntilStep("newly added component selected", () => skinEditor.SelectedComponents, () => Has.Count.EqualTo(1));

            AddStep("exit to main menu", () => Game.ScreenStack.CurrentScreen.Exit());
            AddAssert("selection cleared", () => skinEditor.SelectedComponents, () => Has.Count.Zero);
            AddAssert("blueprint container not present", () => skinEditor.ChildrenOfType<SkinBlueprintContainer>().Count(), () => Is.Zero);
            AddAssert("placeholder present", () => skinEditor.ChildrenOfType<NonSkinnableScreenPlaceholder>().Count(), () => Is.EqualTo(1));
            AddAssert("editor sidebars empty", () => skinEditor.ChildrenOfType<EditorSidebar>().SelectMany(sidebar => sidebar.Children).Count(), () => Is.Zero);

            advanceToSongSelect();
            AddAssert("blueprint container present", () => skinEditor.ChildrenOfType<SkinBlueprintContainer>().Count(), () => Is.EqualTo(1));
            AddAssert("placeholder not present", () => skinEditor.ChildrenOfType<NonSkinnableScreenPlaceholder>().Count(), () => Is.Zero);
            AddAssert("editor sidebars not empty", () => skinEditor.ChildrenOfType<EditorSidebar>().SelectMany(sidebar => sidebar.Children).Count(), () => Is.GreaterThan(0));
        }

        [Test]
        public void TestOpenSkinEditorGameplaySceneOnBeatmapWithNoObjects()
        {
            AddStep("set dummy beatmap", () => Game.Beatmap.SetDefault());
            advanceToSongSelect();

            AddStep("create empty beatmap", () => Game.BeatmapManager.CreateNew(new OsuRuleset().RulesetInfo, new GuestUser()));
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            openSkinEditor();
            switchToGameplayScene();
        }

        [Test]
        public void TestOpenSkinEditorGameplaySceneWhenDummyBeatmapActive()
        {
            AddStep("set dummy beatmap", () => Game.Beatmap.SetDefault());

            openSkinEditor();
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestOpenSkinEditorGameplaySceneWhenDifferentRulesetActive(int rulesetId)
        {
            BeatmapSetInfo beatmapSet = null!;

            AddStep("import beatmap", () => beatmapSet = BeatmapImportHelper.LoadQuickOszIntoOsu(Game).GetResultSafely());
            AddStep($"select difficulty for ruleset w/ ID {rulesetId}", () =>
            {
                var beatmap = beatmapSet.Beatmaps.First(b => b.Ruleset.OnlineID == rulesetId);
                Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(beatmap);
            });

            openSkinEditor();
            switchToGameplayScene();
        }

        [Test]
        public void TestRulesetInputDisabledWhenSkinEditorOpen()
        {
            advanceToSongSelect();
            openSkinEditor();

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            switchToGameplayScene();
            AddUntilStep("nested input disabled", () => ((Player)Game.ScreenStack.CurrentScreen).ChildrenOfType<PassThroughInputManager>().All(manager => !manager.UseParentInput));

            toggleSkinEditor();
            AddUntilStep("nested input enabled", () => ((Player)Game.ScreenStack.CurrentScreen).ChildrenOfType<PassThroughInputManager>().Any(manager => manager.UseParentInput));

            toggleSkinEditor();
            AddUntilStep("nested input disabled", () => ((Player)Game.ScreenStack.CurrentScreen).ChildrenOfType<PassThroughInputManager>().All(manager => !manager.UseParentInput));
        }

        [Test]
        public void TestSkinSavesOnChange()
        {
            advanceToSongSelect();
            openSkinEditor();

            Guid editedSkinId = Guid.Empty;
            AddStep("save skin id", () => editedSkinId = Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value.ID);

            // oily: song select's own pieces are always there to pick, so count blueprints instead of expecting none.
            int before = 0;
            int withAdded = 0;
            AddUntilStep("song select pieces shown", () => (before = skinEditor.ChildrenOfType<SkinBlueprint>().Count()) > 0);
            AddStep("add skinnable component", () =>
            {
                skinEditor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>().First().TriggerClick();
            });
            AddUntilStep("component added", () => (withAdded = skinEditor.ChildrenOfType<SkinBlueprint>().Count()) > before);

            AddStep("change to triangles skin", () => Game.Dependencies.Get<SkinManager>().SetSkinFromConfiguration(SkinInfo.TRIANGLES_SKIN.ToString()));
            AddUntilStep("components loaded", () => Game.ChildrenOfType<SkinnableContainer>().All(c => c.ComponentsLoaded));
            AddUntilStep("skin changed", () => skinEditor.ChildrenOfType<SkinBlueprint>().Count() < withAdded);

            AddStep("change back to modified skin", () => Game.Dependencies.Get<SkinManager>().SetSkinFromConfiguration(editedSkinId.ToString()));
            AddUntilStep("components loaded", () => Game.ChildrenOfType<SkinnableContainer>().All(c => c.ComponentsLoaded));
            AddUntilStep("changes saved", () => skinEditor.ChildrenOfType<SkinBlueprint>().Count() == withAdded);
        }

        private void advanceToSongSelect()
        {
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);
        }

        private void openSkinEditor()
        {
            toggleSkinEditor();
            AddUntilStep("skin editor loaded", () => skinEditor != null);
        }

        private void toggleSkinEditor()
        {
            AddStep("toggle skin editor", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.ShiftLeft);
                InputManager.Key(Key.S);
                InputManager.ReleaseKey(Key.ControlLeft);
                InputManager.ReleaseKey(Key.ShiftLeft);
            });
        }

        private void switchToGameplayScene()
        {
            AddStep("Click gameplay scene button", () =>
            {
                InputManager.MoveMouseTo(skinEditor.ChildrenOfType<SkinEditorSceneLibrary.SceneButton>().First(b => b.Text.ToString() == "Gameplay"));
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return Game.ScreenStack.CurrentScreen is Player;
            });
        }
    }
}
