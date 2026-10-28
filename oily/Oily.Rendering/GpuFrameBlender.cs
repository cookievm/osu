// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;
using Veldrid;
using Veldrid.SPIRV;
using Texture = Veldrid.Texture;

namespace Oily.Rendering
{
    /// <summary>
    /// Motion blur: averages several drawn frames into one on the graphics card.
    /// </summary>
    /// <remarks>
    /// Each frame's pixels are added to running sums, and the last one of a group writes the average to <see cref="Output"/>,
    /// which is then read back like any other frame. Nothing comes back to the CPU until then.
    /// Everything here must be called on the draw thread.
    /// </remarks>
    public sealed class GpuFrameBlender : IDisposable
    {
        private const uint group_size = 16;

        /// <summary>
        /// The most frames one blend can take. Each channel's sum has 16 bits, which holds 257 frames of 255, so this leaves plenty of room.
        /// </summary>
        public const int MAX_FRAMES = 16;

        /// <summary>
        /// The compute shader, in Vulkan-style GLSL. Veldrid translates it for whichever graphics API the game runs on.
        /// </summary>
        public const string SHADER = """
                                     #version 450

                                     layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

                                     layout(set = 0, binding = 0) uniform texture2D Source;
                                     layout(set = 0, binding = 1) uniform sampler SourceSampler;
                                     layout(set = 0, binding = 2) buffer Sums { uint Values[]; };
                                     layout(set = 0, binding = 3, rgba8) uniform writeonly image2D Blended;
                                     layout(set = 0, binding = 4) uniform Params { int Width; int Height; int Mode; int Count; };

                                     // Mode 0: the first frame of a blend, which replaces the sums.
                                     // Mode 1: a frame in the middle, added to them.
                                     // Mode 2: the last frame, added, then the average written out.
                                     void main()
                                     {
                                         ivec2 p = ivec2(gl_GlobalInvocationID.xy);

                                         if (p.x >= Width || p.y >= Height)
                                             return;

                                         uvec4 c = uvec4(round(texelFetch(sampler2D(Source, SourceSampler), p, 0) * 255.0));
                                         int i = (p.y * Width + p.x) * 2;

                                         uint rg = c.r | (c.g << 16);
                                         uint ba = c.b | (c.a << 16);

                                         if (Mode != 0)
                                         {
                                             rg += Values[i];
                                             ba += Values[i + 1];
                                         }

                                         if (Mode == 2)
                                         {
                                             uint count = uint(Count);
                                             uint rounding = count / 2u;
                                             vec3 average = vec3(((rg & 0xFFFFu) + rounding) / count, ((rg >> 16) + rounding) / count, ((ba & 0xFFFFu) + rounding) / count);
                                             imageStore(Blended, p, vec4(average / 255.0, 1.0));
                                         }
                                         else
                                         {
                                             Values[i] = rg;
                                             Values[i + 1] = ba;
                                         }
                                     }
                                     """;

        private readonly GraphicsDevice device;
        private readonly ResourceFactory factory;

        private readonly Shader shader;
        private readonly ResourceLayout layout;
        private readonly Pipeline pipeline;
        private readonly CommandList commands;

        // One per mode, each with its own constant parameters, so no buffer has to change between frames.
        private readonly DeviceBuffer[] parameters = new DeviceBuffer[3];
        private readonly ResourceSet?[] resources = new ResourceSet?[3];

        private Texture? viewedTexture;
        private TextureView? view;
        private DeviceBuffer? sums;
        private int framesSoFar;
        private int count;

        /// <summary>
        /// The blended frame, written by the last frame of each blend. Read it back before the next blend finishes.
        /// </summary>
        public Texture? Output { get; private set; }

        private GpuFrameBlender(GraphicsDevice device, ResourceFactory factory)
        {
            this.device = device;
            this.factory = factory;

            try
            {
                // Compiled with debug info, which keeps the resource names that OpenGL binds them by.
                var spirv = SpirvCompilation.CompileGlslToSpirv(SHADER, "blend.comp", ShaderStages.Compute, new GlslCompileOptions(true));
                shader = factory.CreateFromSpirv(new ShaderDescription(ShaderStages.Compute, spirv.SpirvBytes, "main"));

                layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("Source", ResourceKind.TextureReadOnly, ShaderStages.Compute),
                    new ResourceLayoutElementDescription("SourceSampler", ResourceKind.Sampler, ShaderStages.Compute),
                    new ResourceLayoutElementDescription("Sums", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute),
                    new ResourceLayoutElementDescription("Blended", ResourceKind.TextureReadWrite, ShaderStages.Compute),
                    new ResourceLayoutElementDescription("Params", ResourceKind.UniformBuffer, ShaderStages.Compute)));

                pipeline = factory.CreateComputePipeline(new ComputePipelineDescription(shader, layout, group_size, group_size, 1));
                commands = factory.CreateCommandList();

                for (int i = 0; i < parameters.Length; i++)
                    parameters[i] = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// A blender for <paramref name="device"/>, or null when its graphics card can't run one. The reason is logged.
        /// </summary>
        public static GpuFrameBlender? TryCreate(GraphicsDevice device, ResourceFactory factory)
        {
            if (!device.Features.ComputeShader || !device.Features.StructuredBuffer)
            {
                Logger.Log($"Render: the graphics card can't blend frames ({device.BackendType} has no compute shaders), so the video has no motion blur");
                return null;
            }

            try
            {
                var blender = new GpuFrameBlender(device, factory);
                Logger.Log($"Render: blending frames for motion blur on the graphics card ({device.BackendType})");
                return blender;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Render: couldn't set up motion blur on the graphics card ({device.BackendType}), so the video has no motion blur");
                return null;
            }
        }

        /// <summary>
        /// Whether <paramref name="source"/> can be blended here: a plain 8-bit colour texture the shader can read.
        /// </summary>
        public static bool CanBlend(Texture source)
            => source.Format is PixelFormat.R8G8B8A8UNorm or PixelFormat.B8G8R8A8UNorm
               && (source.Usage & TextureUsage.Sampled) != 0;

        /// <summary>
        /// Adds <paramref name="source"/> to the blend of <paramref name="frames"/> frames. Call it at the start of the draw frame after it was drawn.
        /// </summary>
        /// <param name="source">The frame. Must pass <see cref="CanBlend"/>.</param>
        /// <param name="frames">How many frames make one blend, from 2 to <see cref="MAX_FRAMES"/>.</param>
        /// <returns>Whether this was the blend's last frame, so <see cref="Output"/> now holds the average.</returns>
        public bool Add(Texture source, int frames)
        {
            if (frames < 2 || frames > MAX_FRAMES)
                throw new ArgumentOutOfRangeException(nameof(frames), frames, $"A blend takes 2 to {MAX_FRAMES} frames.");

            ensureResources(source, frames);

            int mode = framesSoFar == 0 ? 0 : framesSoFar == count - 1 ? 2 : 1;

            commands.Begin();
            commands.SetPipeline(pipeline);
            commands.SetComputeResourceSet(0, resources[mode]!);
            commands.Dispatch((source.Width + group_size - 1) / group_size, (source.Height + group_size - 1) / group_size, 1);
            commands.End();
            device.SubmitCommands(commands);

            framesSoFar = (framesSoFar + 1) % count;
            return mode == 2;
        }

        private void ensureResources(Texture source, int frames)
        {
            bool sizeChanged = Output == null || Output.Width != source.Width || Output.Height != source.Height;

            if (!sizeChanged && ReferenceEquals(viewedTexture, source) && count == frames)
                return;

            if (sizeChanged)
            {
                Output?.Dispose();
                sums?.Dispose();

                Output = factory.CreateTexture(TextureDescription.Texture2D(source.Width, source.Height, 1, 1, PixelFormat.R8G8B8A8UNorm,
                    TextureUsage.Sampled | TextureUsage.Storage));

                // Two 4-byte words a pixel: red and green, then blue and alpha.
                // A raw buffer, as the shader translated for Direct3D 11 reads and writes it by byte address.
                sums = factory.CreateBuffer(new BufferDescription(source.Width * source.Height * 8, BufferUsage.StructuredBufferReadWrite, 4, true));
            }

            if (!ReferenceEquals(viewedTexture, source))
            {
                view?.Dispose();
                view = factory.CreateTextureView(source);
                viewedTexture = source;
            }

            for (int mode = 0; mode < parameters.Length; mode++)
            {
                device.UpdateBuffer(parameters[mode], 0, new[] { (int)source.Width, (int)source.Height, mode, frames });

                resources[mode]?.Dispose();
                resources[mode] = factory.CreateResourceSet(new ResourceSetDescription(layout, view, device.PointSampler, sums, Output, parameters[mode]));
            }

            // A new size or blend length starts a new blend.
            count = frames;
            framesSoFar = 0;
        }

        public void Dispose()
        {
            // Null while the constructor is still failing part way.
            foreach (var set in resources)
                set?.Dispose();

            foreach (var buffer in parameters)
                buffer?.Dispose();

            view?.Dispose();
            sums?.Dispose();
            Output?.Dispose();
            commands?.Dispose();
            pipeline?.Dispose();
            layout?.Dispose();
            shader?.Dispose();
        }
    }
}
