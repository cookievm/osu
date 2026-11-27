// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
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

                Height = 24;
                CornerRadius = 3;
                Margin = new MarginPadding();
                Foreground.Padding = new MarginPadding { Horizontal = 8, Vertical = 2 };
                Text.Font = OsuFont.GetFont(size: 17, weight: FontWeight.SemiBold);
                Chevron.Size = new Vector2(12);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Masking = true;
                BorderThickness = 2;
                BorderColour = owner.AccentColour;
            }

            protected override void Update()
            {
                base.Update();

                // Stable's boxes stay dark; hovering only darkens them a little more.
                Background.Colour = new Color4(0, 0, 0, owner.HeaderHidden ? 0 : IsHovered ? 0.8f : 0.55f);
                Chevron.Colour = Color4.White;
                Foreground.Alpha = owner.HeaderHidden ? 0 : 1;
                BorderThickness = owner.HeaderHidden ? 0 : 2;
            }
        }
    }
}
