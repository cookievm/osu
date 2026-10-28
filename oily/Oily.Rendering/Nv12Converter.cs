// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;

namespace Oily.Rendering
{
    /// <summary>
    /// Converts 8-bit RGBA or BGRA pixels to NV12 (BT.709, limited range), the format video encoders work in.
    /// </summary>
    /// <remarks>
    /// Doing this here, on every core, means ffmpeg receives 1.5 bytes per pixel instead of 4 and doesn't convert anything itself.
    /// At 4K, ffmpeg's single-threaded conversion and the 33 MB per frame through its pipe were what limited render speed.
    /// </remarks>
    public static class Nv12Converter
    {
        /// <summary>
        /// Whether a frame of this size can be converted. NV12 needs even dimensions.
        /// </summary>
        public static bool CanConvert(int width, int height) => width > 0 && height > 0 && width % 2 == 0 && height % 2 == 0;

        /// <summary>
        /// Converts <paramref name="height"/> rows of <paramref name="width"/> pixels, <paramref name="rowPitch"/> bytes apart, starting at <paramref name="source"/>.
        /// </summary>
        /// <param name="source">The first pixel. Must stay readable until this returns.</param>
        /// <param name="rowPitch">Bytes from the start of one row to the next. At least <paramref name="width"/> × 4.</param>
        /// <param name="width">Width in pixels. Even.</param>
        /// <param name="height">Height in pixels. Even.</param>
        /// <param name="bgra">Whether the source is BGRA rather than RGBA.</param>
        /// <param name="destination">At least <paramref name="width"/> × <paramref name="height"/> × 1.5 bytes.</param>
        public static unsafe void Convert(IntPtr source, int rowPitch, int width, int height, bool bgra, byte[] destination)
        {
            if (!CanConvert(width, height))
                throw new ArgumentException($"NV12 needs an even width and height, not {width}x{height}.");

            ArgumentOutOfRangeException.ThrowIfLessThan(rowPitch, width * 4);
            ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, RenderFrame.LengthOf(width, height, RenderPixelFormat.Nv12));

            byte* src = (byte*)source;
            int redOffset = bgra ? 2 : 0;
            int blueOffset = bgra ? 0 : 2;

            fixed (byte* dst = destination)
            {
                byte* dstPtr = dst;

                // One iteration per pair of rows: two rows of brightness and one row of colour.
                Parallel.For(0, height / 2, pair =>
                {
                    byte* top = src + (long)pair * 2 * rowPitch;
                    byte* bottom = top + rowPitch;
                    byte* yTop = dstPtr + (long)pair * 2 * width;
                    byte* yBottom = yTop + width;
                    byte* uv = dstPtr + (long)width * height + (long)pair * width;

                    for (int x = 0; x < width; x += 2)
                    {
                        byte* a = top + x * 4;
                        byte* b = a + 4;
                        byte* c = bottom + x * 4;
                        byte* d = c + 4;

                        int r0 = a[redOffset], g0 = a[1], b0 = a[blueOffset];
                        int r1 = b[redOffset], g1 = b[1], b1 = b[blueOffset];
                        int r2 = c[redOffset], g2 = c[1], b2 = c[blueOffset];
                        int r3 = d[redOffset], g3 = d[1], b3 = d[blueOffset];

                        yTop[x] = luma(r0, g0, b0);
                        yTop[x + 1] = luma(r1, g1, b1);
                        yBottom[x] = luma(r2, g2, b2);
                        yBottom[x + 1] = luma(r3, g3, b3);

                        // Colour is shared by each 2x2 block, so it's taken from the block's average.
                        int r = r0 + r1 + r2 + r3;
                        int g = g0 + g1 + g2 + g3;
                        int bl = b0 + b1 + b2 + b3;

                        uv[x] = (byte)(((-26 * r - 86 * g + 112 * bl + 512) >> 10) + 128);
                        uv[x + 1] = (byte)(((112 * r - 102 * g - 10 * bl + 512) >> 10) + 128);
                    }
                });
            }
        }

        // BT.709 limited range, scaled by 256: Y = 16 + 219 × (0.2126 R + 0.7152 G + 0.0722 B) / 255.
        private static byte luma(int r, int g, int b) => (byte)(((47 * r + 157 * g + 16 * b + 128) >> 8) + 16);
    }
}
