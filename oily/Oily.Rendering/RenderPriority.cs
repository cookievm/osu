// Copyright (c) o!ily contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using System.Diagnostics;
using osu.Framework.Logging;

namespace Oily.Rendering
{
    /// <summary>
    /// Runs renders below normal priority, so a game played at the same time gets the CPU first.
    /// </summary>
    public static class RenderPriority
    {
        /// <summary>
        /// Lowers <paramref name="process"/> to below normal priority. Does nothing if that isn't allowed.
        /// </summary>
        public static void Lower(Process process)
        {
            try
            {
                process.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
            {
                Logger.Log($"Couldn't lower the priority of {process.ProcessName}: {e.Message}");
            }
        }
    }
}
