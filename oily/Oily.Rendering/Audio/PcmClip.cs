// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace Oily.Rendering.Audio
{
    /// <summary>
    /// A decoded sound: interleaved stereo floats at <see cref="AudioMixdown.SAMPLE_RATE"/>.
    /// </summary>
    public sealed class PcmClip
    {
        public readonly float[] Samples;

        public int Frames => Samples.Length / 2;

        public PcmClip(float[] stereoSamples)
        {
            if (stereoSamples.Length % 2 != 0)
                throw new ArgumentException("Stereo samples must come in left/right pairs.", nameof(stereoSamples));

            Samples = stereoSamples;
        }

        /// <summary>
        /// Converts interleaved samples with any channel count and rate to stereo at the mix rate.
        /// Mono is copied to both sides; channels past the second are dropped.
        /// </summary>
        public static PcmClip FromInterleaved(ReadOnlySpan<float> samples, int channels, int sampleRate)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1);

            int sourceFrames = samples.Length / channels;

            if (sourceFrames == 0)
                return new PcmClip(Array.Empty<float>());

            double step = (double)sampleRate / AudioMixdown.SAMPLE_RATE;
            int frames = Math.Max(1, (int)Math.Round(sourceFrames / step));
            float[] output = new float[frames * 2];

            for (int i = 0; i < frames; i++)
            {
                // Linear interpolation is plenty for short hitsounds.
                double position = i * step;
                int index = Math.Min((int)position, sourceFrames - 1);
                int next = Math.Min(index + 1, sourceFrames - 1);
                float t = (float)(position - index);

                for (int side = 0; side < 2; side++)
                {
                    int channel = Math.Min(side, channels - 1);
                    float a = samples[index * channels + channel];
                    float b = samples[next * channels + channel];
                    output[i * 2 + side] = a + (b - a) * t;
                }
            }

            return new PcmClip(output);
        }
    }
}
