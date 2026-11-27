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

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily (ilo, 2026-11-26): song select's back button looks like lazer's or osu!stable's.
    /// It draws nothing on screen; picking "Back button" in the skin editor's component list opens its settings.
    /// </summary>
    public partial class SongSelectBackButtonStyle : CompositeDrawable, ISettingsOnlySkinComponent
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource("Style", "Lazer's back button, or osu!stable's (the skin's menu-back picture, at the bottom left corner).")]
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
                Alpha = 0;
                AlwaysPresent = true;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!inControl || Parent?.ChildrenOfType<SongSelectBackButtonStyle>().FirstOrDefault() != this)
                return;

            registry.StableBackButton.Value = Style.Value == SongSelectPieceStyle.Stable;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (inControl)
                registry.StableBackButton.Value = false;

            base.Dispose(isDisposing);
        }
    }
}
