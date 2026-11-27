// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Skinning
{
    /// <summary>
    /// oily: a skin component that only holds settings. It draws nothing and can't be moved; picking it in the skin editor's
    /// component list opens its settings (the layer keeps one of it).
    /// </summary>
    public interface ISettingsOnlySkinComponent : ISkinComponentWithLabel
    {
    }
}
