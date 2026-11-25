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

        /// <summary>
        /// The buttons in order. Buttons that aren't one of these keep their place.
        /// </summary>
        public List<FooterButtonLayout> Buttons { get; set; } = new List<FooterButtonLayout>();

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

                layout.Buttons = layout.Buttons.Where(b => Enum.IsDefined(b.Item)).GroupBy(b => b.Item).Select(g => g.First()).ToList();

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
        Mods,
        Random,
        Options,

        [Description("Free mods (multiplayer)")]
        FreeMods,

        [Description("Freestyle (multiplayer)")]
        Freestyle,
    }
}
