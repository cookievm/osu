// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Screens.Footer;
using osuTK;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// oily: while the skin editor's "Toolbars" tab is open, puts a box over every toolbar and every bottom bar button,
    /// so they can be dragged anywhere (even past the screen's edges). The real buttons don't get clicked meanwhile.
    /// </summary>
    public partial class BarEditLayer : Container
    {
        private readonly Func<Toolbar?> getToolbar;
        private readonly Func<ScreenFooter?> getFooter;

        private bool editing;

        /// <summary>
        /// Whether the boxes are shown and can be dragged.
        /// </summary>
        public bool Editing
        {
            get => editing;
            set
            {
                editing = value;

                if (!editing)
                    Clear();
            }
        }

        public BarEditLayer(Func<Toolbar?> getToolbar, Func<ScreenFooter?> getFooter)
        {
            this.getToolbar = getToolbar;
            this.getFooter = getFooter;

            RelativeSizeAxes = Axes.Both;
        }

        protected override void Update()
        {
            base.Update();

            if (!editing)
                return;

            var targets = new List<(Drawable target, Action<Vector2> drag, Action save)>();

            var toolbar = getToolbar();

            if (toolbar != null)
            {
                foreach (var strip in toolbar.Strips)
                {
                    if (strip.Index >= toolbar.Layout.Bars.Count)
                        continue;

                    var bar = toolbar.Layout.Bars[strip.Index];

                    targets.Add((strip, delta =>
                    {
                        Vector2 local = toolbar.ToLocalSpace(delta) - toolbar.ToLocalSpace(Vector2.Zero);
                        bar.OffsetX = Math.Clamp(bar.OffsetX + local.X / Math.Max(1, toolbar.DrawWidth), -1, 1);
                        bar.OffsetY = Math.Clamp(bar.OffsetY + local.Y / Math.Max(1, toolbar.DrawHeight), -1, 1);
                    }, toolbar.SaveLayout));
                }
            }

            var footer = getFooter();

            if (footer != null && footer.IsPresent)
            {
                foreach (var button in footer.ShownButtons)
                {
                    if (FooterLayout.ItemOf(button) is not FooterItem item || !button.IsPresent)
                        continue;

                    var saved = footer.Layout.Buttons.FirstOrDefault(b => b.Item == item);

                    if (saved == null)
                        continue;

                    targets.Add((button, delta =>
                    {
                        Vector2 local = footer.ToLocalSpace(delta) - footer.ToLocalSpace(Vector2.Zero);
                        saved.OffsetX += local.X;
                        saved.OffsetY += local.Y;
                    }, footer.SaveLayout));
                }
            }

            foreach (var handle in Children.OfType<DragBox>().Where(h => targets.All(t => t.target != h.Target)).ToArray())
                Remove(handle, true);

            foreach (var (target, drag, save) in targets)
            {
                var handle = Children.OfType<DragBox>().FirstOrDefault(h => h.Target == target);

                if (handle == null)
                    Add(handle = new DragBox(target));

                handle.Dragged = drag;
                handle.Released = save;
            }
        }

        /// <summary>
        /// The box over one bar or button. Dragging it moves the bar or button; letting go saves it.
        /// </summary>
        public partial class DragBox : CompositeDrawable
        {
            public readonly Drawable Target;

            public Action<Vector2>? Dragged;
            public Action? Released;

            private readonly Box fill;

            public DragBox(Drawable target)
            {
                Target = target;

                Masking = true;
                CornerRadius = 4;
                BorderThickness = 2;
                BorderColour = OsuColour.Gray(1f);

                InternalChild = fill = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.1f,
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                BorderColour = Colour4.FromHex(@"ffcc22");
                fill.Colour = Colour4.FromHex(@"ffcc22");
            }

            protected override void Update()
            {
                base.Update();

                if (Parent == null || !Target.IsLoaded)
                    return;

                var area = Parent.ToLocalSpace(Target.ScreenSpaceDrawQuad).AABBFloat;

                Position = area.Location;
                Size = area.Size;
            }

            protected override bool OnHover(HoverEvent e)
            {
                fill.FadeTo(0.3f, 100);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e) => fill.FadeTo(0.1f, 100);

            protected override bool OnMouseDown(MouseDownEvent e) => true;

            protected override bool OnClick(ClickEvent e) => true;

            protected override bool OnDragStart(DragStartEvent e) => true;

            protected override void OnDrag(DragEvent e) => Dragged?.Invoke(e.ScreenSpaceMousePosition - e.ScreenSpaceLastMousePosition);

            protected override void OnDragEnd(DragEndEvent e) => Released?.Invoke();
        }
    }
}
