// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: one part of a song select piece whose colours a skin changes (a search box, a dropdown, the panel behind it...).
    /// </summary>
    public class OilyRecolourZone
    {
        public required Drawable Root { get; init; }

        /// <summary>Tints everything in the part that isn't text or an icon. <c>null</c> keeps lazer's colours.</summary>
        public Colour4? Colour { get; init; }

        /// <summary>Colour of the part's text and icons. <c>null</c> keeps lazer's colours (or the surrounding part's text colour).</summary>
        public Colour4? Text { get; init; }

        /// <summary>1 keeps lazer's look, 0 is fully see-through.</summary>
        public float Opacity { get; init; } = 1;

        public bool IsDefault => Colour == null && Text == null && Opacity >= 1;
    }

    /// <summary>
    /// oily: recolours the drawables inside a song select piece without fighting lazer's own colour changes (hover, focus...).
    /// lazer's latest colour is remembered and the skin's colour is applied on top of it every time.
    /// </summary>
    public static class OilyRecolour
    {
        private class State
        {
            public bool HasColour;
            public ColourInfo OriginalColour;
            public ColourInfo SetColour;

            public bool HasAlpha;
            public float OriginalAlpha;
            public float SetAlpha;
        }

        private static readonly ConditionalWeakTable<Drawable, State> states = new ConditionalWeakTable<Drawable, State>();

        /// <summary>
        /// Applies <paramref name="zones"/> to everything under <paramref name="root"/>. The first zone should be <paramref name="root"/> itself.
        /// Drawables inside another <see cref="SongSelectPieceContainer"/> are left alone (that piece has its own layout).
        /// </summary>
        public static void Apply(Drawable root, IReadOnlyList<OilyRecolourZone> zones)
        {
            var byRoot = new Dictionary<Drawable, OilyRecolourZone>();

            foreach (var zone in zones)
            {
                // The first zone for a drawable wins (parts with their own settings come before shared ones).
                if (byRoot.TryAdd(zone.Root, zone))
                    setAlpha(zone.Root, zone.Opacity >= 1 ? null : zone.Opacity);
            }

            foreach (var d in root.ChildrenOfType<Drawable>())
            {
                bool isText = d is SpriteText || d is SpriteIcon;

                if (!isText && d is CompositeDrawable)
                    continue;

                Colour4? colour = null;
                Colour4? text = null;
                bool foundColourZone = false;
                bool otherPiece = false;

                for (Drawable? p = d; p != null; p = p.Parent)
                {
                    if (p != root && p is SongSelectPieceContainer)
                    {
                        otherPiece = true;
                        break;
                    }

                    if (byRoot.TryGetValue(p, out var zone))
                    {
                        if (!foundColourZone)
                        {
                            colour = zone.Colour;
                            foundColourZone = true;
                        }

                        text ??= zone.Text;
                    }

                    if (p == root)
                        break;
                }

                if (otherPiece)
                    continue;

                setColour(d, isText ? text : colour, isText);
            }
        }

        private static void setColour(Drawable d, Colour4? target, bool replace)
        {
            states.TryGetValue(d, out var state);

            if (target == null)
            {
                if (state?.HasColour == true)
                {
                    if (d.Colour.Equals(state.SetColour))
                        d.Colour = state.OriginalColour;
                    state.HasColour = false;
                }

                return;
            }

            state ??= states.GetOrCreateValue(d);

            if (!state.HasColour || !d.Colour.Equals(state.SetColour))
                state.OriginalColour = d.Colour;

            var original = state.OriginalColour;
            d.Colour = replace ? replaced(original, target.Value) : multiplied(original, target.Value);
            state.SetColour = d.Colour;
            state.HasColour = true;
        }

        private static void setAlpha(Drawable d, float? opacity)
        {
            states.TryGetValue(d, out var state);

            if (opacity == null)
            {
                if (state?.HasAlpha == true)
                {
                    if (d.Alpha == state.SetAlpha)
                        d.Alpha = state.OriginalAlpha;
                    state.HasAlpha = false;
                }

                return;
            }

            state ??= states.GetOrCreateValue(d);

            if (!state.HasAlpha || d.Alpha != state.SetAlpha)
                state.OriginalAlpha = d.Alpha;

            d.Alpha = state.OriginalAlpha * opacity.Value;
            state.SetAlpha = d.Alpha;
            state.HasAlpha = true;
        }

        private static ColourInfo multiplied(ColourInfo c, Colour4 tint) => new ColourInfo
        {
            TopLeft = c.TopLeft.SRGB * tint,
            TopRight = c.TopRight.SRGB * tint,
            BottomLeft = c.BottomLeft.SRGB * tint,
            BottomRight = c.BottomRight.SRGB * tint,
        };

        // Text keeps its own see-through-ness (fading placeholders and the like).
        private static ColourInfo replaced(ColourInfo c, Colour4 colour) => new ColourInfo
        {
            TopLeft = colour.Opacity(colour.A * c.TopLeft.Alpha),
            TopRight = colour.Opacity(colour.A * c.TopRight.Alpha),
            BottomLeft = colour.Opacity(colour.A * c.BottomLeft.Alpha),
            BottomRight = colour.Opacity(colour.A * c.BottomRight.Alpha),
        };

        /// <summary>
        /// The skin colour to use: white means "keep lazer's".
        /// </summary>
        public static Colour4? Custom(Colour4 colour) => colour == Colour4.White ? null : colour;

        public static bool AnyCustom(IEnumerable<OilyRecolourZone> zones) => zones.Any(z => !z.IsDefault);
    }
}
