// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Skinning;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: how song select's beatmap cards look. Set by the skin's "Beatmap List" component (<see cref="SongSelectBeatmapListLayout"/>).
    /// The defaults are lazer's look.
    /// </summary>
    public class SongSelectCardStyle
    {
        public const float DEFAULT_CORNER_RADIUS = Panel.CORNER_RADIUS;

        /// <summary>Cards come in from the left side instead of the right.</summary>
        public readonly BindableBool Flip = new BindableBool();

        /// <summary>Card rotation in degrees.</summary>
        public readonly BindableFloat Rotation = new BindableFloat();

        /// <summary>Card width, relative to the list's width.</summary>
        public readonly BindableFloat Width = new BindableFloat(1);

        /// <summary>Card height, relative to lazer's height.</summary>
        public readonly BindableFloat Height = new BindableFloat(1);

        public readonly BindableFloat CornerRadius = new BindableFloat(DEFAULT_CORNER_RADIUS);

        /// <summary>How visible the beatmap background picture on each card is.</summary>
        public readonly BindableFloat BackgroundOpacity = new BindableFloat(1);

        /// <summary>Tints the whole card. White keeps lazer's colours.</summary>
        public readonly Bindable<Colour4> Tint = new Bindable<Colour4>(Colour4.White);

        /// <summary>Shows the SS pp (with the selected mods) on each difficulty card.</summary>
        public readonly BindableBool ShowPerformance = new BindableBool(true);

        /// <summary>Shows each card's pretend score and its simulated pp.</summary>
        public readonly BindableBool ShowSimulatedPerformance = new BindableBool(true);

        public void Reset()
        {
            ShowPerformance.SetDefault();
            ShowSimulatedPerformance.SetDefault();
            Flip.SetDefault();
            Rotation.SetDefault();
            Width.SetDefault();
            Height.SetDefault();
            CornerRadius.SetDefault();
            BackgroundOpacity.SetDefault();
            Tint.SetDefault();
        }
    }

    /// <summary>
    /// oily: which skin components make sense on song select. Gameplay pieces (hit error meters, combo counters...) are left out of the
    /// skin editor's list there.
    /// </summary>
    public static class SongSelectSkinComponents
    {
        private static readonly Type[] allowed =
        {
            // oily: only song select's own parts (ilo, 2026-11-26: no sprites, boxes, texts or player info).
            typeof(SongSelectPieceLayout),
        };

        public static bool IsAllowed(Type type)
        {
            foreach (var t in allowed)
            {
                if (t.IsAssignableFrom(type))
                    return true;
            }

            return false;
        }
    }
}
