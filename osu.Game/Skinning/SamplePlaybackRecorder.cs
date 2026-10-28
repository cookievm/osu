// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio.Sample;

namespace osu.Game.Skinning
{
    /// <summary>
    /// oily: hears every skinnable sample that plays, so the replay renderer can mix the video's audio afterwards.
    /// </summary>
    public interface ISamplePlaybackListener
    {
        /// <param name="source">The drawable playing the sample. Its aggregate volume, balance and frequency apply.</param>
        /// <param name="sample">The sample from the skin.</param>
        void SamplePlayed(PoolableSkinnableSample source, ISample sample);

        void SampleStopped(PoolableSkinnableSample source);
    }

    /// <summary>
    /// oily: where <see cref="PoolableSkinnableSample"/> reports playback. Null outside of renders.
    /// </summary>
    public static class SamplePlaybackRecorder
    {
        public static ISamplePlaybackListener? Listener { get; set; }
    }
}
