// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Overlays.Mods
{
    public partial class ModSelectFooterContent : VisibilityContainer
    {
        private readonly ModSelectOverlay overlay;

        private RankingInformationDisplay? rankingInformationDisplay;
        private BeatmapAttributesDisplay? beatmapAttributesDisplay;

        public DeselectAllModsButton? DeselectAllModsButton { get; set; }

        public readonly IBindable<WorkingBeatmap?> Beatmap = new Bindable<WorkingBeatmap?>();
        public readonly IBindable<IReadOnlyList<Mod>> ActiveMods = new Bindable<IReadOnlyList<Mod>>();
        public readonly IBindable<RulesetInfo?> Ruleset = new Bindable<RulesetInfo?>();

        /// <summary>
        /// Whether the effects (on score multiplier, on or beatmap difficulty) of the current selected set of mods should be shown.
        /// </summary>
        protected virtual bool ShowModEffects => true;

        /// <summary>
        /// Whether the ranking information and beatmap attributes displays are stacked vertically due to small space.
        /// </summary>
        public bool DisplaysStackedVertically => false;

        public ModSelectFooterContent(ModSelectOverlay overlay)
        {
            this.overlay = overlay;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            // oily (ilo, 2026-11-26): the buttons sit next to mod select's search bar, not on the bottom bar.
            overlay.SetHeaderButtons(CreateButtons().ToArray());

            if (ShowModEffects)
            {
                AddRangeInternal(new Drawable[]
                {
                    // The map's details sit on the left, between the bottom bar's buttons and its toolbar icons.
                    beatmapAttributesDisplay = new BeatmapAttributesDisplay
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        BeatmapInfo = { Value = Beatmap.Value?.BeatmapInfo },
                    },
                    // Ranked / unranked and the score multiplier stay on the far right.
                    rankingInformationDisplay = new RankingInformationDisplay
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                        Margin = new MarginPadding { Horizontal = 20 },
                    },
                });
            }
        }

        private ModSettingChangeTracker? modSettingChangeTracker;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Beatmap.BindValueChanged(b =>
            {
                beatmapAttributesDisplay?.BeatmapInfo.Value = b.NewValue?.BeatmapInfo;

                updateInformation();
            }, true);

            Ruleset.BindValueChanged(_ => updateInformation());
            ActiveMods.BindValueChanged(_ =>
            {
                updateInformation();

                modSettingChangeTracker?.Dispose();

                // Importantly, use ActiveMods.Value here (and not the ValueChanged NewValue) as the latter can
                // potentially be stale, due to complexities in the way change trackers work.
                //
                // See https://github.com/ppy/osu/pull/23284#issuecomment-1529056988
                modSettingChangeTracker = new ModSettingChangeTracker(ActiveMods.Value);
                modSettingChangeTracker.SettingChanged += _ => updateInformation();
            }, true);
        }

        private void updateInformation()
        {
            WorkingBeatmap? workingBeatmap = Beatmap.Value;

            if (rankingInformationDisplay != null && workingBeatmap != null)
            {
                var scoreMultiplierCalculator = Ruleset.Value?.CreateInstance().CreateScoreMultiplierCalculator(new ScoreMultiplierContext(workingBeatmap.BeatmapInfo.Difficulty));
                double multiplier = scoreMultiplierCalculator?.CalculateFor(ActiveMods.Value) ?? 1;

                rankingInformationDisplay.ModMultiplier.Value = multiplier;
                rankingInformationDisplay.Ranked.Value = ActiveMods.Value.All(m => m.Ranked);
            }

            beatmapAttributesDisplay?.Mods.Value = ActiveMods.Value;
        }

        /// <summary>
        /// How wide the map's details are with every attribute shown.
        /// </summary>
        private const float expanded_attributes_width = 640;

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        protected override void Update()
        {
            base.Update();

            if (beatmapAttributesDisplay == null || Alpha < 1)
                return;

            var footer = this.FindClosestParent<Screens.Footer.ScreenFooter>();

            // The space between the bottom bar's buttons (like Mods) and its toolbar icons (or the ranked display).
            float left = 0;
            float right = ToLocalSpace(rankingInformationDisplay!.ScreenSpaceDrawQuad.TopLeft).X - 20;

            if (footer != null)
            {
                // oily: the fixed layout moves the buttons left of where this area starts, so this can be negative.
                var shown = footer.ShownButtons.Where(b => b.IsPresent).ToArray();
                if (shown.Length > 0)
                    left = shown.Max(button => ToLocalSpace(button.ScreenSpaceDrawQuad.TopRight).X);

                // The first toolbar icon on the bar that's actually showing (and right of the buttons).
                if (footer.ToolbarButtons is CompositeDrawable toolbarButtons && toolbarButtons.IsPresent)
                {
                    foreach (var icon in toolbarButtons.ChildrenOfType<Drawable>().Where(d => d.IsPresent && d.DrawWidth > 1 && d.DrawWidth < 200))
                    {
                        float iconLeft = ToLocalSpace(icon.ScreenSpaceDrawQuad.TopLeft).X;
                        if (iconLeft > left)
                            right = Math.Min(right, iconLeft);
                    }
                }

                // oily: the fixed toolbar sits inside the bottom bar, so its first icon (the gamemode circle) counts too.
                if (game?.Toolbar is Overlays.Toolbar.Toolbar toolbar && toolbar.IsPresent)
                {
                    float footerTop = footer.ScreenSpaceDrawQuad.TopLeft.Y;

                    foreach (var icon in toolbar.ChildrenOfType<Overlays.Toolbar.ToolbarRulesetTabButton>().Cast<Drawable>()
                                                .Concat(toolbar.ChildrenOfType<Overlays.Toolbar.ToolbarButton>())
                                                .Where(d => d.IsPresent && d.DrawWidth > 1 && d.DrawWidth < 200))
                    {
                        var quad = icon.ScreenSpaceDrawQuad;
                        if (quad.Centre.Y < footerTop)
                            continue;

                        float iconLeft = ToLocalSpace(quad.TopLeft).X;
                        if (iconLeft > left)
                            right = Math.Min(right, iconLeft);
                    }
                }
            }

            float room = right - left;

            // Every attribute only if there's room; otherwise just the stars and BPM (hover shows the rest).
            bool collapsed = room < expanded_attributes_width + 20;
            beatmapAttributesDisplay.Collapsed.Value = collapsed;

            float width = collapsed ? collapsedWidth() : expanded_attributes_width;

            // Sit in the middle of that space.
            beatmapAttributesDisplay.X = left + Math.Max(10, (room - width) / 2);
        }

        /// <summary>
        /// How wide the map's details are with only the stars and BPM shown.
        /// </summary>
        private float collapsedWidth() => beatmapAttributesDisplay!.CollapsedWidth;

        protected virtual IEnumerable<ShearedButton> CreateButtons() => new[]
        {
            DeselectAllModsButton = new DeselectAllModsButton(overlay)
        };

        protected override void PopIn()
        {
            this.MoveToY(0, 400, Easing.OutQuint)
                .FadeIn(400, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.MoveToY(-20f, 200, Easing.OutQuint)
                .FadeOut(200, Easing.OutQuint);
        }
    }
}
