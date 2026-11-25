// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings.Sections.GameRules;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>
    /// oily: "Gamerules" settings tab. Chooses the gameplay rules that are saved with each score.
    /// </summary>
    public partial class GameRulesSection : SettingsSection
    {
        public override LocalisableString Header => "Gamerules";

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = FontAwesome.Solid.Gavel
        };

        public GameRulesSection()
        {
            Children = new Drawable[]
            {
                new OsuGameRulesSettings(),
            };
        }
    }
}
