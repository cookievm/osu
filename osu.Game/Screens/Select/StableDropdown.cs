// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily (ilo, 2026-11-27): a dropdown drawn like osu!stable's song select ones ("By Difficulty", "Global Ranking"):
    /// a thin box with a coloured outline, white text and an arrow.
    /// </summary>
    public partial class StableDropdown<T> : OsuDropdown<T>
    {
        /// <summary>
        /// How an item is written. Without it, lazer's usual text is used.
        /// </summary>
        public Func<T, LocalisableString>? TextFor { get; init; }

        /// <summary>
        /// The outline's colour.
        /// </summary>
        public Color4 AccentColour { get; init; } = new Color4(113, 200, 255, 255);

        /// <summary>
        /// Another dropdown whose items this one shows (their items can change, with the ruleset or collections).
        /// </summary>
        public Dropdown<T>? MirrorItemsOf { get; init; }

        /// <summary>
        /// Draw no box, only keep it clickable (when the skin's picture already shows the button).
        /// </summary>
        public bool HeaderHidden { get; set; }

        /// <summary>
        /// The box's height (stable's are about 23 high).
        /// </summary>
        public float HeaderHeight { get; init; } = 23;

        /// <summary>
        /// The text size, in stable's font.
        /// </summary>
        public float FontSize { get; init; } = StableFont.DROPDOWN_SIZE;

        /// <summary>
        /// The outline's thickness.
        /// </summary>
        public float BorderWidth { get; init; } = 1.2f;

        protected override LocalisableString GenerateItemText(T item) => TextFor?.Invoke(item) ?? base.GenerateItemText(item);

        protected override DropdownHeader CreateHeader() => new StableHeader(this);

        protected override void Update()
        {
            base.Update();

            if (MirrorItemsOf != null && !Items.SequenceEqual(MirrorItemsOf.Items))
                Items = MirrorItemsOf.Items.ToArray();
        }

        private partial class StableHeader : OsuDropdownHeader
        {
            private readonly StableDropdown<T> owner;

            public StableHeader(StableDropdown<T> owner)
            {
                this.owner = owner;
                Dropdown = owner;

                Height = owner.HeaderHeight;
                CornerRadius = 2;
                Margin = new MarginPadding();
                Foreground.Padding = new MarginPadding { Left = 3, Right = 8 };
                Text.Font = StableFont.Get(owner.FontSize);
                Chevron.Size = new Vector2(owner.HeaderHeight * 0.45f);
                Chevron.Icon = FontAwesome.Solid.ChevronDown;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Masking = true;
                BorderThickness = owner.BorderWidth;
                BorderColour = owner.AccentColour;
            }

            protected override void Update()
            {
                base.Update();

                // Stable's boxes stay dark; hovering only darkens them a little more.
                Background.Colour = new Color4(0, 0, 0, owner.HeaderHidden ? 0 : IsHovered ? 0.75f : 0.5f);
                Chevron.Colour = Color4.White;
                Foreground.Alpha = owner.HeaderHidden ? 0 : 1;
                BorderThickness = owner.HeaderHidden ? 0 : owner.BorderWidth;
            }
        }
    }
}
