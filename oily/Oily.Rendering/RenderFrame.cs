// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Oily.Rendering
{
    /// <summary>
    /// The pixels of one captured frame, 4 bytes per pixel with no padding between rows.
    /// The pixel array is pooled, so dispose the frame once it's written.
    /// </summary>
    public sealed class RenderFrame : IDisposable
    {
        public int Width { get; }

        public int Height { get; }

        /// <summary>
        /// Whether the bytes are in BGRA order rather than RGBA.
        /// </summary>
        public bool Bgra { get; }

        /// <summary>
        /// The pixels. Can be longer than <see cref="Length"/>.
        /// </summary>
        public byte[] Pixels { get; private set; }

        public int Length => Width * Height * 4;

        /// <summary>
        /// A frame with a pooled pixel array the caller fills.
        /// </summary>
        public RenderFrame(int width, int height, bool bgra)
        {
            Width = width;
            Height = height;
            Bgra = bgra;
            Pixels = ArrayPool<byte>.Shared.Rent(width * height * 4);
        }

        public static RenderFrame FromImage(Image<Rgba32> image)
        {
            var frame = new RenderFrame(image.Width, image.Height, false);
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

        public FrameReadback(Task<RenderFrame> pixels)
        {
            Pixels = pixels;
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
