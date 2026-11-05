// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;

namespace Oily.Rendering.Tests
{
    [TestFixture]
    public class SkinLayoutFallbackTest
    {
        [Test]
        public void OtherModesBorrowTheOsuLayout()
        {
            var layout = new SkinLayoutInfo();
            layout.Update(new OsuRuleset().RulesetInfo, new[]
            {
                new SerialisedDrawableInfo { Type = typeof(DefaultScoreCounter) },
                new SerialisedDrawableInfo { Type = typeof(DrawableGameplayLeaderboard) },
                new SerialisedDrawableInfo { Type = typeof(DefaultSpinner) },
            });

            Assert.That(layout.TryGetOsuLayoutFor(new ManiaRuleset().RulesetInfo, out var mania), Is.True);
            Assert.That(mania, Has.Length.EqualTo(2), "the osu!-only spinner is left out");
            Assert.That(layout.TryGetOsuLayoutFor(new OsuRuleset().RulesetInfo, out _), Is.False);
            Assert.That(layout.TryGetOsuLayoutFor(null, out _), Is.False);

            layout.Update(new ManiaRuleset().RulesetInfo, new[] { new SerialisedDrawableInfo { Type = typeof(DefaultScoreCounter) } });
            Assert.That(layout.TryGetOsuLayoutFor(new ManiaRuleset().RulesetInfo, out _), Is.False, "a mode with its own layout keeps it");
        }
    }
}
