// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// oily: how the toolbars are laid out: how many there are, which screen edge each one sits on,
    /// and which buttons go on which toolbar, in what order. Saved as JSON in <see cref="Configuration.OsuSetting.OilyToolbarLayout"/>.
    /// </summary>
    public class ToolbarLayout
    {
        public const int MAX_BARS = 4;

        /// <summary>
        /// The toolbars, in order. Can be empty (no toolbar at all).
        /// </summary>
        public List<ToolbarBarLayout> Bars { get; set; } = new List<ToolbarBarLayout>();

        /// <summary>
        /// Where each button goes. A button that isn't listed is hidden.
        /// </summary>
        public List<ToolbarButtonLayout> Buttons { get; set; } = new List<ToolbarButtonLayout>();

        /// <summary>
        /// Lazer's layout, without News, Changelog and Wiki.
        /// </summary>
        public static ToolbarLayout CreateDefault()
        {
            var layout = new ToolbarLayout
            {
                Bars = { new ToolbarBarLayout { Edge = ToolbarEdge.Top } }
            };

            int order = 0;

            foreach (var item in new[] { ToolbarItem.Settings, ToolbarItem.Home })
                layout.Buttons.Add(new ToolbarButtonLayout { Item = item, Bar = 0, Side = ToolbarSide.Start, Order = order++ });

            layout.Buttons.Add(new ToolbarButtonLayout { Item = ToolbarItem.Rulesets, Bar = 0, Side = ToolbarSide.Middle, Order = order++ });

            foreach (var item in new[] { ToolbarItem.Rankings, ToolbarItem.BeatmapListing, ToolbarItem.Chat, ToolbarItem.Social, ToolbarItem.Music, ToolbarItem.User, ToolbarItem.Clock, ToolbarItem.Notifications })
                layout.Buttons.Add(new ToolbarButtonLayout { Item = item, Bar = 0, Side = ToolbarSide.End, Order = order++ });

            return layout;
        }

        /// <summary>
        /// Reads a saved layout (or the default one, if there isn't a valid one).
        /// </summary>
        public static ToolbarLayout Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return CreateDefault();

            try
            {
                var layout = JsonConvert.DeserializeObject<ToolbarLayout>(json);

                if (layout == null)
                    return CreateDefault();

                layout.Bars = layout.Bars.Take(MAX_BARS).ToList();

                foreach (var bar in layout.Bars)
                {
                    bar.Length = float.IsFinite(bar.Length) ? Math.Clamp(bar.Length, ToolbarBarLayout.MIN_LENGTH, 1) : 1;
                    bar.OffsetX = float.IsFinite(bar.OffsetX) ? Math.Clamp(bar.OffsetX, -1, 1) : 0;
                    bar.OffsetY = float.IsFinite(bar.OffsetY) ? Math.Clamp(bar.OffsetY, -1, 1) : 0;
                }

                layout.Buttons = layout.Buttons.Where(b => Enum.IsDefined(b.Item)).GroupBy(b => b.Item).Select(g => g.First()).ToList();
                return layout;
            }
            catch
            {
                return CreateDefault();
            }
        }

        public string Serialise() => JsonConvert.SerializeObject(this);

        /// <summary>
        /// Where a button goes, or <c>null</c> if it's hidden (or its toolbar doesn't exist).
        /// </summary>
        public ToolbarButtonLayout? Find(ToolbarItem item)
        {
            var button = Buttons.FirstOrDefault(b => b.Item == item);
            return button == null || button.Bar < 0 || button.Bar >= Bars.Count ? null : button;
        }

        /// <summary>
        /// The buttons on one side of a toolbar, in order.
        /// </summary>
        public IEnumerable<ToolbarItem> ItemsOn(int bar, ToolbarSide side)
            => Buttons.Where(b => b.Bar == bar && b.Side == side).OrderBy(b => b.Order).Select(b => b.Item);
    }

    public class ToolbarBarLayout
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ToolbarEdge Edge { get; set; }

        public const float MIN_LENGTH = 0.1f;

        /// <summary>
        /// How much of its edge the bar covers (1 = all of it). Shorter bars sit in the middle of the edge.
        /// </summary>
        public float Length { get; set; } = 1;

        /// <summary>
        /// How far the bar was dragged from its place, as a fraction of the screen's width and height. Can go past the screen's edges.
        /// </summary>
        public float OffsetX { get; set; }

        /// <inheritdoc cref="OffsetX"/>
        public float OffsetY { get; set; }

        /// <summary>
        /// Whether the bar was dragged away from its edge. A dragged bar floats over the screen instead of making room for itself.
        /// </summary>
        [JsonIgnore]
        public bool Floating => OffsetX != 0 || OffsetY != 0;
    }

    public class ToolbarButtonLayout
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ToolbarItem Item { get; set; }

        /// <summary>
        /// Which toolbar (from 0), or -1 for hidden.
        /// </summary>
        public int Bar { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public ToolbarSide Side { get; set; }

        /// <summary>
        /// Lower comes first (left to right, or top to bottom).
        /// </summary>
        public int Order { get; set; }
    }

    public enum ToolbarEdge
    {
        Top,
        Bottom,
        Left,
        Right,
    }

    public enum ToolbarSide
    {
        /// <summary>The left end (or the top end, on a sidebar).</summary>
        Start,

        /// <summary>The middle, which fills the space left over.</summary>
        Middle,

        /// <summary>The right end (or the bottom end, on a sidebar).</summary>
        End,
    }

    /// <summary>
    /// The buttons a toolbar can have. News, Changelog and Wiki were removed (ilo, 2026-11-25).
    /// </summary>
    public enum ToolbarItem
    {
        Settings,
        Home,

        [Description("Ruleset selector")]
        Rulesets,

        Rankings,

        [Description("Beatmap listing")]
        BeatmapListing,

        Chat,
        Social,

        [Description("Now playing")]
        Music,

        [Description("Profile")]
        User,

        Clock,
        Notifications,
    }
}
