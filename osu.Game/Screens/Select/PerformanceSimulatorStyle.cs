// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Skinning;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: colours of the Universal PP Simulator's sliders and buttons. White keeps lazer's colour.
    /// </summary>
    public class PerformanceSimulatorStyle
    {
        public readonly Bindable<Colour4> Box = new Bindable<Colour4>(Colour4.White);
        public readonly BindableFloat BoxOpacity = new BindableFloat(1);
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
            Box.SetDefault();
            BoxOpacity.SetDefault();
            AccuracyBar.SetDefault();
            MissesBar.SetDefault();
            SaveButton.SetDefault();
            UseForAllButton.SetDefault();
            DefaultButton.SetDefault();
            CancelButton.SetDefault();
        }
    }

    /// <summary>
    /// oily: skin component on the skin editor's "PP Simulator" layer that colours the Universal PP Simulator's box, sliders and four buttons.
    /// It draws nothing on screen; picking it in the skin editor's component list opens its settings.
    /// </summary>
    public partial class PpSimulatorColours : CompositeDrawable, ISettingsOnlySkinComponent
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource("Box colour", "The simulator's box. White keeps lazer's colour (or the one from Settings > Colours).")]
        public BindableColour4 Box { get; } = new BindableColour4(Colour4.White);

        [SettingSource("Box opacity", "How see-through the simulator's box is.", SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat BoxOpacity { get; } = new BindableFloat(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

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
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new SkinComponentLabel("PP Simulator colours");
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

            // On the screen it is only settings; the label is for the skin editor's component list.
            if (inControl)
            {
                Alpha = 0;
                AlwaysPresent = true;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!inControl)
                return;

            // Only the first one in the layer is in charge.
            if (Parent?.ChildrenOfType<PpSimulatorColours>().FirstOrDefault() != this)
                return;

            var style = registry.SimulatorStyle;
            style.Box.Value = Box.Value;
            style.BoxOpacity.Value = BoxOpacity.Value;
            style.AccuracyBar.Value = AccuracyBar.Value;
            style.MissesBar.Value = MissesBar.Value;
            style.SaveButton.Value = SaveButton.Value;
            style.UseForAllButton.Value = UseForAllButton.Value;
            style.DefaultButton.Value = DefaultButton.Value;
            style.CancelButton.Value = CancelButton.Value;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (inControl)
                registry.SimulatorStyle.Reset();

            base.Dispose(isDisposing);
        }
    }
}
