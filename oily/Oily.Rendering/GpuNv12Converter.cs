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
    /// Converts frames to NV12 on the graphics card, with the same maths as <see cref="Nv12Converter"/>.
    /// </summary>
    /// <remarks>
    /// The CPU then only copies 1.5 bytes per pixel back instead of converting 4, which takes most of the render's CPU time off it.
    /// Everything here must be called on the draw thread.
    /// </remarks>
    public sealed class GpuNv12Converter : IDisposable
    {
        // Each invocation converts a block of 4x2 pixels: two 4-byte words of brightness and one of colour.
        private const uint group_size = 8;

        /// <summary>
        /// The compute shader, in Vulkan-style GLSL. Veldrid translates it for whichever graphics API the game runs on.
        /// </summary>
        public const string SHADER = """
                                     #version 450

                                     layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

                                     layout(set = 0, binding = 0) uniform texture2D Source;
                                     layout(set = 0, binding = 1) uniform sampler SourceSampler;
                                     layout(set = 0, binding = 2) buffer Output { uint Pixels[]; };
                                     layout(set = 0, binding = 3) uniform Params { int Width; int Height; int Padding0; int Padding1; };

                                     ivec3 fetch(int x, int y)
                                     {
                                         vec4 c = texelFetch(sampler2D(Source, SourceSampler), ivec2(x, y), 0);
                                         return ivec3(round(c.rgb * 255.0));
                                     }

                                     // BT.709 limited range, scaled by 256, as Nv12Converter.
                                     uint luma(ivec3 c)
                                     {
                                         return uint(((47 * c.r + 157 * c.g + 16 * c.b + 128) >> 8) + 16) & 0xFFu;
                                     }

                                     void main()
                                     {
                                         int blockX = int(gl_GlobalInvocationID.x);
                                         int blockY = int(gl_GlobalInvocationID.y);

                                         if (blockX >= Width / 4 || blockY >= Height / 2)
                                             return;

                                         int x0 = blockX * 4;
                                         int y0 = blockY * 2;

                                         uint top = 0u;
                                         uint bottom = 0u;
                                         uint colour = 0u;

                                         for (int i = 0; i < 2; i++)
                                         {
                                             int x = x0 + i * 2;
                                             uint shift = uint(i * 16);

                                             ivec3 a = fetch(x, y0);
                                             ivec3 b = fetch(x + 1, y0);
                                             ivec3 c = fetch(x, y0 + 1);
                                             ivec3 d = fetch(x + 1, y0 + 1);

                                             top |= (luma(a) << shift) | (luma(b) << (shift + 8u));
                                             bottom |= (luma(c) << shift) | (luma(d) << (shift + 8u));

                                             ivec3 sum = a + b + c + d;
                                             uint u = uint(((-26 * sum.r - 86 * sum.g + 112 * sum.b + 512) >> 10) + 128) & 0xFFu;
                                             uint v = uint(((112 * sum.r - 102 * sum.g - 10 * sum.b + 512) >> 10) + 128) & 0xFFu;

                                             colour |= (u << shift) | (v << (shift + 8u));
                                         }

                                         Pixels[(y0 * Width + x0) / 4] = top;
                                         Pixels[((y0 + 1) * Width + x0) / 4] = bottom;
                                         Pixels[(Width * Height + blockY * Width + x0) / 4] = colour;
                                     }
                                     """;

        private readonly GraphicsDevice device;
        private readonly ResourceFactory factory;

        private readonly Shader shader;
        private readonly ResourceLayout layout;
        private readonly Pipeline pipeline;
        private readonly DeviceBuffer parameters;

        private Texture? viewedTexture;
        private TextureView? view;

        private uint parametersWidth;
        private uint parametersHeight;

        private GpuNv12Converter(GraphicsDevice device, ResourceFactory factory)
        {
            this.device = device;
            this.factory = factory;

            try
            {
                // Compiled with debug info, which keeps the resource names that OpenGL binds them by.
                var spirv = SpirvCompilation.CompileGlslToSpirv(SHADER, "nv12.comp", ShaderStages.Compute, new GlslCompileOptions(true));
                shader = factory.CreateFromSpirv(new ShaderDescription(ShaderStages.Compute, spirv.SpirvBytes, "main"));

                layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("Source", ResourceKind.TextureReadOnly, ShaderStages.Compute),
                    new ResourceLayoutElementDescription("SourceSampler", ResourceKind.Sampler, ShaderStages.Compute),
                    new ResourceLayoutElementDescription("Output", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute),
                    new ResourceLayoutElementDescription("Params", ResourceKind.UniformBuffer, ShaderStages.Compute)));

                pipeline = factory.CreateComputePipeline(new ComputePipelineDescription(shader, layout, group_size, group_size, 1));
                parameters = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// A converter for <paramref name="device"/>, or null when its graphics card can't run one. The reason is logged.
        /// </summary>
        public static GpuNv12Converter? TryCreate(GraphicsDevice device, ResourceFactory factory)
        {
            if (!device.Features.ComputeShader || !device.Features.StructuredBuffer)
            {
                Logger.Log($"Render: the graphics card can't convert colours ({device.BackendType} has no compute shaders), converting on the CPU");
                return null;
            }

            try
            {
                var converter = new GpuNv12Converter(device, factory);
                Logger.Log($"Render: converting colours on the graphics card ({device.BackendType})");
                return converter;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Render: couldn't set up colour conversion on the graphics card ({device.BackendType}), converting on the CPU");
                return null;
            }
        }

        /// <summary>
        /// Whether <paramref name="source"/> can be converted here. Otherwise the CPU converts it.
        /// sRGB textures can't, as the shader would read them as linear colour, which isn't what the CPU path or the video expects.
        /// </summary>
        public static bool CanConvert(Texture source)
            => source.Format is PixelFormat.R8G8B8A8UNorm or PixelFormat.B8G8R8A8UNorm
               && (source.Usage & TextureUsage.Sampled) != 0
               && source.Width % 4 == 0 && source.Height % 2 == 0;

        /// <summary>
        /// Records converting <paramref name="source"/> into <paramref name="target"/>'s staging buffer, ready to map once the commands are done.
        /// </summary>
        /// <param name="commands">The command list to record into, between Begin and End.</param>
        /// <param name="source">The frame. Must pass <see cref="CanConvert"/>.</param>
        /// <param name="target">The buffers to convert into. Created or resized as needed.</param>
        public void Record(CommandList commands, Texture source, ref Target? target)
        {
            uint width = source.Width;
            uint height = source.Height;
            uint length = (uint)RenderFrame.LengthOf((int)width, (int)height, RenderPixelFormat.Nv12);

            if (!ReferenceEquals(viewedTexture, source))
            {
                view?.Dispose();
                view = factory.CreateTextureView(source);
                viewedTexture = source;
            }

            if (target == null || target.Length != length || !ReferenceEquals(target.View, view))
            {
                target?.Dispose();
                target = new Target(factory, layout, view!, device.PointSampler, parameters, length);
            }

            if (parametersWidth != width || parametersHeight != height)
            {
                commands.UpdateBuffer(parameters, 0, new[] { (int)width, (int)height, 0, 0 });
                parametersWidth = width;
                parametersHeight = height;
            }

            commands.SetPipeline(pipeline);
            commands.SetComputeResourceSet(0, target.Resources);
            commands.Dispatch(divideRoundingUp(width / 4, group_size), divideRoundingUp(height / 2, group_size), 1);
            commands.CopyBuffer(target.Output, 0, target.Staging, 0, length);
        }

        private static uint divideRoundingUp(uint value, uint divisor) => (value + divisor - 1) / divisor;

        public void Dispose()
        {
            // Null while the constructor is still failing part way.
            view?.Dispose();
            parameters?.Dispose();
            pipeline?.Dispose();
            layout?.Dispose();
            shader?.Dispose();
        }

        /// <summary>
        /// Where one frame is converted to, and the staging copy the CPU maps.
        /// </summary>
        public sealed class Target : IDisposable
        {
            public readonly uint Length;
            public readonly TextureView View;
            public readonly DeviceBuffer Output;
            public readonly DeviceBuffer Staging;
            public readonly ResourceSet Resources;

            public Target(ResourceFactory factory, ResourceLayout layout, TextureView view, Sampler sampler, DeviceBuffer parameters, uint length)
            {
                Length = length;
                View = view;

                // A raw buffer, as the shader translated for Direct3D 11 reads and writes it by byte address.
                Output = factory.CreateBuffer(new BufferDescription(length, BufferUsage.StructuredBufferReadWrite, 4, true));
                Staging = factory.CreateBuffer(new BufferDescription(length, BufferUsage.Staging));
                Resources = factory.CreateResourceSet(new ResourceSetDescription(layout, view, sampler, Output, parameters));
            }

            public void Dispose()
            {
                Resources.Dispose();
                Staging.Dispose();
                Output.Dispose();
            }
        }
    }
}
