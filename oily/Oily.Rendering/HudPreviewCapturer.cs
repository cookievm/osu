// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Testing;
using osu.Game.Skinning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Oily.Rendering
{
    /// <summary>
    /// For the companion app's HUD editor: instead of a video, saves a picture of each HUD piece (score, combo, pp counter...) as the skin draws it,
    /// plus where each one is, in <c>hud.json</c>. Only the last frame is used; gameplay is hidden for it so the pieces are drawn over black.
    /// </summary>
    public class HudPreviewCapturer : IFrameCapturer
    {
        public const string INDEX_FILE = "hud.json";

        private readonly ReplayRenderScreen screen;
        private readonly string folder;
        private readonly int frameCount;

        public string OutputDescription => folder;

        public HudPreviewCapturer(ReplayRenderScreen screen, string folder, int frameCount)
        {
            this.screen = screen;
            this.folder = folder;
            this.frameCount = frameCount;
        }

        public async Task CaptureFrameAsync(RenderedFrame frame)
        {
            // This part runs on the update thread, before the first await.
            if (frame.Index == frameCount - 3)
                screen.Player.HideGameplay();

            if (frame.Index < frameCount - 1)
            {
                // Nothing to keep, but the frame still goes back to the pool.
                await discard().ConfigureAwait(false);
                return;
            }

            var hud = Measure(screen.Player.Hud);

            var readback = await screen.GrabFrameAsync().ConfigureAwait(false);
            using var pixels = await readback.Pixels.ConfigureAwait(false);

            Write(hud, pixels, folder);
        }

        private async Task discard()
        {
            try
            {
                var readback = await screen.GrabFrameAsync().ConfigureAwait(false);
                (await readback.Pixels.ConfigureAwait(false)).Dispose();
            }
            catch (OperationCanceledException)
            {
            }
        }

        public Task CompleteAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }

        /// <summary>
        /// Where every HUD piece is, in the HUD's own units (1366 x 768 on a 16:9 screen), in the order the skin's layout lists them.
        /// </summary>
        public static HudPreview Measure(Drawable hud)
        {
            var pieces = new List<HudPreviewPiece>();
            Vector2Size size = default;

            foreach (var container in hud.ChildrenOfType<SkinnableContainer>().Where(c => c.Lookup.Lookup == GlobalSkinnableContainers.MainHUDComponents))
            {
                string group = container.Lookup.Ruleset?.ShortName ?? "global";
                size = new Vector2Size(container.DrawWidth, container.DrawHeight);
                int index = 0;

                foreach (var component in container.Components)
                {
                    if (component is not Drawable drawable)
                        continue;

                    var bounds = container.ToLocalSpace(drawable.ScreenSpaceDrawQuad).AABBFloat;
                    var origin = container.ToLocalSpace(drawable.ToScreenSpace(drawable.OriginPosition));

                    pieces.Add(new HudPreviewPiece
                    {
                        Group = group,
                        Index = index++,
                        Type = drawable.GetType().Name,
                        Visible = drawable.IsPresent && bounds.Width >= 1 && bounds.Height >= 1,
                        Left = bounds.Left,
                        Top = bounds.Top,
                        Width = bounds.Width,
                        Height = bounds.Height,
                        OriginX = origin.X,
                        OriginY = origin.Y,
                    });
                }
            }

            return new HudPreview { Width = size.Width, Height = size.Height, Pieces = pieces };
        }

        /// <summary>
        /// Saves a picture of each visible piece, cut from <paramref name="frame"/>, and the index file.
        /// </summary>
        public static void Write(HudPreview hud, RenderFrame frame, string folder)
        {
            Directory.CreateDirectory(folder);

            if (hud.Width > 0 && hud.Height > 0)
            {
                double scaleX = frame.Width / hud.Width, scaleY = frame.Height / hud.Height;

                foreach (var piece in hud.Pieces.Where(p => p.Visible))
                {
                    var rect = new RectangleI(
                        (int)Math.Floor(piece.Left * scaleX), (int)Math.Floor(piece.Top * scaleY),
                        (int)Math.Ceiling(piece.Width * scaleX) + 1, (int)Math.Ceiling(piece.Height * scaleY) + 1);

                    using var image = Crop(frame, rect);
                    if (image == null)
                        continue;

                    piece.File = $"{piece.Group}-{piece.Index}.png";
                    image.SaveAsPng(Path.Combine(folder, piece.File));
                }
            }

            File.WriteAllText(Path.Combine(folder, INDEX_FILE), JsonSerializer.Serialize(hud, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }

        /// <summary>
        /// Cuts a rectangle out of the frame. The frame is drawn over black, so black becomes see-through:
        /// each pixel's brightest channel is its opacity, and its colour is divided back out of the black.
        /// </summary>
        public static Image<Rgba32>? Crop(RenderFrame frame, RectangleI rect)
        {
            int left = Math.Clamp(rect.Left, 0, frame.Width), top = Math.Clamp(rect.Top, 0, frame.Height);
            int right = Math.Clamp(rect.Right, 0, frame.Width), bottom = Math.Clamp(rect.Bottom, 0, frame.Height);

            if (right <= left || bottom <= top)
                return null;

            var image = new Image<Rgba32>(right - left, bottom - top);

            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    var (r, g, b) = rgbAt(frame, x, y);
                    byte a = Math.Max(r, Math.Max(g, b));

                    image[x - left, y - top] = a == 0
                        ? new Rgba32(0, 0, 0, 0)
                        : new Rgba32((byte)(r * 255 / a), (byte)(g * 255 / a), (byte)(b * 255 / a), a);
                }
            }

            return image;
        }

        private static (byte R, byte G, byte B) rgbAt(RenderFrame frame, int x, int y)
        {
            switch (frame.Format)
            {
                case RenderPixelFormat.Rgba:
                {
                    int i = (y * frame.Width + x) * 4;
                    return (frame.Pixels[i], frame.Pixels[i + 1], frame.Pixels[i + 2]);
                }

                case RenderPixelFormat.Bgra:
                {
                    int i = (y * frame.Width + x) * 4;
                    return (frame.Pixels[i + 2], frame.Pixels[i + 1], frame.Pixels[i]);
                }

                default:
                {
                    // NV12, BT.709 limited range, as Nv12Converter writes it.
                    double luma = (frame.Pixels[y * frame.Width + x] - 16) * 1.164383;
                    int uv = frame.Width * frame.Height + y / 2 * frame.Width + x / 2 * 2;
                    double u = frame.Pixels[uv] - 128, v = frame.Pixels[uv + 1] - 128;

                    return (toByte(luma + 1.792741 * v), toByte(luma - 0.213249 * u - 0.532909 * v), toByte(luma + 2.112402 * u));
                }
            }

            static byte toByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
        }

        private readonly record struct Vector2Size(float Width, float Height);
    }

    /// <summary>What <see cref="HudPreviewCapturer"/> writes to <c>hud.json</c>.</summary>
    public class HudPreview
    {
        /// <summary>Size of the HUD in its own units.</summary>
        public float Width { get; set; }

        public float Height { get; set; }

        public List<HudPreviewPiece> Pieces { get; set; } = new List<HudPreviewPiece>();
    }

    public class HudPreviewPiece
    {
        /// <summary>"global", or the ruleset ("osu") for pieces in its own part of the layout.</summary>
        public string Group { get; set; } = string.Empty;

        /// <summary>Position in its group's list in the skin's layout file.</summary>
        public int Index { get; set; }

        /// <summary>Class name, like "LegacyScoreCounter".</summary>
        public string Type { get; set; } = string.Empty;

        public bool Visible { get; set; }

        /// <summary>The picture of this piece, in the same folder; null when it wasn't drawn.</summary>
        public string? File { get; set; }

        // Bounds and origin in HUD units.
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float OriginX { get; set; }
        public float OriginY { get; set; }
    }
}
