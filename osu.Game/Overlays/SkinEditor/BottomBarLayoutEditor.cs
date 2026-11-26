// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Footer;
using osuTK;

namespace osu.Game.Overlays.SkinEditor
{
    /// <summary>
    /// oily: the song select bottom bar's background transparency and size (see <see cref="FooterLayout"/>).
    /// Its buttons can't be hidden or reordered here (ilo, 2026-11-26: no one needs to remove them); they can still be dragged on screen.
    /// Shown with the toolbar settings.
    /// </summary>
    public partial class BottomBarLayoutEditor : FillFlowContainer
    {
        private readonly Bindable<string> layoutSetting = new Bindable<string>();

        private readonly BindableFloat size = new BindableFloat(1)
        {
            MinValue = FooterLayout.MIN_SIZE,
            MaxValue = FooterLayout.MAX_SIZE,
            Precision = 0.05f,
        };

        private FooterLayout layout = FooterLayout.CreateDefault();

        public BottomBarLayoutEditor()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(5);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.OilyFooterLayout, layoutSetting);
            layout = FooterLayout.Parse(layoutSetting.Value);
            size.Value = layout.Size;

            Children = new Drawable[]
            {
                new PanelOpacitySlider(OilyPanel.BottomBar, "Bottom bar background",
                    "How see-through the bottom bar's background is. 0% makes it fully transparent, so only the buttons show."),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = "Bottom bar size",
                    HintText = "Makes the bar and all its buttons (back, mods, random and so on) bigger or smaller.",
                    Current = size,
                    DisplayAsPercentage = true,
                })
                {
                    Keywords = new[] { "footer", "bottom bar", "size", "scale", "resize" },
                },
            };

            size.BindValueChanged(s =>
            {
                if (updatingFromSetting)
                    return;

                layout.Size = s.NewValue;
                lastSaved = layout.Serialise();
                layoutSetting.Value = lastSaved;
            });

            // Buttons dragged on screen save the layout themselves; pick that up so it isn't overwritten.
            layoutSetting.BindValueChanged(v =>
            {
                if (v.NewValue == lastSaved)
                    return;

                layout = FooterLayout.Parse(v.NewValue);
                lastSaved = v.NewValue;
                updatingFromSetting = true;
                size.Value = layout.Size;
                updatingFromSetting = false;
            });
        }

        private string? lastSaved;
        private bool updatingFromSetting;
    }
}
