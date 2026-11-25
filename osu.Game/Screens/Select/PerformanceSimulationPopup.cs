// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Rulesets.Difficulty;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: lets the player set a beatmap's own pretend score for simulated pp (opened by right-clicking a card in song select).
    /// </summary>
    public partial class PerformanceSimulationPopup : VisibilityContainer, IKeyBindingHandler<GlobalAction>
    {
        [Resolved]
        private PerformanceSimulationStore simulations { get; set; } = null!;

        private readonly BindableDouble accuracy = new BindableDouble { MinValue = 0, MaxValue = 100, Precision = 0.01 };
        private readonly BindableInt misses = new BindableInt { MinValue = 0, MaxValue = 1000 };
        private readonly BindableDouble combo = new BindableDouble { MinValue = 0, MaxValue = 100, Precision = 0.1 };

        private OsuSpriteText title = null!;
        private TruncatingSpriteText subtitle = null!;
        private Container panel = null!;

        private BeatmapInfo? beatmap;

        protected override bool StartHidden => true;

        public PerformanceSimulationPopup()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.5f),
                },
                panel = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 420,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 10,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background4,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(15),
                            Spacing = new Vector2(8),
                            Children = new Drawable[]
                            {
                                title = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Heading2,
                                    Text = "pp simulation",
                                },
                                subtitle = new TruncatingSpriteText
                                {
                                    Font = OsuFont.Style.Caption1,
                                    Colour = colourProvider.Content2,
                                    RelativeSizeAxes = Axes.X,
                                },
                                new FormSliderBar<double>
                                {
                                    Caption = "Accuracy (%)",
                                    Current = accuracy,
                                    KeyboardStep = 0.1f,
                                },
                                new FormSliderBar<int>
                                {
                                    Caption = "Misses",
                                    Current = misses,
                                    KeyboardStep = 1,
                                },
                                new FormSliderBar<double>
                                {
                                    Caption = "Combo (% of max combo)",
                                    Current = combo,
                                    KeyboardStep = 1,
                                },
                                new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Text = "Save for this beatmap",
                                    Action = () => finish(() => simulations.Set(beatmap!, current())),
                                },
                                new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Text = "Use for all beatmaps without their own",
                                    Action = () => finish(() => simulations.SetDefault(current())),
                                },
                                new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Text = "Go back to the default",
                                    Action = () => finish(() => simulations.Set(beatmap!, null)),
                                },
                                new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Text = "Cancel",
                                    Action = Hide,
                                },
                            }
                        }
                    }
                },
            };
        }

        /// <summary>
        /// Opens the popup for a beatmap, filled in with its current pretend score.
        /// </summary>
        public void ShowFor(BeatmapInfo beatmap)
        {
            this.beatmap = beatmap;

            var simulation = simulations.Get(beatmap);
            accuracy.Value = simulation.Accuracy;
            misses.Value = simulation.Misses;
            combo.Value = simulation.ComboPercent;

            title.Text = simulations.HasOwn(beatmap) ? "pp simulation (this beatmap's own)" : "pp simulation (default)";
            subtitle.Text = $"{beatmap.Metadata.Artist} - {beatmap.Metadata.Title} [{beatmap.DifficultyName}]";

            Show();
        }

        private OilySimulation current() => new OilySimulation(accuracy.Value, misses.Value, combo.Value);

        private void finish(Action action)
        {
            if (beatmap != null)
                action();

            Hide();
        }

        protected override void PopIn()
        {
            this.FadeIn(200, Easing.OutQuint);
            panel.ScaleTo(0.95f).ScaleTo(1, 400, Easing.OutElasticHalf);
        }

        protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);

        // Blocks clicks to song select while open; a click outside the panel closes it.
        protected override bool OnClick(ClickEvent e)
        {
            if (!panel.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                Hide();

            return true;
        }

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnScroll(ScrollEvent e) => true;

        protected override bool OnHover(HoverEvent e) => true;

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (State.Value == Visibility.Hidden)
                return false;

            if (e.Action == GlobalAction.Back)
            {
                Hide();
                return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }
}
