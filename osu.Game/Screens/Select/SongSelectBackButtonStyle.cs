// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily (ilo, 2026-11-26): song select's back button looks like lazer's or osu!stable's.
    /// (ilo, 2026-11-27) In Stable it can be moved in the skin editor (this component is the handle; the button itself is drawn in the bottom bar).
    /// In Lazer it stays where lazer's back button is.
    /// </summary>
    public partial class SongSelectBackButtonStyle : CompositeDrawable, ISkinComponentWithLabel
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource("Style", "Lazer's back button, or osu!stable's (the skin's menu-back picture). In Stable it can be moved; in Lazer it stays in the bottom left corner.")]
        public Bindable<SongSelectPieceStyle> Style { get; } = new Bindable<SongSelectPieceStyle>();

        [Resolved]
        private SongSelectPieceRegistry registry { get; set; } = null!;

        private bool inControl;

        public SongSelectBackButtonStyle()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new SkinComponentLabel("Back button");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Only the one placed in song select's skin layer counts (the skin editor's list also makes one as a preview).
            for (var p = Parent; p != null; p = p.Parent)
            {
                if (p is SkinnableContainer)
                {
                    inControl = true;
                    break;
                }
            }

            if (inControl)
            {
                // A see-through box the size of the button, so the skin editor can pick and drag it.
                ClearInternal();
                AutoSizeAxes = Axes.None;
                Size = new Vector2(93, 45);
                Alpha = 0;
                AlwaysPresent = true;
            }
        }

        private bool isInCharge => inControl && Parent?.ChildrenOfType<SongSelectBackButtonStyle>().FirstOrDefault() == this;

        protected override void Update()
        {
            base.Update();

            if (!isInCharge || Parent == null)
                return;

            bool stable = Style.Value == SongSelectPieceStyle.Stable;
            registry.StableBackButton.Value = stable;

            // Same size as the button on screen, never scaled or turned.
            Scale = Vector2.One;
            Rotation = 0;

            if (registry.StableBackButtonScreenSize.X > 0 && registry.StableBackButtonScreenSize.Y > 0)
                Size = Parent.ToLocalSpace(registry.StableBackButtonScreenSize) - Parent.ToLocalSpace(Vector2.Zero);

            if (!stable)
            {
                // Lazer's back button can't be moved: stay in the corner.
                Anchor = Anchor.BottomLeft;
                Origin = Anchor.BottomLeft;
                Position = Vector2.Zero;
                registry.StableBackButtonPosition = null;
                return;
            }

            registry.StableBackButtonPosition = ScreenSpaceDrawQuad.BottomLeft;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (inControl)
            {
                registry.StableBackButton.Value = false;
                registry.StableBackButtonPosition = null;
            }

            base.Dispose(isDisposing);
        }
    }
}
