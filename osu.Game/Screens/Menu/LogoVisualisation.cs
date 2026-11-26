// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// A visualiser that reacts to music coming from beatmaps.
    /// </summary>
    public partial class LogoVisualisation : Drawable
    {
        /// <summary>
        /// The number of bars to jump each update iteration.
        /// </summary>
        private const int index_change = 5;

        /// <summary>
        /// The maximum length of each bar in the visualiser. Will be reduced when kiai is not activated.
        /// </summary>
        private const float bar_length = 600;

        /// <summary>
        /// The number of bars in one rotation of the visualiser.
        /// </summary>
        private const int bars_per_visualiser = 200;

        /// <summary>
        /// How many times we should stretch around the circumference (overlapping overselves).
        /// </summary>
        private const float visualiser_rounds = 5;

        /// <summary>
        /// oily (ilo, 2026-11-26): lay the bars around the heart logo's edge instead of a circle.
        /// </summary>
        public bool HeartShape { get; set; }

        // oily: points around the heart's outer edge, in logo texture space (0-1), evenly spaced, clockwise from the right.
        private static readonly float[] heart_outline =
        {
            0.9806f, 0.4103f, 0.9778f, 0.4245f, 0.9742f, 0.4385f, 0.9702f, 0.4524f,
            0.9653f, 0.4660f, 0.9598f, 0.4794f, 0.9537f, 0.4926f, 0.9468f, 0.5053f,
            0.9392f, 0.5177f, 0.9314f, 0.5299f, 0.9227f, 0.5415f, 0.9136f, 0.5527f,
            0.9039f, 0.5634f, 0.8936f, 0.5737f, 0.8828f, 0.5834f, 0.8715f, 0.5924f,
            0.8597f, 0.6008f, 0.8477f, 0.6089f, 0.8352f, 0.6163f, 0.8225f, 0.6232f,
            0.8099f, 0.6304f, 0.7974f, 0.6377f, 0.7848f, 0.6450f, 0.7725f, 0.6526f,
            0.7602f, 0.6603f, 0.7481f, 0.6682f, 0.7360f, 0.6762f, 0.7241f, 0.6845f,
            0.7123f, 0.6929f, 0.7006f, 0.7014f, 0.6892f, 0.7103f, 0.6778f, 0.7192f,
            0.6666f, 0.7284f, 0.6557f, 0.7378f, 0.6448f, 0.7473f, 0.6344f, 0.7576f,
            0.6238f, 0.7673f, 0.6135f, 0.7776f, 0.6037f, 0.7884f, 0.5940f, 0.7991f,
            0.5845f, 0.8100f, 0.5754f, 0.8213f, 0.5664f, 0.8325f, 0.5576f, 0.8441f,
            0.5491f, 0.8558f, 0.5409f, 0.8677f, 0.5328f, 0.8798f, 0.5250f, 0.8920f,
            0.5176f, 0.9045f, 0.5101f, 0.9169f, 0.5028f, 0.9295f, 0.4949f, 0.9276f,
            0.4877f, 0.9150f, 0.4803f, 0.9025f, 0.4728f, 0.8901f, 0.4650f, 0.8778f,
            0.4570f, 0.8658f, 0.4484f, 0.8541f, 0.4402f, 0.8421f, 0.4313f, 0.8307f,
            0.4223f, 0.8193f, 0.4131f, 0.8082f, 0.4034f, 0.7974f, 0.3937f, 0.7867f,
            0.3839f, 0.7760f, 0.3736f, 0.7657f, 0.3629f, 0.7560f, 0.3525f, 0.7459f,
            0.3415f, 0.7365f, 0.3307f, 0.7268f, 0.3195f, 0.7177f, 0.3080f, 0.7089f,
            0.2965f, 0.7002f, 0.2848f, 0.6916f, 0.2731f, 0.6832f, 0.2612f, 0.6749f,
            0.2491f, 0.6668f, 0.2369f, 0.6591f, 0.2245f, 0.6515f, 0.2122f, 0.6439f,
            0.1997f, 0.6366f, 0.1871f, 0.6294f, 0.1745f, 0.6222f, 0.1618f, 0.6152f,
            0.1494f, 0.6076f, 0.1374f, 0.5996f, 0.1257f, 0.5910f, 0.1145f, 0.5818f,
            0.1038f, 0.5721f, 0.0935f, 0.5618f, 0.0842f, 0.5508f, 0.0751f, 0.5396f,
            0.0664f, 0.5280f, 0.0585f, 0.5158f, 0.0512f, 0.5033f, 0.0443f, 0.4905f,
            0.0383f, 0.4773f, 0.0330f, 0.4638f, 0.0281f, 0.4502f, 0.0241f, 0.4363f,
            0.0210f, 0.4222f, 0.0181f, 0.4081f, 0.0161f, 0.3938f, 0.0151f, 0.3794f,
            0.0151f, 0.3649f, 0.0151f, 0.3503f, 0.0161f, 0.3359f, 0.0173f, 0.3215f,
            0.0200f, 0.3074f, 0.0230f, 0.2933f, 0.0268f, 0.2793f, 0.0311f, 0.2655f,
            0.0363f, 0.2520f, 0.0420f, 0.2387f, 0.0485f, 0.2257f, 0.0558f, 0.2131f,
            0.0635f, 0.2008f, 0.0718f, 0.1890f, 0.0807f, 0.1776f, 0.0900f, 0.1665f,
            0.1002f, 0.1561f, 0.1105f, 0.1459f, 0.1216f, 0.1367f, 0.1332f, 0.1279f,
            0.1450f, 0.1196f, 0.1573f, 0.1118f, 0.1699f, 0.1048f, 0.1829f, 0.0982f,
            0.1962f, 0.0925f, 0.2098f, 0.0874f, 0.2237f, 0.0832f, 0.2376f, 0.0795f,
            0.2516f, 0.0761f, 0.2658f, 0.0737f, 0.2802f, 0.0725f, 0.2946f, 0.0717f,
            0.3090f, 0.0708f, 0.3234f, 0.0718f, 0.3378f, 0.0728f, 0.3521f, 0.0747f,
            0.3662f, 0.0776f, 0.3803f, 0.0810f, 0.3942f, 0.0850f, 0.4078f, 0.0899f,
            0.4213f, 0.0952f, 0.4345f, 0.1012f, 0.4473f, 0.1079f, 0.4597f, 0.1154f,
            0.4718f, 0.1234f, 0.4836f, 0.1318f, 0.4947f, 0.1411f, 0.5062f, 0.1398f,
            0.5173f, 0.1305f, 0.5290f, 0.1220f, 0.5413f, 0.1143f, 0.5538f, 0.1069f,
            0.5666f, 0.1002f, 0.5798f, 0.0943f, 0.5933f, 0.0889f, 0.6070f, 0.0843f,
            0.6210f, 0.0803f, 0.6350f, 0.0769f, 0.6492f, 0.0747f, 0.6635f, 0.0728f,
            0.6779f, 0.0718f, 0.6923f, 0.0708f, 0.7067f, 0.0718f, 0.7211f, 0.0727f,
            0.7354f, 0.0741f, 0.7496f, 0.0767f, 0.7637f, 0.0799f, 0.7776f, 0.0836f,
            0.7914f, 0.0882f, 0.8049f, 0.0933f, 0.8182f, 0.0993f, 0.8311f, 0.1058f,
            0.8437f, 0.1130f, 0.8559f, 0.1209f, 0.8678f, 0.1291f, 0.8790f, 0.1383f,
            0.8902f, 0.1475f, 0.9004f, 0.1578f, 0.9103f, 0.1684f, 0.9199f, 0.1792f,
            0.9286f, 0.1908f, 0.9368f, 0.2028f, 0.9443f, 0.2151f, 0.9515f, 0.2277f,
            0.9580f, 0.2407f, 0.9637f, 0.2541f, 0.9686f, 0.2677f, 0.9729f, 0.2815f,
            0.9766f, 0.2955f, 0.9797f, 0.3096f, 0.9819f, 0.3238f, 0.9833f, 0.3382f,
            0.9839f, 0.3526f, 0.9839f, 0.3672f, 0.9839f, 0.3817f, 0.9827f, 0.3961f,
        };

        /// <summary>
        /// How much should each bar go down each millisecond (based on a full bar).
        /// </summary>
        private const float decay_per_millisecond = 0.0024f;

        /// <summary>
        /// Number of milliseconds between each amplitude update.
        /// </summary>
        private const float time_between_updates = 50;

        /// <summary>
        /// The minimum amplitude to show a bar.
        /// </summary>
        private const float amplitude_dead_zone = 1f / bar_length;

        private int indexOffset;

        /// <summary>
        /// The relative movement of bars based on input amplification. Defaults to 1.
        /// </summary>
        public float Magnitude { get; set; } = 1;

        private readonly float[] frequencyAmplitudes = new float[256];

        private IShader shader = null!;
        private Texture texture = null!;

        public LogoVisualisation()
        {
            Blending = BlendingParameters.Additive;
        }

        private readonly List<IHasAmplitudes> amplitudeSources = new List<IHasAmplitudes>();

        public void AddAmplitudeSource(IHasAmplitudes amplitudeSource)
        {
            amplitudeSources.Add(amplitudeSource);
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer, ShaderManager shaders)
        {
            texture = renderer.WhitePixel;
            shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }

        private readonly float[] temporalAmplitudes = new float[ChannelAmplitudes.AMPLITUDES_SIZE];

        [Resolved]
        private IBeatSyncProvider beatSyncProvider { get; set; } = null!;

        private void updateAmplitudes()
        {
            for (int i = 0; i < temporalAmplitudes.Length; i++)
                temporalAmplitudes[i] = 0;

            addAmplitudesFromSource(beatSyncProvider);

            foreach (var source in amplitudeSources)
                addAmplitudesFromSource(source);

            float kiaiMultiplier = beatSyncProvider.CheckIsKiaiTime() ? 1 : 0.5f;

            for (int i = 0; i < bars_per_visualiser; i++)
            {
                float targetAmplitude = (temporalAmplitudes[(i + indexOffset) % bars_per_visualiser]) * kiaiMultiplier;

                if (targetAmplitude > frequencyAmplitudes[i])
                    frequencyAmplitudes[i] = targetAmplitude;
            }

            indexOffset = (indexOffset + index_change) % bars_per_visualiser;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var delayed = Scheduler.AddDelayed(updateAmplitudes, time_between_updates, true);
            delayed.PerformRepeatCatchUpExecutions = false;
        }

        protected override void Update()
        {
            base.Update();

            float decayFactor = (float)Time.Elapsed * decay_per_millisecond;

            for (int i = 0; i < bars_per_visualiser; i++)
            {
                //3% of extra bar length to make it a little faster when bar is almost at it's minimum
                frequencyAmplitudes[i] -= decayFactor * (frequencyAmplitudes[i] + 0.03f);
                if (frequencyAmplitudes[i] < 0)
                    frequencyAmplitudes[i] = 0;
            }

            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new VisualisationDrawNode(this);

        private void addAmplitudesFromSource(IHasAmplitudes source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var amplitudes = source.CurrentAmplitudes.FrequencyAmplitudes.Span;

            for (int i = 0; i < amplitudes.Length; i++)
            {
                if (i < temporalAmplitudes.Length)
                    temporalAmplitudes[i] += amplitudes[i];
            }
        }

        private class VisualisationDrawNode : DrawNode
        {
            protected new LogoVisualisation Source => (LogoVisualisation)base.Source;

            private IShader shader = null!;
            private Texture texture = null!;

            // Assuming the logo is a circle, we don't need a second dimension.
            private float size;

            private static readonly Color4 transparent_white = Color4.White.Opacity(0.2f);

            private readonly float[] audioData = new float[256];

            private IVertexBatch<TexturedVertex2D>? vertexBatch;
            private bool heartShape;

            /// <summary>
            /// oily: a point on the heart's edge in this visualiser's 0-1 space (it is drawn <see cref="OsuLogo.SCALE_ADJUST"/> of the logo's size).
            /// </summary>
            private static Vector2 heartPoint(int index)
            {
                index = ((index % bars_per_visualiser) + bars_per_visualiser) % bars_per_visualiser;
                var texturePoint = new Vector2(heart_outline[index * 2], heart_outline[index * 2 + 1]);
                return (texturePoint - new Vector2(0.5f)) / OsuLogo.SCALE_ADJUST.X + new Vector2(0.5f);
            }

            public VisualisationDrawNode(LogoVisualisation source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                shader = Source.shader;
                texture = Source.texture;
                size = Source.DrawSize.X;
                heartShape = Source.HeartShape;

                Source.frequencyAmplitudes.AsSpan().CopyTo(audioData);
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(100, 10);

                shader.Bind();

                Vector2 inflation = DrawInfo.MatrixInverse.ExtractScale().Xy;

                ColourInfo colourInfo = DrawColourInfo.Colour;
                colourInfo.ApplyChild(transparent_white);

                for (int j = 0; j < visualiser_rounds; j++)
                {
                    for (int i = 0; i < bars_per_visualiser; i++)
                    {
                        if (audioData[i] < amplitude_dead_zone)
                            continue;

                        float rotation = float.DegreesToRadians(i / (float)bars_per_visualiser * 360 + j * 360 / visualiser_rounds);
                        float rotationCos = MathF.Cos(rotation);
                        float rotationSin = MathF.Sin(rotation);
                        // taking the cos and sin to the 0..1 range
                        var barPosition = new Vector2(rotationCos / 2 + 0.5f, rotationSin / 2 + 0.5f) * size;

                        var barSize = new Vector2(size * MathF.Sqrt(2 * (1 - MathF.Cos(float.DegreesToRadians(360f / bars_per_visualiser)))) / 2f, bar_length * audioData[i]);

                        if (heartShape)
                        {
                            // oily: the bar sits on the heart's edge and points straight out from it.
                            int index = i + j * (int)(bars_per_visualiser / visualiser_rounds);
                            Vector2 here = heartPoint(index);
                            // measured over a few neighbours so bars fan out evenly at the dip and the point instead of crossing.
                            Vector2 tangent = Vector2.Normalize(heartPoint(index + 6) - heartPoint(index - 6));

                            rotationCos = tangent.Y;
                            rotationSin = -tangent.X;
                            barPosition = here * size;
                            barSize.X = Vector2.Distance(heartPoint(index + 1), here) * size;
                        }
                        // The distance between the position and the sides of the bar.
                        var bottomOffset = new Vector2(-rotationSin * barSize.X / 2, rotationCos * barSize.X / 2);
                        // The distance between the bottom side of the bar and the top side.
                        var amplitudeOffset = new Vector2(rotationCos * barSize.Y, rotationSin * barSize.Y);

                        var rectangle = new Quad(
                            Vector2Extensions.Transform(barPosition - bottomOffset, DrawInfo.Matrix),
                            Vector2Extensions.Transform(barPosition - bottomOffset + amplitudeOffset, DrawInfo.Matrix),
                            Vector2Extensions.Transform(barPosition + bottomOffset, DrawInfo.Matrix),
                            Vector2Extensions.Transform(barPosition + bottomOffset + amplitudeOffset, DrawInfo.Matrix)
                        );

                        renderer.DrawQuad(
                            texture,
                            rectangle,
                            colourInfo,
                            null,
                            vertexBatch.AddAction,
                            // barSize by itself will make it smooth more in the X axis than in the Y axis, this reverts that.
                            Vector2.Divide(inflation, barSize.Yx));
                    }
                }

                shader.Unbind();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                vertexBatch?.Dispose();
            }
        }
    }
}
