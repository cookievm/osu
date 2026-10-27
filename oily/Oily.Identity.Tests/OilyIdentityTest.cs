// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;

namespace Oily.Identity.Tests
{
    [TestFixture]
    public class OilyIdentityTest
    {
        /// <summary>
        /// Data folder names used by official osu!lazer (release, debug and multi-client debug builds).
        /// </summary>
        private static readonly string[] official_data_folders = { "osu", "osu-development" };

        /// <summary>
        /// Pipe names used by official osu!lazer.
        /// </summary>
        private static readonly string[] official_ipc_pipes = { "osu-lazer", "osu-lazer-debug" };

        [Test]
        public void TestDataFolderIsSeparateFromOfficialLazer()
        {
            foreach (string official in official_data_folders)
            {
                Assert.That(OilyIdentity.DATA_FOLDER_NAME, Is.Not.EqualTo(official).IgnoreCase);
                Assert.That(OilyIdentity.DATA_FOLDER_NAME, Does.Not.StartWith(official + "-").IgnoreCase);
            }
        }

        [Test]
        public void TestIpcPipeIsSeparateFromOfficialLazer()
        {
            Assert.That(official_ipc_pipes, Has.No.Member(OilyIdentity.IPC_PIPE_NAME));
        }

        [Test]
        public void TestGameNameDoesNotUseOsuBranding()
        {
            Assert.That(OilyIdentity.GAME_NAME, Does.Not.Contain("osu!").IgnoreCase);
        }
    }
}
