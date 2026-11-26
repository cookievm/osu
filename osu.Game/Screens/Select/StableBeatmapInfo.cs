// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// oily: the selected beatmap's details drawn like osu!stable's top left text block:
    /// "Artist - Title [Difficulty]", "Mapped by ...", length / BPM / objects, object counts, then CS / AR / OD / HP / stars.
    /// The skin's songselect-top picture goes behind it when the skin has one.
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

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        private readonly Container backgroundContainer;
        private readonly TruncatingSpriteText titleText;
        private readonly OsuSpriteText mapperText;
        private readonly OsuSpriteText lengthText;
        private readonly OsuSpriteText countsText;
        private readonly OsuSpriteText difficultyText;

        private CancellationTokenSource? cancellationSource;
        private IBindable<StarDifficulty>? starDifficulty;
        private string attributes = string.Empty;

        public StableBeatmapInfo()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                backgroundContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = 10, Vertical = 6 },
                    Children = new Drawable[]
                    {
                        titleText = new TruncatingSpriteText
                        {
                            Font = OsuFont.GetFont(size: 24),
                            RelativeSizeAxes = Axes.X,
                        },
                        mapperText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 15),
                        },
                        lengthText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                            Margin = new MarginPadding { Top = 2 },
                        },
                        countsText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 13),
                        },
                        difficultyText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            onSkinChanged();

            // Like lazer's own display, wait a frame so the beatmap, ruleset and mods all agree before calculating.
            working.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));
            ruleset.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));
            mods.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));

            updateDisplay();
        }

        private void onSkinChanged()
        {
            var top = skin.GetTexture("songselect-top");

            if (top != null)
            {
                // Drawn at its own size from the top left, like stable (lazer's UI uses stable's 1024x768 units).
                // Stable repeats the picture's rightmost pixels to fill a wide screen; the last column is stretched here to do the same.
                backgroundContainer.Children = new Drawable[]
                {
                    new Sprite
                    {
                        Texture = top.Crop(new osu.Framework.Graphics.Primitives.RectangleF(0.995f, 0, 0.005f, 1), Axes.Both),
                        X = top.DisplayWidth - 1,
                        Size = new Vector2(4000, top.DisplayHeight),
                    },
                    new Sprite
                    {
                        Texture = top,
                        Size = new Vector2(top.DisplayWidth, top.DisplayHeight),
                    },
                };
            }
            else
            {
                backgroundContainer.Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.6f),
                };
            }
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

                string bpm = bpmMin == bpmMax ? $"{bpmMin}" : $"{bpmMin}-{bpmMax}";
                var counts = playable.GetStatistics().ToList();

                Schedule(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    lengthText.Text = $"Length: {length.ToFormattedDuration()} BPM: {bpm} Objects: {playable.HitObjects.Count}";
                    countsText.Text = string.Join(" ", counts.Select(c => $"{c.Name}: {c.Content}"));
                });
            }, token);
        }

        private void updateDifficultyText()
        {
            double stars = starDifficulty?.Value.Stars ?? 0;
            difficultyText.Text = $"{attributes} Stars:{stars:0.##}".Trim();
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellationSource?.Cancel();

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }
    }
}
