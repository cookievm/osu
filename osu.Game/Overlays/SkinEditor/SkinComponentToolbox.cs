// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Overlays.SkinEditor
{
    public partial class SkinComponentToolbox : EditorSidebarSection
    {
        public Action<Type>? RequestPlacement;

        private readonly SkinnableContainer target;

        private readonly RulesetInfo? ruleset;

        private FillFlowContainer fill = null!;

        /// <summary>
        /// Create a new component toolbox for the specified taget.
        /// </summary>
        /// <param name="target">The target. This is mainly used as a dependency source to find candidate components.</param>
        /// <param name="ruleset">A ruleset to filter components by. If null, only components which are not ruleset-specific will be included.</param>
        public SkinComponentToolbox(SkinnableContainer target, RulesetInfo? ruleset)
            : base(ruleset == null ? SkinEditorStrings.Components : LocalisableString.Interpolate($"{SkinEditorStrings.Components} ({ruleset.Name})"))
        {
            this.target = target;
            this.ruleset = ruleset;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = fill = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(EditorSidebar.PADDING)
            };

            reloadComponents();
        }

        private void reloadComponents()
        {
            fill.Clear();

            // oily: the toolbar and PP Simulator settings live with song select's parts.
            if (target.Lookup.Lookup == GlobalSkinnableContainers.SongSelect)
            {
                fill.Add(new ToolboxActionButton("Toolbars", () => skinEditor?.ShowToolbarSettings()));
                fill.Add(new ToolboxActionButton("PP Simulator", showPerformanceSimulator));
            }

            var skinnableTypes = SerialisedDrawableInfo.GetAllAvailableDrawables(ruleset);
            foreach (var type in skinnableTypes)
                attemptAddComponent(type);
        }

        private void attemptAddComponent(Type type)
        {
            // oily: song select only offers pieces that make sense there (no gameplay pieces).
            if (target.Lookup.Lookup == GlobalSkinnableContainers.SongSelect && !Screens.Select.SongSelectSkinComponents.IsAllowed(type))
                return;

            // oily: the PP Simulator layer only holds its colours.
            if (target.Lookup.Lookup == GlobalSkinnableContainers.PpSimulator && type != typeof(Screens.Select.PpSimulatorColours))
                return;

            try
            {
                Drawable instance = (Drawable)Activator.CreateInstance(type)!;

                if (!((ISerialisableDrawable)instance).IsEditable) return;

                fill.Add(new ToolboxComponentButton(instance, target)
                {
                    RequestPlacement = t => RequestPlacement?.Invoke(t),
                    Expanding = contractOtherButtons,
                });
            }
            catch (DependencyNotRegisteredException)
            {
                // This loading code relies on try-catching any dependency injection errors to know which components are valid for the current target screen.
                // If a screen can't provide the required dependencies, a skinnable component should not be displayed in the list.
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Skin component {type} could not be loaded in the editor component list due to an error");
            }
        }

        [Resolved]
        private SkinEditor? skinEditor { get; set; }

        [Resolved]
        private SkinEditorOverlay? skinEditorOverlay { get; set; }

        /// <summary>
        /// oily: song select with the Universal PP Simulator open, working on its own layer.
        /// </summary>
        private void showPerformanceSimulator()
        {
            (target.FindClosestParent<Screens.Select.SongSelect>())?.ShowPerformanceSimulatorScene();
            skinEditorOverlay?.SelectTarget(GlobalSkinnableContainers.PpSimulator);
        }

        /// <summary>
        /// oily: a button in the component list that opens some settings instead of adding a component.
        /// </summary>
        public partial class ToolboxActionButton : OsuButton
        {
            public ToolboxActionButton(string text, Action action)
            {
                RelativeSizeAxes = Axes.X;
                Height = LABELLED_SIZE;
                Enabled.Value = true;
                Action = action;

                Add(new SkinComponentLabel(text)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                BackgroundColour = colourProvider.Background3;
            }
        }

        /// <summary>
        /// oily: how tall the buttons are for components that just show their name.
        /// </summary>
        public const float LABELLED_SIZE = 36;

        private void contractOtherButtons(ToolboxComponentButton obj)
        {
            foreach (var b in fill.OfType<ToolboxComponentButton>())
            {
                if (b == obj)
                    continue;

                b.Contract();
            }
        }

        public partial class ToolboxComponentButton : OsuButton
        {
            public Action<Type>? RequestPlacement;
            public Action<ToolboxComponentButton>? Expanding;

            private readonly Drawable component;
            private readonly CompositeDrawable? dependencySource;

            private Container innerContainer = null!;

            private ScheduledDelegate? expandContractAction;

            private const float contracted_size = 60;
            private const float expanded_size = 120;

            public ToolboxComponentButton(Drawable component, CompositeDrawable? dependencySource)
            {
                this.component = component;
                this.dependencySource = dependencySource;

                Enabled.Value = true;

                RelativeSizeAxes = Axes.X;
                Height = component is ISkinComponentWithLabel ? LABELLED_SIZE : contracted_size;
            }

            // oily: components that just show their name are small and don't grow on hover.
            private bool labelled => component is ISkinComponentWithLabel;

            private const double animation_duration = 500;

            protected override bool OnHover(HoverEvent e)
            {
                if (labelled)
                    return base.OnHover(e);

                expandContractAction?.Cancel();
                expandContractAction = Scheduler.AddDelayed(() =>
                {
                    this.ResizeHeightTo(expanded_size, animation_duration, Easing.OutQuint);
                    Expanding?.Invoke(this);
                }, 100);

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);

                expandContractAction?.Cancel();
                // If no other component is selected for too long, force a contract.
                // Otherwise we will generally contract when Contract() is called from outside.
                expandContractAction = Scheduler.AddDelayed(Contract, 1000);
            }

            public void Contract()
            {
                // Cheap debouncing to avoid stacking animations.
                // The only place this is nulled is at the end of this method.
                if (expandContractAction == null)
                    return;

                this.ResizeHeightTo(contracted_size, animation_duration, Easing.OutQuint);

                expandContractAction?.Cancel();
                expandContractAction = null;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                BackgroundColour = colourProvider.Background3;

                AddRange(new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = labelled ? new MarginPadding(5) : new MarginPadding(10) { Bottom = 20 },
                        Masking = true,
                        Child = innerContainer = new DependencyBorrowingContainer(dependencySource)
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Child = component
                        },
                    },
                    new OsuSpriteText
                    {
                        Text = component.GetType().Name,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Margin = new MarginPadding(5),
                        // oily: these components say what they are in their own preview.
                        Alpha = component is ISkinComponentWithLabel ? 0 : 1,
                    },
                });

                // adjust provided component to fit / display in a known state.
                component.Anchor = Anchor.Centre;
                component.Origin = Anchor.Centre;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                if (component.DrawSize != Vector2.Zero)
                {
                    float bestScale = Math.Min(
                        innerContainer.DrawWidth / component.DrawWidth,
                        innerContainer.DrawHeight / component.DrawHeight);

                    // oily: names stay at their own text size.
                    if (labelled)
                        bestScale = Math.Min(bestScale, 1);

                    innerContainer.Scale = new Vector2(bestScale);
                }
            }

            protected override bool OnClick(ClickEvent e)
            {
                RequestPlacement?.Invoke(component.GetType());
                return true;
            }
        }

        private partial class DependencyBorrowingContainer : Container
        {
            protected override bool ShouldBeConsideredForInput(Drawable child) => false;

            public override bool PropagateNonPositionalInputSubTree => false;

            private readonly CompositeDrawable? donor;

            public DependencyBorrowingContainer(CompositeDrawable? donor)
            {
                this.donor = donor;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var baseDependencies = base.CreateChildDependencies(parent);
                if (donor == null)
                    return baseDependencies;

                var dependencies = new DependencyContainer(donor.Dependencies);
                // inject `SkinEditor` again *on top* of the borrowed dependencies.
                // this is designed to let components know when they are being displayed in the context of the skin editor
                // via attempting to resolve `SkinEditor`.
                dependencies.CacheAs(baseDependencies.Get<SkinEditor>());
                return dependencies;
            }
        }
    }
}
