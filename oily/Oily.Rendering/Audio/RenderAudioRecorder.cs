// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using ManagedBass;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics.Audio;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Skinning;

namespace Oily.Rendering.Audio
{
    /// <summary>
    /// Records every gameplay sample during a render, with the video time it played at.
    /// The render runs slower than real time, so nothing can be recorded from the speakers; the sounds are mixed afterwards instead.
    /// </summary>
    public class RenderAudioRecorder : ISamplePlaybackListener
    {
        public readonly AudioMixdown Mixdown = new AudioMixdown();

        private readonly Func<double?> videoTime;
        private readonly Dictionary<PoolableSkinnableSample, SamplePlay> playing = new Dictionary<PoolableSkinnableSample, SamplePlay>();
        private readonly Dictionary<int, PcmClip?> clips = new Dictionary<int, PcmClip?>();

        private int unreadable;

        /// <summary>
        /// Music time at the first video frame, once rendering has started.
        /// </summary>
        public double? MusicStartTime { get; private set; }

        private double musicTempo = 1;
        private double musicFrequency = 1;

        /// <param name="videoTime">The video time of the frame being rendered, or null before the first frame.</param>
        public RenderAudioRecorder(Func<double?> videoTime)
        {
            this.videoTime = videoTime;
        }

        /// <summary>
        /// Called on the first video frame, to line the music up with the video.
        /// </summary>
        /// <param name="gameplayTime">Gameplay time shown on the first frame, which is also the music's time.</param>
        /// <param name="tempo">Speed change from mods that keeps pitch.</param>
        /// <param name="frequency">Speed change from mods that also changes pitch.</param>
        public void Begin(double gameplayTime, double tempo, double frequency)
        {
            MusicStartTime = gameplayTime;
            musicTempo = tempo;
            musicFrequency = frequency;
        }

        /// <summary>
        /// Copies the beatmap's music into <paramref name="folder"/> and says how it lines up with the video.
        /// Null if the beatmap has no music or rendering never started.
        /// </summary>
        public MusicTrack? ExtractMusic(WorkingBeatmap beatmap, string folder)
        {
            if (MusicStartTime is not double start)
                return null;

            string audioFile = beatmap.BeatmapInfo.Metadata.AudioFile;
            if (string.IsNullOrEmpty(audioFile))
                return null;

            string? storagePath = beatmap.BeatmapSetInfo.GetPathForFile(audioFile);
            if (storagePath == null)
                return null;

            using var source = beatmap.GetStream(storagePath);
            if (source == null)
                return null;

            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "music" + Path.GetExtension(audioFile));

            using (var file = File.Create(path))
                source.CopyTo(file);

            return new MusicTrack(path, start, musicTempo, musicFrequency);
        }

        public void SamplePlayed(PoolableSkinnableSample source, ISample sample)
        {
            if (videoTime() is not double time)
                return;

            var clip = readClip(sample);
            if (clip == null)
                return;

            var play = new SamplePlay(clip, time,
                source.AggregateVolume.Value,
                source.AggregateBalance.Value,
                source.AggregateFrequency.Value,
                source.Looping);

            Mixdown.Add(play);
            playing[source] = play;
        }

        public void SampleStopped(PoolableSkinnableSample source)
        {
            if (!playing.Remove(source, out var play))
                return;

            if (videoTime() is double time)
                play.EndMs = time;
        }

        private PcmClip? readClip(ISample sample)
        {
            int id = GetBassSampleId(sample);

            if (id == 0)
                return null;

            if (clips.TryGetValue(id, out var cached))
                return cached;

            PcmClip? clip = null;

            try
            {
                clip = ReadBassSample(id);
            }
            catch (Exception e)
            {
                if (unreadable++ < 5)
                    Logger.Log($"Couldn't read sample {sample.Name} for the video's audio: {e.Message}");
            }

            clips[id] = clip;
            return clip;
        }

        /// <summary>
        /// The BASS handle behind a framework sample, or 0 if there isn't one (for example a missing, silent sample).
        /// </summary>
        /// <remarks>
        /// The framework keeps the handle internal, so it is read by reflection.
        /// </remarks>
        public static int GetBassSampleId(ISample sample)
        {
            object current = sample;

            // A DrawableSample wraps the real sample.
            if (current is DrawableSample drawable && typeof(DrawableSample).GetField("sample", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(drawable) is ISample inner)
                current = inner;

            var property = current.GetType().GetProperty("SampleId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.GetValue(current) is int id ? id : 0;
        }

        /// <summary>
        /// Copies a loaded BASS sample's sound out of BASS.
        /// </summary>
        public static PcmClip ReadBassSample(int sampleId)
        {
            var info = Bass.SampleGetInfo(sampleId);

            if (info.Length <= 0 || info.Channels <= 0)
                throw new InvalidOperationException($"BASS couldn't describe the sample ({Bass.LastError}).");

            byte[] data = new byte[info.Length];

            if (!Bass.SampleGetData(sampleId, data))
                throw new InvalidOperationException($"BASS couldn't read the sample ({Bass.LastError}).");

            float[] samples;

            if (info.Flags.HasFlag(BassFlags.Float))
            {
                samples = MemoryMarshal.Cast<byte, float>(data).ToArray();
            }
            else if (info.Flags.HasFlag(BassFlags.Byte))
            {
                samples = new float[data.Length];
                for (int i = 0; i < data.Length; i++)
                    samples[i] = (data[i] - 128) / 128f;
            }
            else
            {
                var shorts = MemoryMarshal.Cast<byte, short>(data);
                samples = new float[shorts.Length];
                for (int i = 0; i < shorts.Length; i++)
                    samples[i] = shorts[i] / 32768f;
            }

            return PcmClip.FromInterleaved(samples, info.Channels, info.Frequency);
        }
    }
}
