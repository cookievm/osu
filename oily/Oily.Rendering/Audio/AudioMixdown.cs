// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Oily.Rendering.Audio
{
    /// <summary>
    /// Mixes recorded sample plays into one stereo track, in video time.
    /// </summary>
    public class AudioMixdown
    {
        public const int SAMPLE_RATE = 48000;

        private readonly List<SamplePlay> plays = new List<SamplePlay>();

        public IReadOnlyList<SamplePlay> Plays => plays;

        public void Add(SamplePlay play) => plays.Add(play);

        /// <summary>
        /// Mixes every play into a track <paramref name="lengthMs"/> long. Interleaved stereo floats.
        /// </summary>
        /// <remarks>
        /// If plays stack up above full scale, the whole track is turned down evenly instead of clipping.
        /// </remarks>
        public float[] Mix(double lengthMs)
        {
            int frames = msToFrames(lengthMs);
            float[] output = new float[frames * 2];

            foreach (var play in plays)
                mixInto(output, frames, play);

            float peak = 0;
            foreach (float s in output)
                peak = Math.Max(peak, Math.Abs(s));

            if (peak > 1)
            {
                float scale = 0.98f / peak;
                for (int i = 0; i < output.Length; i++)
                    output[i] *= scale;
            }

            return output;
        }

        private static void mixInto(float[] output, int frames, SamplePlay play)
        {
            var clip = play.Clip;

            if (clip.Frames == 0 || play.Volume <= 0)
                return;

            int start = msToFrames(play.StartMs);
            if (start >= frames)
                return;

            // A sample plays until it is stopped. Without a stop, a looping sample runs to the end of the video.
            int end = play.EndMs is double endMs ? Math.Min(frames, msToFrames(endMs)) : frames;

            // Balance works like BASS's pan: the far side is turned down, the near side stays at full volume.
            float left = (float)(play.Volume * (play.Balance > 0 ? 1 - play.Balance : 1));
            float right = (float)(play.Volume * (play.Balance < 0 ? 1 + play.Balance : 1));
            double rate = play.Frequency > 0 ? play.Frequency : 1;

            for (int frame = Math.Max(0, start); frame < end; frame++)
            {
                double position = (frame - start) * rate;

                if (play.Looping)
                    position %= clip.Frames;
                else if (position >= clip.Frames)
                    break;

                int index = (int)position;
                int next = index + 1 < clip.Frames ? index + 1 : (play.Looping ? 0 : index);
                float t = (float)(position - index);

                float l = clip.Samples[index * 2] + (clip.Samples[next * 2] - clip.Samples[index * 2]) * t;
                float r = clip.Samples[index * 2 + 1] + (clip.Samples[next * 2 + 1] - clip.Samples[index * 2 + 1]) * t;

                output[frame * 2] += l * left;
                output[frame * 2 + 1] += r * right;
            }
        }

        private static int msToFrames(double ms) => (int)Math.Round(ms * SAMPLE_RATE / 1000);

        /// <summary>
        /// Writes interleaved stereo floats as a 32-bit float WAV file.
        /// </summary>
        public static void WriteWav(Stream stream, float[] stereoSamples)
        {
            const int channels = 2;
            const int bytes_per_sample = 4;
            int dataLength = stereoSamples.Length * bytes_per_sample;

            using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)3); // IEEE float
            writer.Write((short)channels);
            writer.Write(SAMPLE_RATE);
            writer.Write(SAMPLE_RATE * channels * bytes_per_sample);
            writer.Write((short)(channels * bytes_per_sample));
            writer.Write((short)(bytes_per_sample * 8));

            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);

            foreach (float sample in stereoSamples)
                writer.Write(sample);
        }
    }

    /// <summary>
    /// One sample played during the render.
    /// </summary>
    /// <param name="Clip">The sound.</param>
    /// <param name="StartMs">Video time it started at.</param>
    /// <param name="Volume">0 to 1.</param>
    /// <param name="Balance">-1 (left) to 1 (right).</param>
    /// <param name="Frequency">Playback speed, 1 for normal.</param>
    /// <param name="Looping">Whether it repeats until stopped.</param>
    public record SamplePlay(PcmClip Clip, double StartMs, double Volume = 1, double Balance = 0, double Frequency = 1, bool Looping = false)
    {
        /// <summary>
        /// Video time the sample was stopped at, or null if it never was.
        /// </summary>
        public double? EndMs { get; set; }
    }
}
