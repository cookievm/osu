// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Screens.Select;

namespace osu.Game.Screens.Footer
{
    /// <summary>
    /// oily: how the bottom bar (song select footer) looks: shown or not, which side the back button is on,
    /// and which buttons it has in what order. Saved as JSON in <see cref="Configuration.OsuSetting.OilyFooterLayout"/>.
    /// </summary>
    public class FooterLayout
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public FooterBarMode Bar { get; set; } = FooterBarMode.Shown;

        [JsonConverter(typeof(StringEnumConverter))]
        public FooterBackSide BackButton { get; set; } = FooterBackSide.Left;

        public const float MIN_SIZE = 0.5f;
        public const float MAX_SIZE = 1.5f;

        /// <summary>
        /// How big the bar and its buttons are (1 = lazer's size).
        /// </summary>
        public float Size { get; set; } = 1;

        /// <summary>
        /// How far the back button was dragged from its place, in the bar's own units.
        /// </summary>
        public float BackOffsetX { get; set; }

        /// <inheritdoc cref="BackOffsetX"/>
        public float BackOffsetY { get; set; }

        /// <summary>
        /// How far the toolbar buttons moved onto the bar were dragged from their place, in the bar's own units.
        /// </summary>
        public float ToolbarButtonsOffsetX { get; set; }

        /// <inheritdoc cref="ToolbarButtonsOffsetX"/>
        public float ToolbarButtonsOffsetY { get; set; }

        /// <summary>
        /// The buttons in order. Buttons that aren't one of these keep their place.
        /// </summary>
        public List<FooterButtonLayout> Buttons { get; set; } = new List<FooterButtonLayout>();

        /// <summary>
        /// oily (ilo, 2026-11-26): the bottom bar everyone gets: ilo's own layout from 2026-11-26. Players can't change it in game;
        /// we change it here. Loading the config always puts this back (see <see cref="Configuration.OsuConfigManager"/>).
        /// </summary>
        public const string FIXED = @"{""Bar"":""Shown"",""BackButton"":""Left"",""Size"":0.8,""BackOffsetX"":-5.3837585,""BackOffsetY"":-67.6239,""ToolbarButtonsOffsetX"":0.0,""ToolbarButtonsOffsetY"":0.0,""Buttons"":[{""Item"":""Mods"",""Visible"":true,""OffsetX"":-274.9633,""OffsetY"":-6.219116},{""Item"":""FreeMods"",""Visible"":true,""OffsetX"":0.0,""OffsetY"":0.0},{""Item"":""Freestyle"",""Visible"":true,""OffsetX"":0.0,""OffsetY"":0.0},{""Item"":""Random"",""Visible"":true,""OffsetX"":-278.11865,""OffsetY"":-7.033325},{""Item"":""Options"",""Visible"":true,""OffsetX"":-281.05988,""OffsetY"":-7.989502},{""Item"":""PerformanceSimulator"",""Visible"":true,""OffsetX"":-284.0029,""OffsetY"":-8.419922}]}";

        public static FooterLayout CreateDefault() => new FooterLayout
        {
            Buttons = Enum.GetValues<FooterItem>().Select(i => new FooterButtonLayout { Item = i, Visible = true }).ToList(),
        };

        public static FooterLayout Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return CreateDefault();

            try
            {
                var layout = JsonConvert.DeserializeObject<FooterLayout>(json);

                if (layout == null)
                    return CreateDefault();

                layout.Size = float.IsFinite(layout.Size) ? Math.Clamp(layout.Size, MIN_SIZE, MAX_SIZE) : 1;
                layout.BackOffsetX = float.IsFinite(layout.BackOffsetX) ? layout.BackOffsetX : 0;
                layout.BackOffsetY = float.IsFinite(layout.BackOffsetY) ? layout.BackOffsetY : 0;
                layout.ToolbarButtonsOffsetX = float.IsFinite(layout.ToolbarButtonsOffsetX) ? layout.ToolbarButtonsOffsetX : 0;
                layout.ToolbarButtonsOffsetY = float.IsFinite(layout.ToolbarButtonsOffsetY) ? layout.ToolbarButtonsOffsetY : 0;
                layout.Buttons = layout.Buttons.Where(b => Enum.IsDefined(b.Item)).GroupBy(b => b.Item).Select(g => g.First()).ToList();

                // oily: buttons and the bar can't be hidden any more (ilo, 2026-11-26: no one needs to remove them), so older layouts show everything again.
                layout.Bar = FooterBarMode.Shown;
                foreach (var button in layout.Buttons)
                    button.Visible = true;

                // Buttons added in later versions go at the end.
                foreach (var item in Enum.GetValues<FooterItem>().Where(i => layout.Buttons.All(b => b.Item != i)))
                    layout.Buttons.Add(new FooterButtonLayout { Item = item, Visible = true });

                return layout;
            }
            catch
            {
                return CreateDefault();
            }
        }

        public string Serialise() => JsonConvert.SerializeObject(this);

        /// <summary>
        /// Which <see cref="FooterItem"/> a button is, or <c>null</c> for buttons that can't be moved.
        /// </summary>
        public static FooterItem? ItemOf(ScreenFooterButton button)
        {
            switch (button)
            {
                case FooterButtonMods:
                    return FooterItem.Mods;

                case FooterButtonRandom:
                    return FooterItem.Random;

                case FooterButtonOptions:
                    return FooterItem.Options;

                case FooterButtonPerformanceSimulator:
                    return FooterItem.PerformanceSimulator;

                case FooterButtonFreeMods:
                    return FooterItem.FreeMods;

                case FooterButtonFreestyle:
                    return FooterItem.Freestyle;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Puts a screen's buttons in the saved order, leaving out hidden ones.
        /// </summary>
        public IReadOnlyList<ScreenFooterButton> Arrange(IReadOnlyList<ScreenFooterButton> buttons)
        {
            int indexOf(FooterItem item)
            {
                int index = Buttons.FindIndex(b => b.Item == item);
                return index < 0 ? int.MaxValue : index;
            }

            bool visible(FooterItem item) => Buttons.FirstOrDefault(b => b.Item == item)?.Visible != false;

            // Buttons this layout knows swap places among themselves; other buttons stay where the screen put them.
            var known = new Queue<ScreenFooterButton>(buttons.Where(b => ItemOf(b) != null).OrderBy(b => indexOf(ItemOf(b)!.Value)));
            var result = new List<ScreenFooterButton>();

            foreach (var button in buttons)
            {
                var next = ItemOf(button) == null ? button : known.Dequeue();

                if (ItemOf(next) is not FooterItem item || visible(item))
                    result.Add(next);
            }

            return result;
        }
    }

    public class FooterButtonLayout
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public FooterItem Item { get; set; }

        public bool Visible { get; set; } = true;

        /// <summary>
        /// How far the button was dragged from its place in the bar, in the bar's own units (lazer's bar is 50 tall).
        /// </summary>
        public float OffsetX { get; set; }

        /// <inheritdoc cref="OffsetX"/>
        public float OffsetY { get; set; }
    }

    public enum FooterBarMode
    {
        Shown,

        [Description("Buttons only (no background)")]
        ButtonsOnly,

        Hidden,
    }

    public enum FooterBackSide
    {
        Left,
        Right,
    }

    public enum FooterItem
    {
        // The order is the default order (lazer puts the multiplayer buttons next to Mods).
        Mods,

        [Description("Free mods (multiplayer)")]
        FreeMods,

        [Description("Freestyle (multiplayer)")]
        Freestyle,

        Random,
        Options,

        [Description("PP Simulator")]
        PerformanceSimulator,
    }
}
