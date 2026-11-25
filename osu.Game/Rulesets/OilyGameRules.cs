// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Configuration;

namespace osu.Game.Rulesets
{
    /// <summary>
    /// oily: the gameplay rules chosen in settings ("Gamerules" tab). Only osu! standard uses them for now.
    /// </summary>
    /// <remarks>
    /// The rules are saved with every score, at the end of its client version (for example "2026.1005.0-oily.nonotelock"),
    /// so a replay is always played back with the rules it was played with.
    /// Scores without the "-oily" tag (official lazer, renders of lazer replays) use <see cref="LAZER"/>.
    /// </remarks>
    /// <param name="NoteLock">Stable's notelock: a later object can't be hit until earlier ones are judged.</param>
    /// <param name="SliderAccuracy">Lazer's slider head accuracy. When off, sliders are judged like stable (head timing doesn't matter).</param>
    public record OilyGameRules(bool NoteLock, bool SliderAccuracy)
    {
        /// <summary>
        /// The default o!ily rules.
        /// </summary>
        public static readonly OilyGameRules DEFAULT = new OilyGameRules(NoteLock: true, SliderAccuracy: true);

        /// <summary>
        /// Official lazer's rules, for scores not played in o!ily.
        /// </summary>
        public static readonly OilyGameRules LAZER = new OilyGameRules(NoteLock: false, SliderAccuracy: true);

        private const string tag = "-oily";
        private const string no_note_lock_flag = "nonotelock";
        private const string no_slider_accuracy_flag = "noslideracc";

        public static OilyGameRules FromConfig(OsuConfigManager? config)
        {
            if (config == null)
                return DEFAULT;

            return new OilyGameRules(config.Get<bool>(OsuSetting.GameRulesNoteLock), config.Get<bool>(OsuSetting.GameRulesSliderAccuracy));
        }

        /// <summary>
        /// Adds these rules to the end of a client version. An existing tag is replaced.
        /// </summary>
        public string TagClientVersion(string version)
        {
            int existing = version.IndexOf(tag, StringComparison.Ordinal);
            if (existing >= 0)
                version = version[..existing];

            var flags = new List<string>();
            if (!NoteLock) flags.Add(no_note_lock_flag);
            if (!SliderAccuracy) flags.Add(no_slider_accuracy_flag);

            return version + tag + string.Concat(flags.Select(f => "." + f));
        }

        /// <summary>
        /// Reads the rules a score was played with from its client version.
        /// </summary>
        public static OilyGameRules FromClientVersion(string? clientVersion)
        {
            int index = clientVersion?.IndexOf(tag, StringComparison.Ordinal) ?? -1;

            if (clientVersion == null || index < 0)
                return LAZER;

            string[] flags = clientVersion[(index + tag.Length)..].Split('.', StringSplitOptions.RemoveEmptyEntries);

            return new OilyGameRules(
                NoteLock: !flags.Contains(no_note_lock_flag),
                SliderAccuracy: !flags.Contains(no_slider_accuracy_flag));
        }
    }
}
