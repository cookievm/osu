// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.IO;

namespace Oily.Rendering
{
    /// <summary>
    /// Tells whoever started a <see cref="RenderJob"/> how it is going, one line at a time on standard output:
    /// <code>
    /// OILY-RENDER STATUS Importing the beatmap
    /// OILY-RENDER PROGRESS 42
    /// OILY-RENDER DONE C:\Videos\play.mp4
    /// OILY-RENDER ERROR ffmpeg failed with code 1
    /// </code>
    /// </summary>
    public class RenderJobReporter
    {
        public const string PREFIX = "OILY-RENDER";

        private readonly TextWriter output;
        private readonly object writeLock = new object();

        public RenderJobReporter(TextWriter output)
        {
            this.output = output;
        }

        public void Status(string message) => write($"STATUS {oneLine(message)}");

        public void Progress(double progress) =>
            write($"PROGRESS {((int)Math.Round(Math.Clamp(progress, 0, 1) * 100)).ToString(CultureInfo.InvariantCulture)}");

        public void Done(string outputPath) => write($"DONE {oneLine(outputPath)}");

        public void Error(string message) => write($"ERROR {oneLine(message)}");

        private void write(string line)
        {
            lock (writeLock)
            {
                output.WriteLine($"{PREFIX} {line}");
                output.Flush();
            }
        }

        private static string oneLine(string text) => text.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
