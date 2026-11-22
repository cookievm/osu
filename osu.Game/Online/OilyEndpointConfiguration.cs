// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Oily.Identity;

namespace osu.Game.Online
{
    /// <summary>
    /// oily: points the client at the o!ily server instead of osu!'s.
    /// Accounts are o!ily accounts (the same as on o!ily for osu!stable).
    /// </summary>
    public class OilyEndpointConfiguration : EndpointConfiguration
    {
        public OilyEndpointConfiguration()
        {
            string api = OilyIdentity.ApiUrl;

            WebsiteUrl = OilyIdentity.WEBSITE_URL;
            APIUrl = api;
            APIClientID = "oily";
            APIClientSecret = "oily";
            SpectatorUrl = $@"{api}/spectator";
            MultiplayerUrl = $@"{api}/multiplayer";
            MetadataUrl = $@"{api}/metadata";
            BeatmapSubmissionServiceUrl = null;
            LivenessProbeUrl = null;
        }
    }
}
