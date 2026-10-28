// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Oily.Rendering
{
    /// <summary>
    /// How a <see cref="RenderFrame"/>'s bytes are laid out.
    /// </summary>
    public enum RenderPixelFormat
    {
        /// <summary>
        /// 4 bytes per pixel, red first.
        /// </summary>
        Rgba,

        /// <summary>
        /// 4 bytes per pixel, blue first.
        /// </summary>
        Bgra,

        /// <summary>
        /// The video's own format: a full size brightness plane, then a half size plane of interleaved colour pairs.
        /// 1.5 bytes per pixel, BT.709 limited range. Encoders take it as is.
        /// </summary>
        Nv12,
    }

    /// <summary>
    /// The pixels of one captured frame, with no padding between rows.
    /// The pixel array is pooled, so dispose the frame once it's written.
    /// </summary>
    public sealed class RenderFrame : IDisposable
    {
        public int Width { get; }

        public int Height { get; }

        public RenderPixelFormat Format { get; }

        /// <summary>
        /// The pixels. Can be longer than <see cref="Length"/>.
        /// </summary>
        public byte[] Pixels { get; private set; }

        public int Length => LengthOf(Width, Height, Format);

        /// <summary>
        /// A frame with a pooled pixel array the caller fills.
        /// </summary>
        public RenderFrame(int width, int height, RenderPixelFormat format)
        {
            Width = width;
            Height = height;
            Format = format;
            Pixels = ArrayPool<byte>.Shared.Rent(LengthOf(width, height, format));
        }

        public static int LengthOf(int width, int height, RenderPixelFormat format)
            => format == RenderPixelFormat.Nv12 ? width * height * 3 / 2 : width * height * 4;

        public static RenderFrame FromImage(Image<Rgba32> image)
        {
            var frame = new RenderFrame(image.Width, image.Height, RenderPixelFormat.Rgba);
            image.CopyPixelDataTo(frame.Pixels.AsSpan(0, frame.Length));
            return frame;
        }

        public void Dispose()
        {
            if (Pixels.Length == 0)
                return;

            ArrayPool<byte>.Shared.Return(Pixels);
            Pixels = Array.Empty<byte>();
        }
    }

    /// <summary>
    /// A frame that has been drawn, whose pixels may still be on their way back from the graphics card.
    /// </summary>
    public sealed class FrameReadback
    {
        /// <summary>
        /// Completes once the pixels are in memory.
        /// </summary>
        public Task<RenderFrame> Pixels { get; }

        /// <summary>
        /// False for a frame that went into a motion blur blend instead of the video. Its <see cref="Pixels"/> never has any.
        /// </summary>
        public bool InVideo { get; }

        /// <summary>
        /// A frame that went into a motion blur blend. The blend's last frame brings the result.
        /// </summary>
        public static FrameReadback BlendedIn { get; } = new FrameReadback(Task.FromCanceled<RenderFrame>(new CancellationToken(true)), false);

        public FrameReadback(Task<RenderFrame> pixels)
            : this(pixels, true)
        {
        }

        private FrameReadback(Task<RenderFrame> pixels, bool inVideo)
        {
            Pixels = pixels;
            InVideo = inVideo;
        }

        /// <summary>
        /// A readback of an image already in memory. The image is disposed.
        /// </summary>
        public static FrameReadback FromImage(Image<Rgba32> image)
        {
            using (image)
                return new FrameReadback(Task.FromResult(RenderFrame.FromImage(image)));
        }
    }
}
