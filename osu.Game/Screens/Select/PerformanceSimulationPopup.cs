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
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Overlays.SkinEditor;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: the Universal PP Simulator. Lets the player set a beatmap's own pretend score for simulated pp (opened from song select's "PP Simulator" button).
    /// </summary>
    public partial class PerformanceSimulationPopup : VisibilityContainer, IKeyBindingHandler<GlobalAction>
    {
        [Resolved]
        private PerformanceSimulationStore simulations { get; set; } = null!;

        private readonly BindableDouble accuracy = new BindableDouble { MinValue = 0, MaxValue = 100, Precision = 0.01 };
        private readonly BindableInt misses = new BindableInt { MinValue = 0, MaxValue = 1000 };

        [Resolved(CanBeNull = true)]
        private SongSelectPieceRegistry? registry { get; set; }

        [Resolved(CanBeNull = true)]
        private OsuConfigManager? config { get; set; }

        private IBindable<string>? panelColourSetting;

        internal PerformanceSimulatorStyle? Style => registry?.SimulatorStyle;

        [Resolved(CanBeNull = true)]
        private SkinEditorOverlay? skinEditor { get; set; }


        private Box panelBackground = null!;
        private FormSliderBar<double> accuracySlider = null!;
        private FormSliderBar<int> missesSlider = null!;
        private RoundedButton saveButton = null!;
        private RoundedButton useForAllButton = null!;
        private RoundedButton defaultButton = null!;
        private RoundedButton cancelButton = null!;
        private Colour4 panelDefaultColour;

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
                        panelBackground = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = panelDefaultColour = colourProvider.Background4,
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
                                    Text = "Universal PP Simulator",
                                },
                                subtitle = new TruncatingSpriteText
                                {
                                    Font = OsuFont.Style.Caption1,
                                    Colour = colourProvider.Content2,
                                    RelativeSizeAxes = Axes.X,
                                },
                                accuracySlider = new FormSliderBar<double>
                                {
                                    Caption = "Accuracy (%)",
                                    Current = accuracy,
                                    KeyboardStep = 0.1f,
                                },
                                missesSlider = new FormSliderBar<int>
                                {
                                    Caption = "Misses",
                                    Current = misses,
                                    KeyboardStep = 1,
                                },
                                saveButton = new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Text = "Save for this beatmap",
                                    Action = () => finish(() => simulations.Set(beatmap!, current())),
                                },
                                useForAllButton = new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Text = "Use for all beatmaps without their own",
                                    Action = () => finish(() => simulations.SetDefault(current())),
                                },
                                defaultButton = new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Text = "Go back to the default",
                                    Action = () => finish(() => simulations.Set(beatmap!, null)),
                                },
                                cancelButton = new RoundedButton
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

        protected override void LoadComplete()
        {
            base.LoadComplete();

            State.BindValueChanged(_ => skinEditor?.RefreshTargets());

            // oily: the box's own colour and opacity (Settings > Colours).
            if (config != null)
            {
                panelColourSetting = OilyPanelColours.Track(config, OilyPanel.PpSimulator, style =>
                {
                    settingsPanelStyle = style;
                    updatePanel();
                });
            }

            // oily: slider and button colours from the skin's "PP Simulator colours" component.
            if (registry != null)
            {
                var style = registry.SimulatorStyle;
                style.Box.BindValueChanged(_ => updatePanel());
                style.BoxOpacity.BindValueChanged(_ => updatePanel(), true);
                style.AccuracyBar.BindValueChanged(c => accuracySlider.FillColour = PerformanceSimulatorStyle.Custom(c.NewValue), true);
                style.MissesBar.BindValueChanged(c => missesSlider.FillColour = PerformanceSimulatorStyle.Custom(c.NewValue), true);
                bindButton(style.SaveButton, saveButton);
                bindButton(style.UseForAllButton, useForAllButton);
                bindButton(style.DefaultButton, defaultButton);
                bindButton(style.CancelButton, cancelButton);
            }
        }

        private OilyPanelStyle settingsPanelStyle = new OilyPanelStyle();

        /// <summary>
        /// The box's colour: the skin's if it sets one, else Settings > Colours, else lazer's.
        /// </summary>
        private void updatePanel()
        {
            var skin = registry?.SimulatorStyle;
            panelBackground.Colour = (skin == null ? null : PerformanceSimulatorStyle.Custom(skin.Box.Value)) ?? settingsPanelStyle.CustomColour ?? panelDefaultColour;
            panelBackground.Alpha = settingsPanelStyle.Opacity * (skin?.BoxOpacity.Value ?? 1);
        }

        private Colour4? defaultButtonColour;

        private void bindButton(IBindable<Colour4> colour, RoundedButton button)
        {
            defaultButtonColour ??= button.BackgroundColour;
            colour.BindValueChanged(c => button.BackgroundColour = PerformanceSimulatorStyle.Custom(c.NewValue) ?? defaultButtonColour.Value, true);
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

            title.Text = "Universal PP Simulator";
            subtitle.Text = $"{beatmap.Metadata.Artist} - {beatmap.Metadata.Title} [{beatmap.DifficultyName}]"
                            + (simulations.HasOwn(beatmap) ? " (this beatmap's own)" : " (default)");

            Show();
        }

        private OilySimulation current() => new OilySimulation(accuracy.Value, misses.Value, 100); // o!ily pp does not scale with combo, so there is no combo slider.

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
            // While skinning, clicks around the box are for the skin editor.
            if (skinEditor?.State.Value == Visibility.Visible)
                return true;

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
