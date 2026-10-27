// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Platform;
using SixLabors.ImageSharp;

namespace Oily.Rendering
{
    /// <summary>
    /// Saves every frame as a numbered PNG in a new folder under "renders" in the o!ily data folder.
    /// </summary>
    /// <remarks>
    /// Phase 0 only: this reads back the whole window through <see cref="GameHost.TakeScreenshotAsync"/>, which stalls the GPU each frame.
    /// Output resolution is the window's resolution.
    /// </remarks>
    public class PngSequenceCapturer : IFrameCapturer
    {
        private readonly GameHost host;
        private readonly Storage storage;

        public string OutputDescription { get; }

        public PngSequenceCapturer(GameHost host, Storage rendersStorage)
        {
            this.host = host;

            string folderName = DateTime.Now.ToString(@"yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            storage = rendersStorage.GetStorageForDirectory(folderName);
            OutputDescription = storage.GetFullPath(string.Empty);
        }

        public async Task CaptureFrameAsync(RenderedFrame frame)
        {
            using (var image = await host.TakeScreenshotAsync().ConfigureAwait(false))
            using (var stream = storage.CreateFileSafely($@"frame_{frame.Index:D6}.png"))
                await image.SaveAsPngAsync(stream).ConfigureAwait(false);
        }

        public void Complete()
        {
            // Lets anyone looking at the folder know the frame rate needed to turn it into a video.
            using (var writer = new StreamWriter(storage.CreateFileSafely(@"README.txt")))
                writer.WriteLine(@"Frames rendered by o!ily. Turn them into a video with: ffmpeg -framerate <fps> -i frame_%06d.png -pix_fmt yuv420p out.mp4");
        }

        public void Open() => storage.PresentExternally();

        public void Dispose()
        {
        }
    }
}
