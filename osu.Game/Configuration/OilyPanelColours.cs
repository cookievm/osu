// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace osu.Game.Configuration
{
    /// <summary>
    /// oily: the colour and see-through-ness of each panel (toolbars, song search, map details...).
    /// Saved as JSON in <see cref="OsuSetting.OilyPanelColours"/>.
    /// </summary>
    public class OilyPanelColours
    {
        public Dictionary<OilyPanel, OilyPanelStyle> Panels { get; set; } = new Dictionary<OilyPanel, OilyPanelStyle>();

        /// <summary>
        /// Colours of single things (buttons, icons) as hex. Missing means lazer's colour.
        /// </summary>
        public Dictionary<OilyColour, string> Colours { get; set; } = new Dictionary<OilyColour, string>();

        public OilyPanelStyle Get(OilyPanel panel) => Panels.TryGetValue(panel, out var style) ? style : new OilyPanelStyle();

        public Colour4? GetColour(OilyColour item) => Colours.TryGetValue(item, out string? hex) && Colour4.TryParseHex(hex, out var c) ? c : null;

        public static OilyPanelColours Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new OilyPanelColours();

            try
            {
                return JsonConvert.DeserializeObject<OilyPanelColours>(json) ?? new OilyPanelColours();
            }
            catch
            {
                return new OilyPanelColours();
            }
        }

        public string Serialise() => JsonConvert.SerializeObject(this);

        /// <summary>
        /// Calls <paramref name="apply"/> now and whenever a panel's style changes. Keep the returned bindable in a field, or it stops working.
        /// </summary>
        public static IBindable<string> Track(OsuConfigManager config, OilyPanel panel, Action<OilyPanelStyle> apply)
        {
            var setting = config.GetBindable<string>(OsuSetting.OilyPanelColours);
            setting.BindValueChanged(v => apply(Parse(v.NewValue).Get(panel)), true);
            return setting;
        }

        /// <summary>
        /// Calls <paramref name="apply"/> now and whenever any colour changes. Keep the returned bindable in a field, or it stops working.
        /// </summary>
        public static IBindable<string> Track(OsuConfigManager config, Action<OilyPanelColours> apply)
        {
            var setting = config.GetBindable<string>(OsuSetting.OilyPanelColours);
            setting.BindValueChanged(v => apply(Parse(v.NewValue)), true);
            return setting;
        }
    }

    public class OilyPanelStyle
    {
        /// <summary>
        /// The panel's colour as hex (#rrggbb), or <c>null</c> for lazer's colour.
        /// </summary>
        public string? Colour { get; set; }

        /// <summary>
        /// 1 is lazer's look, 0 is fully see-through.
        /// </summary>
        public float Opacity { get; set; } = 1;

        [JsonIgnore]
        public Colour4? CustomColour => Colour != null && Colour4.TryParseHex(Colour, out var c) ? c : null;
    }

    public enum OilyPanel
    {
        [Description("Toolbars")]
        Toolbar,

        [Description("Song select bottom bar")]
        BottomBar,

        [Description("Song search")]
        SongSearch,

        [Description("Map title")]
        MapTitle,

        [Description("Map difficulty")]
        MapDifficulty,

        [Description("Map details")]
        MapDetails,

        [Description("Song select leaderboard")]
        Leaderboard,
    }

    /// <summary>
    /// oily: single things whose colour can be changed.
    /// </summary>
    public enum OilyColour
    {
        [Description("Back button")]
        BackButton,

        [Description("Mods button icon")]
        ModsIcon,

        [Description("Mods button light bar")]
        ModsBar,

        [Description("Random button icon")]
        RandomIcon,

        [Description("Random button light bar")]
        RandomBar,

        [Description("Options button icon")]
        OptionsIcon,

        [Description("Options button light bar")]
        OptionsBar,

        [Description("Toolbar icons")]
        ToolbarIcons,

        [Description("Gamemode icon (selected)")]
        GamemodeSelected,

        [Description("Gamemode icons (not selected)")]
        GamemodeOthers,
    }
}
