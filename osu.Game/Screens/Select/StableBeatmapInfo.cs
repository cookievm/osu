// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: the selected beatmap's details drawn like osu!stable's top left text block, in stable's font and at stable's places:
    /// "Artist - Title [Difficulty]", "Mapped by ...", length / BPM / objects, object counts, then CS / AR / OD / HP / stars.
    /// The skin's songselect-top picture is drawn behind it by <see cref="StableSongSelectTop"/>.
    /// </summary>
    public partial class StableBeatmapInfo : CompositeDrawable
    {
        [Resolved]
        private IBindable<WorkingBeatmap> working { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private readonly OsuSpriteText titleText;
        private readonly OsuSpriteText mapperText;
        private readonly OsuSpriteText lengthText;
        private readonly OsuSpriteText countsText;
        private readonly OsuSpriteText difficultyText;

        private CancellationTokenSource? cancellationSource;
        private IBindable<StarDifficulty>? starDifficulty;
        private string attributes = string.Empty;

        public StableBeatmapInfo()
        {
            // Placed in stable's 768 high space by StableSongSelectLayer. Positions are each line's baseline, measured from stable.
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                titleText = line(39.5f, StableFont.TITLE_BASELINE, StableFont.Get(StableFont.TITLE_SIZE)),
                mapperText = line(41, StableFont.MAPPER_BASELINE, StableFont.Get(StableFont.MAPPER_SIZE)),
                lengthText = line(5.3f, StableFont.LENGTH_BASELINE, StableFont.Get(StableFont.LENGTH_SIZE, bold: true)),
                countsText = line(5.3f, StableFont.COUNTS_BASELINE, StableFont.Get(StableFont.COUNTS_SIZE)),
                difficultyText = line(4.3f, StableFont.DIFFICULTY_BASELINE, StableFont.Get(StableFont.DIFFICULTY_SIZE)),
            };
        }

        private static OsuSpriteText line(float x, float baseline, FontUsage font) => new OsuSpriteText
        {
            Position = new Vector2(x, baseline - StableFont.Ascent(font.Size)),
            Font = font,
            Shadow = false,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Like lazer's own display, wait a frame so the beatmap, ruleset and mods all agree before calculating.
            working.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));
            ruleset.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));
            mods.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));

            updateDisplay();
        }

        private void updateDisplay()
        {
            cancellationSource?.Cancel();
            cancellationSource = new CancellationTokenSource();
            var token = cancellationSource.Token;

            var beatmap = working.Value;
            var metadata = beatmap.Metadata;

            if (working.IsDefault)
            {
                titleText.Text = string.Empty;
                mapperText.Text = lengthText.Text = countsText.Text = difficultyText.Text = string.Empty;
                return;
            }

            titleText.Text = LocalisableString.Interpolate(
                $"{new RomanisableString(metadata.ArtistUnicode, metadata.Artist)} - {new RomanisableString(metadata.TitleUnicode, metadata.Title)} [{beatmap.BeatmapInfo.DifficultyName}]");
            mapperText.Text = $"Mapped by {metadata.Author.Username}";

            var rulesetInfo = ruleset.Value;
            var currentMods = mods.Value;

            Ruleset? rulesetInstance = rulesetInfo?.CreateInstance();

            attributes = rulesetInstance == null
                ? string.Empty
                : string.Join(" ", rulesetInstance.GetBeatmapAttributesForDisplay(beatmap.BeatmapInfo, currentMods)
                                                  .Select(a => $"{a.Acronym}:{Math.Round(a.AdjustedValue, 2)}"));

            starDifficulty = difficultyCache.GetBindableDifficulty(beatmap.BeatmapInfo, token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            starDifficulty.BindValueChanged(_ => updateDifficultyText(), true);

            if (rulesetInfo == null)
                return;

            Task.Run(() =>
            {
                // Can take a moment, so it runs off the update thread.
                IBeatmap playable;

                try
                {
                    playable = beatmap.GetPlayableBeatmap(rulesetInfo, currentMods, token);
                }
                catch
                {
                    // The beatmap, ruleset and mods can briefly not fit together while they change.
                    return;
                }

                double rate = ModUtils.CalculateRateWithMods(currentMods);
                int bpmMax = FormatUtils.RoundBPM(playable.ControlPointInfo.BPMMaximum, rate);
                int bpmMin = FormatUtils.RoundBPM(playable.ControlPointInfo.BPMMinimum, rate);
                double length = Math.Round(beatmap.BeatmapInfo.Length / rate);

                // Stable: "165-191 (180)", the most common BPM in brackets.
                int bpmCommon = FormatUtils.RoundBPM(60000 / playable.GetMostCommonBeatLength(), rate);
                string bpm = bpmMin == bpmMax ? $"{bpmMin}" : $"{bpmMin}-{bpmMax} ({bpmCommon})";
                var counts = playable.GetStatistics().ToList();

                Schedule(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    lengthText.Text = $"Length: {(int)(length / 60000):00}:{(int)(length / 1000) % 60:00} BPM: {bpm} Objects: {playable.HitObjects.Count}";
                    countsText.Text = string.Join(" ", counts.Select(c => $"{c.Name}: {c.Content}"));
                });
            }, token);
        }

        private void updateDifficultyText()
        {
            double stars = starDifficulty?.Value.Stars ?? 0;
            difficultyText.Text = $"{attributes} Star Rating: {stars:0.##}\u2605".Trim();
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellationSource?.Cancel();

            base.Dispose(isDisposing);
        }
    }
}
