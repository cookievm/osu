// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using NUnit.Framework;
using Veldrid;
using Veldrid.SPIRV;

namespace Oily.Rendering.Tests
{
    [TestFixture]
    public class GpuNv12ConverterTest
    {
        // The shader only runs on a graphics card, so this checks it translates for each graphics API the game can use,
        // keeping the resource names that OpenGL binds them by.
        [TestCase(CrossCompileTarget.HLSL)]
        [TestCase(CrossCompileTarget.GLSL)]
        [TestCase(CrossCompileTarget.MSL)]
        public void TestShaderTranslates(CrossCompileTarget target)
        {
            var spirv = SpirvCompilation.CompileGlslToSpirv(GpuNv12Converter.SHADER, "nv12.comp", ShaderStages.Compute, new GlslCompileOptions(true));
            var result = SpirvCompilation.CompileCompute(spirv.SpirvBytes, target);

            Assert.That(result.ComputeShader, Does.Contain("Source"));

            var resources = result.Reflection.ResourceLayouts;
            Assert.That(resources, Has.Length.EqualTo(1));
            Assert.That(resources[0].Elements, Has.Length.EqualTo(4));
            Assert.That(resources[0].Elements[2].Kind, Is.EqualTo(ResourceKind.StructuredBufferReadWrite));

            if (target == CrossCompileTarget.GLSL)
                Assert.That(result.ComputeShader, Does.Contain("buffer Output").And.Contain("uniform Params").And.Contain("sampler2D Source"));

            if (target == CrossCompileTarget.HLSL)
                Assert.That(result.ComputeShader, Does.Contain("RWByteAddressBuffer"));
        }

        [Test]
        public void TestShaderSourceIsAscii()
        {
            Assert.That(Encoding.UTF8.GetByteCount(GpuNv12Converter.SHADER), Is.EqualTo(GpuNv12Converter.SHADER.Length));
        }
    }
}
