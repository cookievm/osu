// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osuTK;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// oily: holds the popup a toolbar button opens (settings, chat, now playing...) and slides it in from the side of the screen
    /// the toolbar settings pick for that button ("Popup comes from"). With lazer's default, nothing changes.
    /// </summary>
    public partial class ToolbarPopupContainer : Container
    {
        public readonly ToolbarItem Item;

        private readonly VisibilityContainer popup;
        private readonly Anchor originalAnchor;
        private readonly Anchor originalOrigin;

        private readonly Bindable<string> layoutSetting = new Bindable<string>();

        private const double duration = 500;

        /// <summary>
        /// Where the popup currently comes from.
        /// </summary>
        public ToolbarPopupDirection Direction { get; private set; }

        public ToolbarPopupContainer(ToolbarItem item, VisibilityContainer popup)
        {
            Item = item;
            this.popup = popup;

            originalAnchor = popup.Anchor;
            originalOrigin = popup.Origin;

            RelativeSizeAxes = Axes.Both;
            Child = popup;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.OilyToolbarLayout, layoutSetting);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            layoutSetting.BindValueChanged(v =>
            {
                Direction = ToolbarLayout.Parse(v.NewValue).Buttons.FirstOrDefault(b => b.Item == Item)?.PopupFrom ?? ToolbarPopupDirection.Default;
                placePopup();
            }, true);

            popup.State.BindValueChanged(s => Schedule(() => onStateChanged(s.NewValue)));
        }

        /// <summary>
        /// Puts popups that sit against one side of the screen (settings, notifications, now playing) on the side they come from.
        /// </summary>
        private void placePopup()
        {
            switch (Direction)
            {
                case ToolbarPopupDirection.Left:
                    popup.Anchor = withX(originalAnchor, Anchor.x0);
                    popup.Origin = withX(originalOrigin, Anchor.x0);
                    break;

                case ToolbarPopupDirection.Right:
                    popup.Anchor = withX(originalAnchor, Anchor.x2);
                    popup.Origin = withX(originalOrigin, Anchor.x2);
                    break;

                case ToolbarPopupDirection.Bottom when (originalAnchor & Anchor.y0) != 0 && popup.RelativeSizeAxes != Axes.Both && (popup.RelativeSizeAxes & Axes.Y) == 0:
                    // Small popups (now playing, profile) move to the bottom of the screen.
                    popup.Anchor = withY(originalAnchor, Anchor.y2);
                    popup.Origin = withY(originalOrigin, Anchor.y2);
                    break;

                default:
                    popup.Anchor = originalAnchor;
                    popup.Origin = originalOrigin;
                    break;
            }
        }

        /// <summary>
        /// Whether the popup sits on the right side of the screen (so the game nudges left while it's open, not right).
        /// </summary>
        public bool OnRight => (popup.Anchor & Anchor.x2) != 0;

        private void onStateChanged(Visibility state)
        {
            if (Direction == ToolbarPopupDirection.Default)
                return;

            Vector2 offscreen = offscreenOffset();

            if (state == Visibility.Visible)
            {
                // Skip lazer's own animation, so the popup only slides in from the chosen side.
                popup.FinishTransforms(true);
                this.MoveTo(offscreen).MoveTo(Vector2.Zero, duration, Easing.OutQuint);
            }
            else
            {
                // Come back once it has faded out, so parts shown while it's closed (like notification toasts) stay on screen.
                this.MoveTo(offscreen, duration, Easing.OutQuint).Then().Delay(500).MoveTo(Vector2.Zero);
            }
        }

        private Vector2 offscreenOffset()
        {
            switch (Direction)
            {
                case ToolbarPopupDirection.Left:
                    return new Vector2(-DrawWidth, 0);

                case ToolbarPopupDirection.Right:
                    return new Vector2(DrawWidth, 0);

                case ToolbarPopupDirection.Top:
                    return new Vector2(0, -DrawHeight);

                case ToolbarPopupDirection.Bottom:
                    return new Vector2(0, DrawHeight);

                default:
                    return Vector2.Zero;
            }
        }

        private static Anchor withX(Anchor anchor, Anchor x) => (anchor & (Anchor.y0 | Anchor.y1 | Anchor.y2)) | x;

        private static Anchor withY(Anchor anchor, Anchor y) => (anchor & (Anchor.x0 | Anchor.x1 | Anchor.x2)) | y;
    }
}
