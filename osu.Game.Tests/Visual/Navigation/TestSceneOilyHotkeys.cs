// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Overlays.Mods;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Allocation;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Toolbar;
using osu.Game.Screens.Select;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// oily (ilo, 2026-11-26): lazer's hotkeys for the toolbar popups still work.
    /// </summary>
    public partial class TestSceneOilyHotkeys : OsuGameTestScene
    {
        private static IEnumerable<(string name, Key[] keys)> hotkeys()
        {
            yield return ("settings", new[] { Key.ControlLeft, Key.O });
            yield return ("chat", new[] { Key.F8 });
            yield return ("now playing", new[] { Key.F6 });
            yield return ("social", new[] { Key.F9 });
            yield return ("beatmap listing", new[] { Key.ControlLeft, Key.B });
            yield return ("notifications", new[] { Key.ControlLeft, Key.N });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestToolbarHotkeys(bool songSelect)
        {
            if (songSelect)
                PushAndConfirm(() => new SoloSongSelect());

            foreach (var (name, keys) in hotkeys())
            {
                AddStep($"press {name} hotkey", () => press(keys));
                AddUntilStep($"{name} open", () => overlayFor(name).State.Value, () => Is.EqualTo(Visibility.Visible));
                AddStep($"press {name} hotkey again", () => press(keys));
                AddUntilStep($"{name} closed", () => overlayFor(name).State.Value, () => Is.EqualTo(Visibility.Hidden));
            }

            AddAssert("still on the same screen", () => Game.ScreenStack.CurrentScreen is osu.Game.Screens.Select.SongSelect, () => Is.EqualTo(songSelect));
        }

        [Test]
        public void TestHotkeysWorkWithButtonsHiddenOrOnBottomBar()
        {
            AddStep("hide settings and chat buttons, put notifications on the bottom bar", () =>
            {
                var layout = ToolbarLayout.CreateDefault();
                foreach (var b in layout.Buttons)
                {
                    if (b.Item == ToolbarItem.Settings || b.Item == ToolbarItem.Chat) b.Bar = -1;
                    if (b.Item == ToolbarItem.Notifications) b.Bar = -2;
                }

                Game.LocalConfig.SetValue(OsuSetting.OilyToolbarLayout, layout.Serialise());
            });

            PushAndConfirm(() => new SoloSongSelect());

            foreach (var (name, keys) in hotkeys())
            {
                AddStep($"press {name} hotkey", () => press(keys));
                AddUntilStep($"{name} open", () => overlayFor(name).State.Value, () => Is.EqualTo(Visibility.Visible));
                AddStep($"press {name} hotkey again", () => press(keys));
                AddUntilStep($"{name} closed", () => overlayFor(name).State.Value, () => Is.EqualTo(Visibility.Hidden));
            }

            AddStep("put the toolbar back", () => Game.LocalConfig.SetValue(OsuSetting.OilyToolbarLayout, string.Empty));
        }

        [Test]
        public void TestMapDetailsCentredInBottomBar()
        {
            PushAndConfirm(() => new SoloSongSelect());
            AddStep("import beatmap", () => osu.Game.Tests.Beatmaps.IO.BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);
            AddStep("open mods", () => press(new[] { Key.F1 }));
            AddUntilStep("map details shown", () => Game.ChildrenOfType<BeatmapAttributesDisplay>().SingleOrDefault()?.Alpha == 1);
            AddUntilStep("map details centred between Mods and the ranked display", () =>
            {
                var details = Game.ChildrenOfType<BeatmapAttributesDisplay>().Single().ScreenSpaceDrawQuad;
                float left = Game.ChildrenOfType<FooterButtonMods>().Single().ScreenSpaceDrawQuad.TopRight.X;
                float right = Game.ChildrenOfType<RankingInformationDisplay>().Single().ScreenSpaceDrawQuad.TopLeft.X;
                return details.TopLeft.X > left && Math.Abs((details.TopLeft.X - left) - (right - details.TopRight.X)) < 60;
            });
        }

        private VisibilityContainer overlayFor(string name)
        {
            switch (name)
            {
                case "settings": return Game.Settings;
                case "chat": return Game.Dependencies.Get<ChatOverlay>();
                case "now playing": return Game.Dependencies.Get<NowPlayingOverlay>();
                case "social": return Game.Dependencies.Get<DashboardOverlay>();
                case "beatmap listing": return Game.Dependencies.Get<BeatmapListingOverlay>();
                default: return Game.Notifications;
            }
        }

        private void press(Key[] keys)
        {
            foreach (var k in keys) InputManager.PressKey(k);
            foreach (var k in keys) InputManager.ReleaseKey(k);
        }
    }
}
