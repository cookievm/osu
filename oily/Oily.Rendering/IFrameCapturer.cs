// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;

namespace Oily.Rendering
{
    /// <summary>
    /// Receives each rendered frame. The render loop waits for <see cref="CaptureFrameAsync"/> before stepping to the next frame.
    /// </summary>
    public interface IFrameCapturer : IDisposable
    {
        /// <summary>
        /// Where the output ends up, shown to the user when the render finishes.
        /// </summary>
        string OutputDescription { get; }

        /// <summary>
        /// Captures what is currently on screen as frame <paramref name="frame"/>.
        /// </summary>
        Task CaptureFrameAsync(RenderedFrame frame);

        /// <summary>
        /// Called once after the last frame.
        /// </summary>
        void Complete();
    }

    /// <summary>
    /// Timing of one rendered frame.
    /// </summary>
    /// <param name="Index">The frame number, starting at 0.</param>
    /// <param name="VideoTime">Time in the output video, in milliseconds.</param>
    /// <param name="GameplayTime">Gameplay time shown in the frame, in milliseconds.</param>
    public readonly record struct RenderedFrame(int Index, double VideoTime, double GameplayTime);
}
