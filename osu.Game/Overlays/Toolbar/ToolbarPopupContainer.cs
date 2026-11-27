// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    /// the toolbar settings pick for that button ("Popup comes from"), lined up with the button that opened it.
    /// </summary>
    public partial class ToolbarPopupContainer : Container
    {
        public readonly ToolbarItem Item;

        private readonly VisibilityContainer popup;
        private readonly Func<Drawable?> findButton;
        private readonly Anchor originalAnchor;
        private readonly Anchor originalOrigin;

        private readonly Bindable<string> layoutSetting = new Bindable<string>();

        private const double duration = 500;

        /// <summary>
        /// Where the popup comes from.
        /// </summary>
        public ToolbarPopupDirection Direction { get; private set; }

        /// <summary>
        /// Where the popup rests when open (lined up with its button).
        /// </summary>
        private Vector2 restPosition;

        /// <param name="item">The toolbar button that opens this popup.</param>
        /// <param name="popup">The popup.</param>
        /// <param name="findButton">Finds the button on screen (on a toolbar or the bottom bar), if it's shown.</param>
        public ToolbarPopupContainer(ToolbarItem item, VisibilityContainer popup, Func<Drawable?> findButton)
        {
            Item = item;
            this.popup = popup;
            this.findButton = findButton;

            originalAnchor = popup.Anchor;
            originalOrigin = popup.Origin;

            RelativeSizeAxes = Axes.Both;
            Child = popup;
        }

        /// <summary>
        /// Where each popup comes from in lazer (used until the player picks another side).
        /// </summary>
        public static ToolbarPopupDirection NaturalDirection(ToolbarItem item)
        {
            switch (item)
            {
                case ToolbarItem.Settings:
                    return ToolbarPopupDirection.Left;

                case ToolbarItem.Notifications:
                    return ToolbarPopupDirection.Right;

                case ToolbarItem.Music:
                case ToolbarItem.User:
                    return ToolbarPopupDirection.Top;

                default:
                    return ToolbarPopupDirection.Bottom;
            }
        }

        /// <summary>
        /// The side a button's popup comes from, from its saved setting.
        /// </summary>
        public static ToolbarPopupDirection DirectionOf(ToolbarButtonLayout? button, ToolbarItem item)
            => button == null || button.PopupFrom == ToolbarPopupDirection.Default ? NaturalDirection(item) : button.PopupFrom;

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
                Direction = DirectionOf(ToolbarLayout.Parse(v.NewValue).Buttons.FirstOrDefault(b => b.Item == Item), Item);
                placePopup();
            }, true);

            popup.State.BindValueChanged(s => Schedule(() => onStateChanged(s.NewValue)));
        }

        private bool horizontal => Direction == ToolbarPopupDirection.Left || Direction == ToolbarPopupDirection.Right;

        /// <summary>
        /// Whether the popup covers the whole screen across the side it comes from (so there's nothing to line up).
        /// </summary>
        private bool spansAcross => (popup.RelativeSizeAxes & (horizontal ? Axes.Y : Axes.X)) != 0;

        /// <summary>
        /// Puts the popup against the side it comes from. Across that side it sits in the middle, and gets moved to line up with its button when opened.
        /// </summary>
        private void placePopup()
        {
            Anchor anchor = originalAnchor;
            Anchor origin = originalOrigin;

            switch (Direction)
            {
                case ToolbarPopupDirection.Left:
                    anchor = withX(anchor, Anchor.x0);
                    origin = withX(origin, Anchor.x0);
                    break;

                case ToolbarPopupDirection.Right:
                    anchor = withX(anchor, Anchor.x2);
                    origin = withX(origin, Anchor.x2);
                    break;

                case ToolbarPopupDirection.Top:
                    anchor = withY(anchor, Anchor.y0);
                    origin = withY(origin, Anchor.y0);
                    break;

                case ToolbarPopupDirection.Bottom:
                    anchor = withY(anchor, Anchor.y2);
                    origin = withY(origin, Anchor.y2);
                    break;
            }

            if (!spansAcross)
            {
                if (horizontal)
                {
                    anchor = withY(anchor, Anchor.y1);
                    origin = withY(origin, Anchor.y1);
                }
                else
                {
                    anchor = withX(anchor, Anchor.x1);
                    origin = withX(origin, Anchor.x1);
                }
            }

            popup.Anchor = anchor;
            popup.Origin = origin;
        }

        /// <summary>
        /// Whether the popup sits on the right side of the screen (so the game nudges left while it's open, not right).
        /// </summary>
        public bool OnRight => (popup.Anchor & Anchor.x2) != 0;

        private void onStateChanged(Visibility state)
        {
            Vector2 offscreen = offscreenOffset();

            if (state == Visibility.Visible)
            {
                restPosition = lineUpWithButton();

                // Skip lazer's own animation, so the popup only slides in from the chosen side.
                popup.FinishTransforms(true);
                this.MoveTo(restPosition + offscreen).MoveTo(restPosition, duration, Easing.OutQuint);
            }
            else
            {
                // Come back once it has faded out, so parts shown while it's closed (like notification toasts) stay on screen.
                this.MoveTo(restPosition + offscreen, duration, Easing.OutQuint).Then().Delay(500).MoveTo(Vector2.Zero);
            }
        }

        /// <summary>
        /// How far to move the popup (across the side it comes from) so its middle lines up with its button, staying on screen.
        /// </summary>
        private Vector2 lineUpWithButton()
        {
            if (spansAcross || Parent == null)
                return Vector2.Zero;

            var button = findButton();

            if (button == null || !button.IsPresent)
                return Vector2.Zero;

            Vector2 buttonCentre = Parent.ToLocalSpace(button.ScreenSpaceDrawQuad.Centre) - Parent.ToLocalSpace(ScreenSpaceDrawQuad.Centre) + Position;
            Vector2 popupSize = popup.DrawSize * popup.Scale;
            Vector2 room = Vector2.ComponentMax(Vector2.Zero, (DrawSize - popupSize) / 2);

            return horizontal
                ? new Vector2(0, Math.Clamp(buttonCentre.Y, -room.Y, room.Y))
                : new Vector2(Math.Clamp(buttonCentre.X, -room.X, room.X), 0);
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

                default:
                    return new Vector2(0, DrawHeight);
            }
        }

        private static Anchor withX(Anchor anchor, Anchor x) => (anchor & (Anchor.y0 | Anchor.y1 | Anchor.y2)) | x;

        private static Anchor withY(Anchor anchor, Anchor y) => (anchor & (Anchor.x0 | Anchor.x1 | Anchor.x2)) | y;
    }
}
