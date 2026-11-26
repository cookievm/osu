// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Toolbar;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// oily: clicking a toolbar button again closes its overlay.
    /// </summary>
    public partial class TestSceneToolbarToggle : OsuGameTestScene
    {
        [Test]
        public void TestNotificationsButtonClosesOverlay()
        {
            AddUntilStep("wait for toolbar", () => Game.Toolbar.IsLoaded);
            // oily: the main menu has no toolbar, so use a plain screen that shows it.
            PushAndConfirm(() => new TestSceneScreenNavigation.ToolbarTestScreen());
            AddUntilStep("toolbar shown", () => Game.Toolbar.State.Value == Visibility.Visible);

            AddStep("click notifications", () => clickButton<ToolbarNotificationButton>());
            AddUntilStep("notifications open", () => Game.ChildrenOfType<NotificationOverlay>().Single().State.Value == Visibility.Visible);

            AddWaitStep("wait", 3);
            AddStep("click notifications again", () => clickButton<ToolbarNotificationButton>());
            AddUntilStep("notifications closed", () => Game.ChildrenOfType<NotificationOverlay>().Single().State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestSettingsButtonClosesOverlay()
        {
            AddUntilStep("wait for toolbar", () => Game.Toolbar.IsLoaded);
            // oily: the main menu has no toolbar, so use a plain screen that shows it.
            PushAndConfirm(() => new TestSceneScreenNavigation.ToolbarTestScreen());
            AddUntilStep("toolbar shown", () => Game.Toolbar.State.Value == Visibility.Visible);

            // oily: the fixed toolbar hides the settings button, so bring back lazer's layout for this one.
            AddStep("show settings button", () => Game.LocalConfig.SetValue(OsuSetting.OilyToolbarLayout, ToolbarLayout.CreateDefault().Serialise()));
            AddUntilStep("wait for settings button", () => Game.Toolbar.ChildrenOfType<ToolbarSettingsButton>().Any(b => b.IsPresent));
            AddStep("click settings", () => clickButton<ToolbarSettingsButton>());
            AddUntilStep("settings open", () => Game.Settings.State.Value == Visibility.Visible);

            AddWaitStep("wait", 3);
            AddStep("click settings again", () => clickButton<ToolbarSettingsButton>());
            AddUntilStep("settings closed", () => Game.Settings.State.Value == Visibility.Hidden);
        }

        private void clickButton<T>() where T : ToolbarButton
        {
            InputManager.MoveMouseTo(Game.Toolbar.ChildrenOfType<T>().First());
            InputManager.Click(MouseButton.Left);
        }
    }
}
