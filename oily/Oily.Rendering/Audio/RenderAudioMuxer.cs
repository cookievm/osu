// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Oily.Rendering.Audio
{
    /// <summary>
    /// Where the beatmap's music sits in the video.
    /// </summary>
    /// <param name="File">The music file.</param>
    /// <param name="StartTime">Music time at the first video frame, in milliseconds. Negative during lead-in, before the music starts.</param>
    /// <param name="Tempo">Speed change that keeps pitch (Double Time).</param>
    /// <param name="Frequency">Speed change that also changes pitch (Nightcore, Daycore).</param>
    /// <param name="Volume">0 to 1.</param>
    public record MusicTrack(string File, double StartTime, double Tempo = 1, double Frequency = 1, double Volume = 0.5)
    {
        /// <summary>
        /// How much faster than real time the music plays in the video.
        /// </summary>
        public double Rate => Tempo * Frequency;
    }

    /// <summary>
    /// Builds the ffmpeg command that adds the music and the mixed hitsounds to a rendered video.
    /// </summary>
    public static class RenderAudioMuxer
    {
        /// <summary>
        /// Volume of the mixed hitsounds. Music and hitsounds both at full scale come out far too loud once summed.
        /// </summary>
        public const double HITSOUND_VOLUME = 0.5;

        // Peaks stay below -1 dBFS, which leaves room for the AAC encoder's overshoot.
        private const string limiter = "alimiter=limit=0.89:level=false";

        public static IReadOnlyList<string> BuildArguments(string video, string hitsounds, MusicTrack? music, string output)
        {
            var args = new List<string>
            {
                "-y",
                "-hide_banner",
                "-loglevel", "warning",
                "-i", video,
                "-i", hitsounds,
            };

            string filter;

            if (music != null)
            {
                if (music.StartTime > 0)
                    args.AddRange(new[] { "-ss", seconds(music.StartTime) });

                args.AddRange(new[] { "-i", music.File });

                filter = $"[1:a]{hitsounds_filter}[hits];[2:a]{MusicFilter(music)}[music];[hits][music]amix=inputs=2:normalize=0:duration=first,{limiter}[audio]";
            }
            else
                filter = $"[1:a]{hitsounds_filter},{limiter}[audio]";

            args.AddRange(new[]
            {
                "-filter_complex", filter,
                "-map", "0:v",
                "-map", "[audio]",
                "-c:v", "copy",
                "-c:a", "aac",
                "-b:a", "192k",
                "-movflags", "+faststart",
                "-shortest",
                output,
            });

            return args;
        }

        /// <summary>
        /// The ffmpeg filter chain for the music: speed, pitch, lead-in silence and volume.
        /// </summary>
        public static string MusicFilter(MusicTrack music)
        {
            const int rate = AudioMixdown.SAMPLE_RATE;

            var filters = new List<string> { $"aresample={rate}" };

            // Speeding up the sample rate changes speed and pitch together, like a record played faster.
            if (Math.Abs(music.Frequency - 1) > 0.0001)
                filters.Add($"asetrate={(int)Math.Round(rate * music.Frequency)},aresample={rate}");

            if (Math.Abs(music.Tempo - 1) > 0.0001)
                filters.Add($"atempo={number(music.Tempo)}");

            // Before the music starts, the video plays silence.
            if (music.StartTime < 0)
            {
                long delay = (long)Math.Round(-music.StartTime / music.Rate);
                filters.Add($"adelay={delay}|{delay}");
            }

            filters.Add($"volume={number(music.Volume)}");

            return string.Join(',', filters);
        }

        private static readonly string hitsounds_filter = $"volume={number(HITSOUND_VOLUME)}";

        private static string seconds(double ms) => (ms / 1000).ToString("0.###", CultureInfo.InvariantCulture);

        private static string number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
