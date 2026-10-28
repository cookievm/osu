// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Oily.Rendering.Tests
{
    [TestFixture]
    public class Nv12ConverterTest
    {
        // BT.709 limited range values for pure colours, as ffmpeg computes them.
        [TestCase(255, 255, 255, 235, 128, 128)]
        [TestCase(0, 0, 0, 16, 128, 128)]
        [TestCase(255, 0, 0, 63, 102, 240)]
        [TestCase(0, 255, 0, 173, 42, 26)]
        [TestCase(0, 0, 255, 32, 240, 118)]
        public void TestPureColours(int r, int g, int b, int y, int u, int v)
        {
            byte[] nv12 = convert(4, 2, (_, _) => (r, g, b), bgra: false);

            Assert.Multiple(() =>
            {
                for (int i = 0; i < 8; i++)
                    Assert.That(nv12[i], Is.EqualTo(y).Within(1), "Y");

                for (int i = 8; i < 12; i += 2)
                {
                    Assert.That(nv12[i], Is.EqualTo(u).Within(1), "U");
                    Assert.That(nv12[i + 1], Is.EqualTo(v).Within(1), "V");
                }
            });
        }

        [Test]
        public void TestBgraAndRowPaddingAndLayout()
        {
            // Left half red, right half blue; top rows and bottom rows differ in brightness only through colour.
            byte[] nv12 = convert(4, 4, (x, _) => x < 2 ? (255, 0, 0) : (0, 0, 255), bgra: true, padding: 12);

            Assert.That(nv12.Length, Is.EqualTo(24));

            // Brightness plane, row by row.
            for (int row = 0; row < 4; row++)
            {
                Assert.That(nv12[row * 4 + 0], Is.EqualTo(63).Within(1));
                Assert.That(nv12[row * 4 + 1], Is.EqualTo(63).Within(1));
                Assert.That(nv12[row * 4 + 2], Is.EqualTo(32).Within(1));
                Assert.That(nv12[row * 4 + 3], Is.EqualTo(32).Within(1));
            }

            // Colour plane: two rows of (U, V) for the left block then the right block.
            for (int row = 0; row < 2; row++)
            {
                int start = 16 + row * 4;
                Assert.That(new[] { nv12[start], nv12[start + 1], nv12[start + 2], nv12[start + 3] },
                    Is.EqualTo(new[] { 102, 240, 240, 118 }).Within(1));
            }
        }

        [Test]
        public void TestRejectsOddSizes()
        {
            Assert.That(Nv12Converter.CanConvert(3840, 2160), Is.True);
            Assert.That(Nv12Converter.CanConvert(1279, 720), Is.False);
            Assert.Throws<ArgumentException>(() => convert(3, 2, (_, _) => (0, 0, 0), bgra: false));
        }

        [Test]
        public void TestConverts4KQuickly()
        {
            const int width = 3840;
            const int height = 2160;

            IntPtr source = Marshal.AllocHGlobal(width * height * 4);

            try
            {
                byte[] destination = new byte[width * height * 3 / 2];

                // Warm up, then time a few frames.
                Nv12Converter.Convert(source, width * 4, width, height, false, destination);

                var stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < 5; i++)
                    Nv12Converter.Convert(source, width * 4, width, height, false, destination);

                TestContext.Out.WriteLine($"4K frame converted in {stopwatch.Elapsed.TotalMilliseconds / 5:0.0} ms on {Environment.ProcessorCount} cores");
            }
            finally
            {
                Marshal.FreeHGlobal(source);
            }
        }

        private static byte[] convert(int width, int height, Func<int, int, (int r, int g, int b)> colour, bool bgra, int padding = 0)
        {
            int pitch = width * 4 + padding;
            byte[] pixels = new byte[pitch * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var (r, g, b) = colour(x, y);
                    int i = y * pitch + x * 4;
                    pixels[i + (bgra ? 2 : 0)] = (byte)r;
                    pixels[i + 1] = (byte)g;
                    pixels[i + (bgra ? 0 : 2)] = (byte)b;
                    pixels[i + 3] = 255;
                }
            }

            byte[] result = new byte[RenderFrame.LengthOf(width, height, RenderPixelFormat.Nv12)];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);

            try
            {
                Nv12Converter.Convert(handle.AddrOfPinnedObject(), pitch, width, height, bgra, result);
            }
            finally
            {
                handle.Free();
            }

            return result;
        }
    }
}
