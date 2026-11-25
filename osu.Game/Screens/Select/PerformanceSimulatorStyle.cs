// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: colours of the Universal PP Simulator's sliders and buttons. White keeps lazer's colour.
    /// </summary>
    public class PerformanceSimulatorStyle
    {
        public readonly Bindable<Colour4> AccuracyBar = new Bindable<Colour4>(Colour4.White);
        public readonly Bindable<Colour4> MissesBar = new Bindable<Colour4>(Colour4.White);
        public readonly Bindable<Colour4> SaveButton = new Bindable<Colour4>(Colour4.White);
        public readonly Bindable<Colour4> UseForAllButton = new Bindable<Colour4>(Colour4.White);
        public readonly Bindable<Colour4> DefaultButton = new Bindable<Colour4>(Colour4.White);
        public readonly Bindable<Colour4> CancelButton = new Bindable<Colour4>(Colour4.White);

        /// <summary>
        /// The colour to use, or <c>null</c> for lazer's colour (white means "not changed").
        /// </summary>
        public static Colour4? Custom(Colour4 colour) => colour == Colour4.White ? null : colour;

        public void Reset()
        {
            AccuracyBar.SetDefault();
            MissesBar.SetDefault();
            SaveButton.SetDefault();
            UseForAllButton.SetDefault();
            DefaultButton.SetDefault();
            CancelButton.SetDefault();
        }
    }

    /// <summary>
    /// oily: skin editor component (song select only) that colours the Universal PP Simulator's sliders and its four buttons.
    /// It shows as a small label in the skin editor and in song select only while editing the skin.
    /// </summary>
    public partial class PpSimulatorColours : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource("Accuracy bar colour", "The accuracy slider. White keeps lazer's colour.")]
        public BindableColour4 AccuracyBar { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Misses bar colour", "The misses slider. White keeps lazer's colour.")]
        public BindableColour4 MissesBar { get; } = new BindableColour4(Colour4.White);

        [SettingSource("\"Save for this beatmap\" colour", "White keeps lazer's colour.")]
        public BindableColour4 SaveButton { get; } = new BindableColour4(Colour4.White);

        [SettingSource("\"Use for all beatmaps\" colour", "White keeps lazer's colour.")]
        public BindableColour4 UseForAllButton { get; } = new BindableColour4(Colour4.White);

        [SettingSource("\"Go back to the default\" colour", "White keeps lazer's colour.")]
        public BindableColour4 DefaultButton { get; } = new BindableColour4(Colour4.White);

        [SettingSource("\"Cancel\" colour", "White keeps lazer's colour.")]
        public BindableColour4 CancelButton { get; } = new BindableColour4(Colour4.White);

        [Resolved]
        private SongSelectPieceRegistry registry { get; set; } = null!;

        private bool inControl;

        public PpSimulatorColours()
        {
            AutoSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChild = new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 5,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Gray2,
                        Alpha = 0.8f,
                    },
                    new OsuSpriteText
                    {
                        Margin = new MarginPadding { Horizontal = 10, Vertical = 5 },
                        Text = "PP Simulator colours",
                        Font = OsuFont.Torus.With(size: 14, weight: FontWeight.Bold),
                        Colour = colours.Yellow,
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Only the one placed in song select's skin layer counts (the skin editor's list also makes one as a preview).
            for (var p = Parent; p != null; p = p.Parent)
            {
                if (p is SkinnableContainer)
                {
                    inControl = true;
                    break;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!inControl)
                return;

            // Only visible while editing the skin; it is a settings holder, not part of the screen.
            Alpha = skinEditor?.State.Value == Visibility.Visible ? 1 : 0;

            var style = registry.SimulatorStyle;
            style.AccuracyBar.Value = AccuracyBar.Value;
            style.MissesBar.Value = MissesBar.Value;
            style.SaveButton.Value = SaveButton.Value;
            style.UseForAllButton.Value = UseForAllButton.Value;
            style.DefaultButton.Value = DefaultButton.Value;
            style.CancelButton.Value = CancelButton.Value;
        }

        [Resolved(CanBeNull = true)]
        private Overlays.SkinEditor.SkinEditorOverlay? skinEditor { get; set; }

        protected override void Dispose(bool isDisposing)
        {
            if (inControl)
                registry.SimulatorStyle.Reset();

            base.Dispose(isDisposing);
        }
    }
}
