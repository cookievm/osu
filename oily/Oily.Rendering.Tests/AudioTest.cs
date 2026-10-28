// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Oily.Rendering.Audio;

namespace Oily.Rendering.Tests
{
    [TestFixture]
    public class AudioTest
    {
        private const int rate = AudioMixdown.SAMPLE_RATE;

        [Test]
        public void TestMonoIsCopiedToBothSides()
        {
            var clip = PcmClip.FromInterleaved(new[] { 0.5f, -0.25f }, 1, rate);

            Assert.That(clip.Samples, Is.EqualTo(new[] { 0.5f, 0.5f, -0.25f, -0.25f }));
        }

        [Test]
        public void TestResamplesToMixRate()
        {
            // One second at 24 kHz becomes one second at 48 kHz.
            var clip = PcmClip.FromInterleaved(new float[24000 * 2], 2, 24000);

            Assert.That(clip.Frames, Is.EqualTo(rate));
        }

        [Test]
        public void TestSampleLandsAtItsVideoTime()
        {
            var mixdown = new AudioMixdown();
            mixdown.Add(new SamplePlay(constant(0.5f, 10), StartMs: 100));

            float[] mix = mixdown.Mix(200);
            int start = rate / 10 * 2;

            Assert.That(mix.Length, Is.EqualTo(rate / 5 * 2));
            Assert.That(mix[start - 2], Is.Zero);
            Assert.That(mix[start], Is.EqualTo(0.5f));
            Assert.That(mix[start + 9 * 2 + 1], Is.EqualTo(0.5f));
            Assert.That(mix[start + 10 * 2], Is.Zero);
        }

        [Test]
        public void TestVolumeAndBalance()
        {
            var mixdown = new AudioMixdown();
            mixdown.Add(new SamplePlay(constant(1, 4), StartMs: 0, Volume: 0.5, Balance: 0.5));

            float[] mix = mixdown.Mix(1);

            // Panned right: the left side is turned down, the right side keeps the full (halved) volume.
            Assert.That(mix[0], Is.EqualTo(0.25f).Within(1e-6));
            Assert.That(mix[1], Is.EqualTo(0.5f).Within(1e-6));
        }

        [Test]
        public void TestLoopingSampleRepeatsUntilStopped()
        {
            var mixdown = new AudioMixdown();
            mixdown.Add(new SamplePlay(constant(0.5f, 10), StartMs: 0, Looping: true) { EndMs = 100 });

            float[] mix = mixdown.Mix(200);
            int stop = rate / 10 * 2;

            Assert.That(mix[stop - 2], Is.EqualTo(0.5f));
            Assert.That(mix[stop], Is.Zero);
        }

        [Test]
        public void TestFrequencyPlaysFaster()
        {
            var mixdown = new AudioMixdown();
            mixdown.Add(new SamplePlay(constant(0.5f, 100), StartMs: 0, Frequency: 2));

            float[] mix = mixdown.Mix(10);

            Assert.That(mix[49 * 2], Is.EqualTo(0.5f));
            Assert.That(mix[50 * 2], Is.Zero);
        }

        [Test]
        public void TestLoudMixIsTurnedDownInsteadOfClipping()
        {
            var mixdown = new AudioMixdown();
            for (int i = 0; i < 4; i++)
                mixdown.Add(new SamplePlay(constant(0.9f, 10), StartMs: 0));

            float[] mix = mixdown.Mix(1);

            Assert.That(mix.Max(Math.Abs), Is.LessThanOrEqualTo(1));
            Assert.That(mix[0], Is.EqualTo(0.98f).Within(1e-6));
        }

        [Test]
        public void TestWavHeader()
        {
            using var stream = new MemoryStream();
            AudioMixdown.WriteWav(stream, new[] { 0.25f, -0.25f });

            byte[] bytes = stream.ToArray();

            Assert.That(bytes.Length, Is.EqualTo(44 + 8));
            Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(BitConverter.ToInt16(bytes, 20), Is.EqualTo(3), "IEEE float");
            Assert.That(BitConverter.ToInt32(bytes, 24), Is.EqualTo(rate));
            Assert.That(BitConverter.ToSingle(bytes, 44), Is.EqualTo(0.25f));
        }

        [Test]
        public void TestMuxWithoutMusic()
        {
            var args = RenderAudioMuxer.BuildArguments("v.mp4", "h.wav", null, "out.mp4");

            Assert.That(args, Does.Not.Contain("-ss"));
            Assert.That(args.Count(a => a == "-i"), Is.EqualTo(2));
            Assert.That(args, Does.Contain("[1:a]alimiter=limit=0.95:level=false[audio]"));
            Assert.That(args.Last(), Is.EqualTo("out.mp4"));
            Assert.That(string.Join(' ', args), Does.Contain("-c:v copy"));
        }

        [Test]
        public void TestMuxSeeksMusicToTheFirstFrame()
        {
            var args = RenderAudioMuxer.BuildArguments("v.mp4", "h.wav", new MusicTrack("m.mp3", 12345), "out.mp4").ToList();

            int seek = args.IndexOf("-ss");
            Assert.That(args[seek + 1], Is.EqualTo("12.345"));
            Assert.That(args[seek + 2], Is.EqualTo("-i"));
            Assert.That(args[seek + 3], Is.EqualTo("m.mp3"));
        }

        [Test]
        public void TestMusicFilterForMods()
        {
            Assert.That(RenderAudioMuxer.MusicFilter(new MusicTrack("m", 0)), Is.EqualTo("aresample=48000,volume=0.8"));

            // Double Time: faster, same pitch.
            Assert.That(RenderAudioMuxer.MusicFilter(new MusicTrack("m", 0, Tempo: 1.5)), Does.Contain("atempo=1.5").And.Not.Contain("asetrate"));

            // Nightcore: faster and higher.
            Assert.That(RenderAudioMuxer.MusicFilter(new MusicTrack("m", 0, Frequency: 1.5)), Does.Contain("asetrate=72000").And.Not.Contain("atempo"));
        }

        [Test]
        public void TestLeadInDelaysTheMusic()
        {
            // The video starts 1.5 s of music time before the song, at Double Time: 1 s of silence.
            string filter = RenderAudioMuxer.MusicFilter(new MusicTrack("m", -1500, Tempo: 1.5));

            Assert.That(filter, Does.Contain("adelay=1000|1000"));
        }

        [Test]
        public void TestJobRecordsAudioByDefault()
        {
            var job = RenderJob.Parse("""{ "replay": "r.osr", "output": "o.mp4" }""");

            Assert.That(job.Audio, Is.True);
            Assert.That(job.ToRenderSettings().RecordAudio, Is.True);
            Assert.That(RenderJob.Parse("""{ "replay": "r.osr", "output": "o.mp4", "audio": false }""").ToRenderSettings().RecordAudio, Is.False);
        }

        private static PcmClip constant(float value, int frames) => new PcmClip(Enumerable.Repeat(value, frames * 2).ToArray());
    }
}
