// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.Overlays.SkinEditor
{
    /// <summary>
    /// oily: how see-through one panel's background is (0% is fully transparent). Shares its value with the "Panel colours" settings.
    /// </summary>
    public partial class PanelOpacitySlider : CompositeDrawable
    {
        private readonly OilyPanel panel;
        private readonly string caption;
        private readonly string hint;

        private readonly Bindable<string> coloursSetting = new Bindable<string>();
        private readonly BindableFloat opacity = new BindableFloat(1) { MinValue = 0, MaxValue = 1, Precision = 0.01f };

        private bool updatingFromSetting;

        public PanelOpacitySlider(OilyPanel panel, string caption, string hint)
        {
            this.panel = panel;
            this.caption = caption;
            this.hint = hint;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.OilyPanelColours, coloursSetting);

            InternalChild = new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = caption,
                HintText = hint,
                Current = opacity,
                DisplayAsPercentage = true,
            })
            {
                Keywords = new[] { "transparent", "opacity", "background", "see-through" },
            };

            coloursSetting.BindValueChanged(v =>
            {
                updatingFromSetting = true;
                opacity.Value = OilyPanelColours.Parse(v.NewValue).Get(panel).Opacity;
                updatingFromSetting = false;
            }, true);

            opacity.BindValueChanged(o =>
            {
                if (updatingFromSetting)
                    return;

                var colours = OilyPanelColours.Parse(coloursSetting.Value);
                var style = colours.Get(panel);
                style.Opacity = o.NewValue;
                colours.Panels[panel] = style;
                coloursSetting.Value = colours.Serialise();
            });
        }
    }
}
