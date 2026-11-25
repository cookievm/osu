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
        /// The o!ily server's domain. The website lives here and the lazer API on the "lazer." subdomain.
        /// </summary>
        public const string SERVER_DOMAIN = "oily.ilystudios.net";

        /// <summary>
        /// The website, used for profile and beatmap links.
        /// </summary>
        public const string WEBSITE_URL = "https://" + SERVER_DOMAIN;

        /// <summary>
        /// The o!ily lazer server. Can be pointed elsewhere for testing with the OILY_API_URL environment variable.
        /// </summary>
        public static string ApiUrl => System.Environment.GetEnvironmentVariable("OILY_API_URL") is string url && url.Length > 0
            ? url.TrimEnd('/')
            : "https://lazer." + SERVER_DOMAIN;

        /// <summary>
        /// Whether a web address belongs to the o!ily server (so its images may be loaded, like avatars).
        /// </summary>
        public static bool IsOilyHost(string host) =>
            host.Equals(SERVER_DOMAIN, System.StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + SERVER_DOMAIN, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Discord Rich Presence uses ppy's Discord application, which would show o!ily as "osu!".
        /// Off until o!ily has its own Discord application.
        /// </summary>
        public static bool DiscordRichPresenceEnabled => false;

        /// <summary>
        /// Added to the client version saved with every score played in o!ily.
        /// o!ily plays osu! standard with stable's notelock, so replays carrying this tag are played back with it,
        /// while replays from official lazer (renders) are played back without it.
        /// </summary>
        public const string STABLE_NOTELOCK_VERSION_TAG = "-oily-notelock";

        public static string TagClientVersion(string version) =>
            version.EndsWith(STABLE_NOTELOCK_VERSION_TAG, System.StringComparison.Ordinal) ? version : version + STABLE_NOTELOCK_VERSION_TAG;

        public static bool PlayedWithStableNoteLock(string? clientVersion) =>
            clientVersion?.EndsWith(STABLE_NOTELOCK_VERSION_TAG, System.StringComparison.Ordinal) == true;
    }
}
