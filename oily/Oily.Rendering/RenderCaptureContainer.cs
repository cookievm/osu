// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using RectangleF = osu.Framework.Graphics.Primitives.RectangleF;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Oily.Rendering
{
    /// <summary>
    /// Draws its children into an off-screen buffer at the video's size, whatever the window's size, and hands out frames from it.
    /// </summary>
    /// <remarks>
    /// The children are laid out for the window, and the buffer is drawn at a higher (or lower) resolution than the window,
    /// so the window must have the video's aspect ratio. A 1280x720 window gives a sharp 4K video.
    /// </remarks>
    public partial class RenderCaptureContainer : Container, IBufferedDrawable
    {
        /// <summary>
        /// Size of the captured frames, in pixels.
        /// </summary>
        public Vector2 TargetSize { get; }

        public IShader TextureShader { get; private set; } = null!;

        public Color4 BackgroundColour => Color4.Black;

        public DrawColourInfo? FrameBufferDrawColour => null;

        public Vector2 FrameBufferScale { get; private set; } = Vector2.One;

        private readonly BufferedDrawNodeSharedData sharedData = new BufferedDrawNodeSharedData(formats: new[] { RenderBufferFormat.D16 }, clipToRootNode: false);

        // Bumped every update so the buffer is redrawn every frame.
        private long drawVersion;

        private PendingCapture? pending;

        [Resolved]
        private GameHost host { get; set; } = null!;

        public RenderCaptureContainer(Vector2 targetSize)
        {
            TargetSize = targetSize;
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }

        /// <summary>
        /// Captures the first frame drawn after this update. Call it from the update thread once the frame's state is set.
        /// </summary>
        public Task<Image<Rgba32>> CaptureAsync()
        {
            pending?.Source.TrySetCanceled();
            pending = new PendingCapture();
            return pending.Source.Task;
        }

        protected override void Update()
        {
            base.Update();

            var screenSize = ScreenSpaceDrawQuad.AABBFloat.Size;
            if (screenSize.X > 0 && screenSize.Y > 0)
                FrameBufferScale = Vector2.Divide(TargetSize, screenSize);

            drawVersion++;
            Invalidate(Invalidation.DrawNode);

            // Once handed to a draw node, the request is the draw thread's to complete.
            if (pending?.Source.Task.IsCompleted == true)
                pending = null;
        }

        protected override RectangleF ComputeChildMaskingBounds() => ScreenSpaceDrawQuad.AABBFloat;

        protected override DrawNode CreateDrawNode() => new CaptureDrawNode(this, new CompositeDrawableDrawNode(this), sharedData);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            pending?.Source.TrySetCanceled();
            sharedData.Dispose();
        }

        private class PendingCapture
        {
            public readonly TaskCompletionSource<Image<Rgba32>> Source = new TaskCompletionSource<Image<Rgba32>>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Set on the draw thread once a draw node has drawn this frame.
            public bool Drawn;
        }

        private class CaptureDrawNode : BufferedDrawNode, ICompositeDrawNode
        {
            protected new RenderCaptureContainer Source => (RenderCaptureContainer)base.Source;

            protected new CompositeDrawableDrawNode Child => (CompositeDrawableDrawNode)base.Child;

            private long drawVersion;
            private PendingCapture? pending;

            public CaptureDrawNode(RenderCaptureContainer source, CompositeDrawableDrawNode child, BufferedDrawNodeSharedData sharedData)
                : base(source, child, sharedData)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                drawVersion = Source.drawVersion;
                pending = Source.pending;
            }

            protected override long GetDrawVersion() => drawVersion;

            protected override void DrawContents(IRenderer renderer)
            {
                base.DrawContents(renderer);

                var capture = pending;
                if (capture == null || capture.Drawn)
                    return;

                capture.Drawn = true;

                // The frame's commands are only submitted at the end of the frame, so read the buffer back at the start of the next one,
                // before anything draws over it.
                var buffer = SharedData.MainBuffer;
                Source.host.DrawThread.Scheduler.Add(() =>
                {
                    try
                    {
                        var image = renderer.ExtractFrameBufferData(buffer);

                        if (image == null)
                            capture.Source.TrySetException(new InvalidOperationException("The graphics renderer couldn't read the frame back."));
                        else
                            capture.Source.TrySetResult(image);
                    }
                    catch (Exception e)
                    {
                        capture.Source.TrySetException(e);
                    }
                });
            }

            public List<DrawNode>? Children
            {
                get => Child.Children;
                set => Child.Children = value;
            }

            public bool AddChildDrawNodes => RequiresRedraw;
        }
    }
}
