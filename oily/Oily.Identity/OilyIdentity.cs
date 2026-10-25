// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace Oily.Identity
{
    /// <summary>
    /// Everything that makes the o!ily client a separate install from official osu!lazer.
    /// This is the only place the client's name lives; renaming the client means changing the values here.
    /// </summary>
    /// <remarks>
    /// <see cref="DATA_FOLDER_NAME"/> decides where beatmaps, skins, scores and settings are stored.
    /// Changing it after release will need a migration from the old folder.
    /// </remarks>
    public static class OilyIdentity
    {
        /// <summary>
        /// The client's display name.
        /// </summary>
        public const string CLIENT_NAME = "o!ily";

        // Folder, pipe and file names use "oily", since "!" is awkward in paths and command lines.
        private const string release_data_folder_name = "oily";
        private const string release_ipc_pipe_name = "oily";

#if DEBUG
        /// <summary>
        /// The name shown in the window title.
        /// </summary>
        public const string GAME_NAME = CLIENT_NAME + " (development)";

        /// <summary>
        /// The name of the folder (under the platform's app data folder) holding all user data.
        /// Never the same as official lazer's "osu" or "osu-development".
        /// </summary>
        public const string DATA_FOLDER_NAME = release_data_folder_name + "-development";

        /// <summary>
        /// The pipe used to hand files and links to an already running instance.
        /// Never the same as official lazer's, so opening o!ily never sends anything to official lazer and vice versa.
        /// </summary>
        public const string IPC_PIPE_NAME = release_ipc_pipe_name + "-debug";
#else
        public const string GAME_NAME = CLIENT_NAME;
        public const string DATA_FOLDER_NAME = release_data_folder_name;
        public const string IPC_PIPE_NAME = release_ipc_pipe_name;
#endif

        /// <summary>
        /// The legacy TCP IPC server lets osu!stable ask lazer for difficulty calculations.
        /// Only one process can listen on its port, so it stays off unless explicitly enabled,
        /// leaving the port to an official lazer running at the same time.
        /// </summary>
        public static bool LegacyIpcEnabled => System.Environment.GetEnvironmentVariable("OILY_ENABLE_LEGACY_IPC") == "1";

        /// <summary>
        /// Discord Rich Presence uses ppy's Discord application, which would show o!ily as "osu!".
        /// Off until o!ily has its own Discord application.
        /// </summary>
        public static bool DiscordRichPresenceEnabled => false;
    }
}
