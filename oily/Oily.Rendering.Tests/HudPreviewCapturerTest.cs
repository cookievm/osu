// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using osu.Framework.Graphics.Primitives;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Oily.Rendering.Tests
{
    [TestFixture]
    public class HudPreviewCapturerTest
    {
        private static RenderFrame rgbaFrame(int width, int height, Func<int, int, (byte R, byte G, byte B)> colour)
        {
            var frame = new RenderFrame(width, height, RenderPixelFormat.Rgba);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var (r, g, b) = colour(x, y);
                    int i = (y * width + x) * 4;
                    frame.Pixels[i] = r;
                    frame.Pixels[i + 1] = g;
                    frame.Pixels[i + 2] = b;
                    frame.Pixels[i + 3] = 255;
                }
            }

            return frame;
        }

        [Test]
        public void TestBlackBecomesSeeThrough()
        {
            using var frame = rgbaFrame(4, 4, (x, _) => x < 2 ? ((byte)0, (byte)0, (byte)0) : ((byte)255, (byte)128, (byte)0));
            using var image = HudPreviewCapturer.Crop(frame, new RectangleI(0, 0, 4, 4))!;

            Assert.That(image[0, 0].A, Is.Zero);
            Assert.That(image[3, 3], Is.EqualTo(new Rgba32(255, 128, 0, 255)));
        }

        [Test]
        public void TestDimColourKeepsItsHue()
        {
            using var frame = rgbaFrame(1, 1, (_, _) => (100, 50, 0));
            using var image = HudPreviewCapturer.Crop(frame, new RectangleI(0, 0, 1, 1))!;

            // Half-bright orange over black is orange at about 40% opacity.
            Assert.That(image[0, 0], Is.EqualTo(new Rgba32(255, 127, 0, 100)));
        }

        [Test]
        public void TestReadsNv12()
        {
            // Limited range: Y 16 is black, Y 235 is white, chroma 128 is grey.
            using var frame = new RenderFrame(2, 2, RenderPixelFormat.Nv12);
            frame.Pixels[0] = 16;
            frame.Pixels[1] = 235;
            frame.Pixels[2] = 16;
            frame.Pixels[3] = 235;
            frame.Pixels[4] = 128;
            frame.Pixels[5] = 128;

            using var image = HudPreviewCapturer.Crop(frame, new RectangleI(0, 0, 2, 2))!;

            Assert.That(image[0, 0].A, Is.Zero);
            Assert.That(image[1, 0], Is.EqualTo(new Rgba32(255, 255, 255, 255)));
        }

        [Test]
        public void TestCropOutsideFrameIsNull()
        {
            using var frame = rgbaFrame(4, 4, (_, _) => (255, 255, 255));

            Assert.That(HudPreviewCapturer.Crop(frame, new RectangleI(10, 10, 5, 5)), Is.Null);
            Assert.That(HudPreviewCapturer.Crop(frame, new RectangleI(2, 2, 10, 10))!.Width, Is.EqualTo(2));
        }

        [Test]
        public void TestWritesPicturesAndIndex()
        {
            string folder = Path.Combine(Path.GetTempPath(), "oily-hud-test-" + Guid.NewGuid().ToString("N"));

            try
            {
                // The HUD is half the frame's size, so a 10 x 10 piece is 20 x 20 pixels.
                using var frame = rgbaFrame(200, 100, (x, y) => x >= 20 && x < 40 && y >= 20 && y < 40 ? ((byte)255, (byte)255, (byte)255) : ((byte)0, (byte)0, (byte)0));
                var hud = new HudPreview
                {
                    Width = 100,
                    Height = 50,
                    Pieces =
                    {
                        new HudPreviewPiece { Group = "global", Index = 0, Type = "LegacyScoreCounter", Visible = true, Left = 10, Top = 10, Width = 10, Height = 10, OriginX = 10, OriginY = 10 },
                        new HudPreviewPiece { Group = "osu", Index = 0, Type = "SpectatorList", Visible = false },
                    },
                };

                HudPreviewCapturer.Write(hud, frame, folder);

                using (var image = Image.Load<Rgba32>(Path.Combine(folder, "global-0.png")))
                {
                    Assert.That(image.Width, Is.EqualTo(21));
                    Assert.That(image[5, 5].A, Is.EqualTo(255));
                }

                Assert.That(File.Exists(Path.Combine(folder, "osu-0.png")), Is.False);

                using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, HudPreviewCapturer.INDEX_FILE)));
                var first = index.RootElement.GetProperty("pieces")[0];
                Assert.That(first.GetProperty("file").GetString(), Is.EqualTo("global-0.png"));
                Assert.That(first.GetProperty("type").GetString(), Is.EqualTo("LegacyScoreCounter"));
                Assert.That(index.RootElement.GetProperty("width").GetSingle(), Is.EqualTo(100));
                Assert.That(index.RootElement.GetProperty("pieces")[1].GetProperty("file").ValueKind, Is.EqualTo(JsonValueKind.Null));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
    }
}
