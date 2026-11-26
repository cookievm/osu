// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;

namespace osu.Game.Graphics
{
    /// <summary>
    /// oily: o!ily's own textures, embedded in osu.Game under Resources/Oily (for example the o!ily logo).
    /// </summary>
    public class OilyTextures : TextureStore
    {
        public OilyTextures(IRenderer renderer, GameHost host)
            : base(renderer, host.CreateTextureLoaderStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OilyTextures).Assembly), @"Resources/Oily")))
        {
        }
    }
}
