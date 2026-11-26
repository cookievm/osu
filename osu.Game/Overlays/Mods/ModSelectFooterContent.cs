// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osuTK;

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

            var buttons = CreateButtons().ToArray();

            // oily (ilo, 2026-11-26): "Deselect All" sits next to mod select's search bar, not on the bottom bar.
            // Multiplayer's free mods (Select All + Deselect All) don't fit there, so they stay on the bottom bar like lazer.
            if (buttons.Length == 1)
                overlay.SetHeaderButtons(buttons);
            else
            {
                AddInternal(new FillFlowContainer<ShearedButton>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Padding = new MarginPadding { Horizontal = 20 },
                    Spacing = new Vector2(10),
                    Children = buttons,
                });
            }

            if (ShowModEffects)
            {
                AddRangeInternal(new Drawable[]
                {
                    // The map's details sit on the left, between the bottom bar's buttons and its toolbar icons.
                    beatmapAttributesDisplay = new BeatmapAttributesDisplay
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Margin = new MarginPadding { Horizontal = 20 },
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

        protected override void Update()
        {
            base.Update();

            if (beatmapAttributesDisplay == null || Alpha < 1)
                return;

            // Show every attribute only if there's room before the bottom bar's toolbar icons (or the ranked display).
            float limit = rankingInformationDisplay!.ScreenSpaceDrawQuad.TopLeft.X;

            if (this.FindClosestParent<Screens.Footer.ScreenFooter>()?.ToolbarButtons is Drawable toolbarButtons && toolbarButtons.IsPresent && toolbarButtons.DrawWidth > 0)
            {
                float toolbarLeft = toolbarButtons.ScreenSpaceDrawQuad.TopLeft.X;
                if (toolbarLeft > beatmapAttributesDisplay.ScreenSpaceDrawQuad.TopLeft.X)
                    limit = Math.Min(limit, toolbarLeft);
            }

            float room = ToLocalSpace(new Vector2(limit, 0)).X - ToLocalSpace(beatmapAttributesDisplay.ScreenSpaceDrawQuad.TopLeft).X;
            beatmapAttributesDisplay.Collapsed.Value = room < expanded_attributes_width;
        }

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
