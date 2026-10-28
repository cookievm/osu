// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Rendering;
using Veldrid;
using Texture = Veldrid.Texture;

namespace Oily.Rendering
{
    /// <summary>
    /// Reads frames back from the graphics card without waiting for them.
    /// </summary>
    /// <remarks>
    /// The framework's own readback (<see cref="IRenderer.ExtractFrameBufferData"/>) creates a staging texture per frame,
    /// waits for the graphics card to finish, and copies the pixels several times. At 4K that is most of a render's time.
    /// This copies each frame into one of a few reused staging textures and maps it a frame or two later, once the copy is done,
    /// so the game keeps drawing while earlier frames come back.
    /// Pixels are converted on other threads while the staging texture stays mapped, so the draw thread only maps and unmaps.
    /// Everything here must be called on the draw thread.
    /// </remarks>
    internal sealed class VeldridFrameReader : IDisposable
    {
        // One more than before conversion moved off the draw thread, as a slot stays mapped while its pixels are converted.
        private const int slot_count = 4;

        /// <summary>
        /// Draw frames to wait before mapping a copy, so the graphics card has finished it and mapping doesn't stall.
        /// </summary>
        private const int frames_before_map = 2;

        /// <summary>
        /// Leaves cores free for the game's update and draw threads, which the conversion would otherwise slow down.
        /// </summary>
        private static readonly int conversion_threads = Math.Max(1, Environment.ProcessorCount - 2);

        private static readonly PropertyInfo? native_texture_property =
            typeof(osu.Framework.Graphics.Textures.Texture).GetProperty("NativeTexture", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private readonly GraphicsDevice device;
        private readonly ResourceFactory factory;

        private readonly Stack<Slot> freeSlots = new Stack<Slot>();
        private readonly Queue<Slot> busySlots = new Queue<Slot>();
        private readonly Queue<Slot> convertingSlots = new Queue<Slot>();
        private readonly List<Slot> allSlots = new List<Slot>();

        private long pollCount;

        /// <summary>
        /// Whether to hand out frames as NV12 (see <see cref="Nv12Converter"/>) rather than as they are on the graphics card.
        /// </summary>
        public bool ConvertToNv12 { get; set; } = true;

        private VeldridFrameReader(GraphicsDevice device, ResourceFactory factory)
        {
            this.device = device;
            this.factory = factory;
        }

        /// <summary>
        /// A reader for <paramref name="renderer"/>, or null when it isn't a Veldrid renderer.
        /// </summary>
        public static VeldridFrameReader? Create(IRenderer renderer)
        {
            var type = renderer.GetType();

            if (type.GetProperty("Device")?.GetValue(renderer) is GraphicsDevice device
                && type.GetProperty("Factory")?.GetValue(renderer) is ResourceFactory factory)
                return new VeldridFrameReader(device, factory);

            return null;
        }

        /// <summary>
        /// The Veldrid texture behind <paramref name="frameBuffer"/>, when it is one this reader can read.
        /// </summary>
        public static Texture? GetTexture(IFrameBuffer frameBuffer)
        {
            object? native = native_texture_property?.GetValue(frameBuffer.Texture);

            if (native?.GetType().GetMethod("GetResourceList")?.Invoke(native, null) is not IEnumerable resources)
                return null;

            object? first = resources.Cast<object>().FirstOrDefault();

            if (first?.GetType().GetField("Texture")?.GetValue(first) is not Texture texture)
                return null;

            return isSupported(texture.Format) ? texture : null;
        }

        /// <summary>
        /// Starts copying <paramref name="source"/> back. The task completes during a later <see cref="Poll"/>.
        /// Call it at the start of a draw frame, after the frame that drew <paramref name="source"/> has been submitted.
        /// </summary>
        public Task<RenderFrame> Read(Texture source)
        {
            collectFinished();

            // All staging textures are busy: wait for the oldest, which also keeps the queue from growing without end.
            if (freeSlots.Count == 0 && allSlots.Count >= slot_count)
            {
                long stallStart = Stopwatch.GetTimestamp();

                if (convertingSlots.Count == 0)
                    startConverting(busySlots.Dequeue());

                var oldest = convertingSlots.Dequeue();
                oldest.Conversion!.WaitSafely();
                release(oldest);

                RenderTimings.ReadbackStall.AddSince(stallStart);
            }

            var slot = freeSlots.Count > 0 ? freeSlots.Pop() : createSlot();
            slot.EnsureMatches(factory, source);

            slot.Target = new TaskCompletionSource<RenderFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            slot.SubmittedAt = pollCount;

            slot.Commands.Begin();
            slot.Commands.CopyTexture(source, slot.Staging!);
            slot.Commands.End();

            device.ResetFence(slot.Fence);
            device.SubmitCommands(slot.Commands, slot.Fence);

            busySlots.Enqueue(slot);
            return slot.Target.Task;
        }

        /// <summary>
        /// Hands out every copy that's done. Call it once per draw frame while <see cref="Busy"/>.
        /// </summary>
        public void Poll()
        {
            pollCount++;
            collectFinished();
        }

        private void collectFinished()
        {
            while (convertingSlots.TryPeek(out var converting) && converting.Conversion!.IsCompleted)
                release(convertingSlots.Dequeue());

            while (busySlots.TryPeek(out var slot) && slot.Fence.Signaled && pollCount - slot.SubmittedAt >= frames_before_map)
                startConverting(busySlots.Dequeue());
        }

        /// <summary>
        /// Whether copies are still on their way back or being converted.
        /// </summary>
        public bool Busy => busySlots.Count > 0 || convertingSlots.Count > 0;

        private Slot createSlot()
        {
            var slot = new Slot(factory.CreateCommandList(), factory.CreateFence(false));
            allSlots.Add(slot);
            return slot;
        }

        /// <summary>
        /// Maps <paramref name="slot"/>'s staging texture and converts its pixels on other threads.
        /// The texture stays mapped until <see cref="release"/>, back on the draw thread.
        /// </summary>
        private void startConverting(Slot slot)
        {
            var target = slot.Target!;
            slot.Target = null;

            long start = Stopwatch.GetTimestamp();

            try
            {
                if (!device.WaitForFence(slot.Fence, 5_000_000_000))
                    throw new TimeoutException("The graphics card didn't copy the frame back within 5 seconds.");

                var staging = slot.Staging!;
                int width = (int)staging.Width;
                int height = (int)staging.Height;
                bool bgra = isBgra(staging.Format);
                bool nv12 = ConvertToNv12 && Nv12Converter.CanConvert(width, height);

                var mapped = device.Map(staging, MapMode.Read, 0);
                slot.Mapped = true;

                IntPtr data = mapped.Data;
                int rowPitch = (int)mapped.RowPitch;

                slot.Conversion = Task.Run(() =>
                {
                    long convertStart = Stopwatch.GetTimestamp();
                    var frame = new RenderFrame(width, height, nv12 ? RenderPixelFormat.Nv12 : bgra ? RenderPixelFormat.Bgra : RenderPixelFormat.Rgba);

                    try
                    {
                        if (nv12)
                            Nv12Converter.Convert(data, rowPitch, width, height, bgra, frame.Pixels, conversion_threads);
                        else
                        {
                            int rowBytes = width * 4;

                            for (int y = 0; y < height; y++)
                                Marshal.Copy(data + y * (nint)rowPitch, frame.Pixels, y * rowBytes, rowBytes);
                        }
                    }
                    catch (Exception e)
                    {
                        frame.Dispose();
                        target.TrySetException(e);
                        return;
                    }

                    RenderTimings.Convert.AddSince(convertStart);
                    target.TrySetResult(frame);
                });

                convertingSlots.Enqueue(slot);
            }
            catch (Exception e)
            {
                target.TrySetException(e);
                release(slot);
            }
            finally
            {
                RenderTimings.Readback.AddSince(start);
            }
        }

        /// <summary>
        /// Unmaps <paramref name="slot"/> once its pixels are converted, so it can take another frame.
        /// </summary>
        private void release(Slot slot)
        {
            slot.Conversion = null;

            if (slot.Mapped)
            {
                device.Unmap(slot.Staging!, 0);
                slot.Mapped = false;
            }

            freeSlots.Push(slot);
        }

        private static bool isSupported(Veldrid.PixelFormat format)
            => format is Veldrid.PixelFormat.R8G8B8A8UNorm or Veldrid.PixelFormat.R8G8B8A8UNormSRgb
                or Veldrid.PixelFormat.B8G8R8A8UNorm or Veldrid.PixelFormat.B8G8R8A8UNormSRgb;

        private static bool isBgra(Veldrid.PixelFormat format)
            => format is Veldrid.PixelFormat.B8G8R8A8UNorm or Veldrid.PixelFormat.B8G8R8A8UNormSRgb;

        public void Dispose()
        {
            // The conversions read mapped memory, so they finish before anything is unmapped.
            foreach (var slot in convertingSlots)
                slot.Conversion!.WaitSafely();

            foreach (var slot in allSlots)
            {
                if (slot.Mapped)
                    device.Unmap(slot.Staging!, 0);

                slot.Target?.TrySetCanceled();
                slot.Dispose();
            }

            allSlots.Clear();
            freeSlots.Clear();
            busySlots.Clear();
            convertingSlots.Clear();
        }

        private sealed class Slot : IDisposable
        {
            public readonly CommandList Commands;
            public readonly Fence Fence;

            public Texture? Staging;
            public TaskCompletionSource<RenderFrame>? Target;
            public long SubmittedAt;
            public Task? Conversion;
            public bool Mapped;

            public Slot(CommandList commands, Fence fence)
            {
                Commands = commands;
                Fence = fence;
            }

            public void EnsureMatches(ResourceFactory factory, Texture source)
            {
                if (Staging != null && Staging.Width == source.Width && Staging.Height == source.Height && Staging.Format == source.Format
                    && Staging.MipLevels == source.MipLevels && Staging.ArrayLayers == source.ArrayLayers)
                    return;

                Staging?.Dispose();
                Staging = factory.CreateTexture(TextureDescription.Texture2D(source.Width, source.Height, source.MipLevels, source.ArrayLayers, source.Format, TextureUsage.Staging));
            }

            public void Dispose()
            {
                Staging?.Dispose();
                Commands.Dispose();
                Fence.Dispose();
            }
        }
    }
}
