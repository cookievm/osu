// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// oily (ilo, 2026-11-26): opens the ilystudios Discord server, in the Discord app if it's installed, otherwise in the browser.
    /// </summary>
    public partial class ToolbarDiscordButton : ToolbarButton
    {
        public const string INVITE_CODE = @"ilystudios";
        public const string WEB_LINK = @"https://discord.gg/" + INVITE_CODE;
        public const string APP_LINK = @"discord://-/invite/" + INVITE_CODE;

        protected override Anchor TooltipAnchor => Anchor.TopRight;

        [Resolved]
        private GameHost host { get; set; } = null!;

        /// <summary>
        /// Opens a link (replaceable in tests).
        /// </summary>
        public Action<string>? OpenLink;

        /// <summary>
        /// Whether the Discord app is installed (replaceable in tests).
        /// </summary>
        public Func<bool> DiscordInstalled = isDiscordInstalled;

        [BackgroundDependencyLoader]
        private void load()
        {
            TooltipMain = "Discord";
            TooltipSub = "Join the ilystudios Discord server";
            SetIcon(FontAwesome.Brands.Discord);
            Action = open;
        }

        private void open()
        {
            string link = DiscordInstalled() ? APP_LINK : WEB_LINK;

            if (OpenLink != null)
            {
                OpenLink(link);
                return;
            }

            if (link == APP_LINK && tryOpenApp())
                return;

            host.OpenUrlExternally(WEB_LINK);
        }

        private static bool tryOpenApp()
        {
            try
            {
                Process.Start(new ProcessStartInfo(APP_LINK) { UseShellExecute = true });
                return true;
            }
            catch (Exception e)
            {
                Logger.Log($"Couldn't open the Discord app ({e.Message}), opening the browser instead.");
                return false;
            }
        }

        /// <summary>
        /// The Discord app registers the discord:// link type when it's installed (only checked on Windows; elsewhere the browser link is used).
        /// </summary>
        private static bool isDiscordInstalled()
        {
            if (!OperatingSystem.IsWindows())
                return false;

            try
            {
                using var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"discord\shell\open\command");
                return key?.GetValue(null) is string command && !string.IsNullOrWhiteSpace(command);
            }
            catch
            {
                return false;
            }
        }
    }
}
