// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarRulesetTabButton : TabItem<RulesetInfo>
    {
        private readonly RulesetButton ruleset;

        public ToolbarRulesetTabButton(RulesetInfo value)
            : base(value)
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;
            Child = ruleset = new RulesetButton
            {
                Active = false,
            };

            var rInstance = value.CreateInstance();

            ruleset.TooltipMain = rInstance.Description;
            ruleset.TooltipSub = ToolbarStrings.PlaySomeRuleset(rInstance.Description);
            ruleset.SetIcon(rInstance.CreateIcon());
        }

        protected override void OnActivated() => ruleset.Active = true;

        protected override void OnDeactivated() => ruleset.Active = false;

        private partial class RulesetButton : ToolbarButton
        {
            protected override HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverSounds();

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public RulesetButton()
            {
                ButtonContent.Padding = new MarginPadding(PADDING)
                {
                    Bottom = 5
                };
            }

            protected override bool UsesToolbarIconColour => false;

            [Resolved(CanBeNull = true)]
            private OsuConfigManager? config { get; set; }

            private IBindable<string>? colourSetting;
            private OilyPanelColours oilyColours = new OilyPanelColours();
            private bool active;

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // oily: gamemode icon colours from settings.
                if (config != null)
                {
                    colourSetting = OilyPanelColours.Track(config, c =>
                    {
                        oilyColours = c;
                        Scheduler.AddOnce(updateColour);
                    });
                }
            }

            public bool Active
            {
                set
                {
                    active = value;
                    Scheduler.AddOnce(updateColour);
                }
            }

            private void updateColour()
            {
                IconContainer.Colour = active
                    ? oilyColours.GetColour(OilyColour.GamemodeSelected) ?? Color4Extensions.FromHex("#00FFAA")
                    : oilyColours.GetColour(OilyColour.GamemodeOthers) ?? colours.GrayF;
            }

            protected override bool OnClick(ClickEvent e)
            {
                Parent!.TriggerClick();
                return base.OnClick(e);
            }
        }
    }
}
