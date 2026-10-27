# o!ily

> **Archived (27 October 2026).** Work on o!ily is paused. See [ARCHIVE.md](ARCHIVE.md) for its state, how to build it, and the open decisions.

o!ily is a custom client forked from [osu!lazer](https://github.com/ppy/osu). It is not affiliated with or endorsed by ppy.

The display name is **o!ily**. Wherever a name ends up in a path, file name or code identifier, it is written **oily** instead, since `!` is awkward there.

## Layout

All o!ily-specific code lives in this folder, one project per feature:

| Project | Purpose |
| --- | --- |
| `Oily.Identity` | Client name, data folder, IPC pipe and other values that keep o!ily separate from official osu!lazer |
| `Oily.Identity.Tests` | Checks that those values never collide with official osu!lazer |
| `Oily.Rendering` | Renders replays to video by stepping the game one frame at a time (experimental) |
| `Oily.Rendering.Tests` | Checks that rendered frames land on exact, repeatable gameplay times |

Changes to upstream files are kept as small as possible and marked with a `// oily:` comment (`<!-- oily: -->` in project files), so they are easy to find when merging a new osu!lazer release.

## Running alongside official osu!lazer

| | Official osu!lazer | o!ily |
| --- | --- | --- |
| Window title | `osu!` | `o!ily` |
| Data folder (Windows) | `%APPDATA%\osu` | `%APPDATA%\oily` (`oily-development` for debug builds) |
| Executable | `osu!.exe` | `oily.exe` |
| IPC pipe | `osu-lazer` | `oily` (`oily-debug` for debug builds) |
| Auto-updates | From ppy/osu releases | Off |
| Discord Rich Presence | On | Off |
| Legacy IPC (port 45357) | On | Off, unless `OILY_ENABLE_LEGACY_IPC=1` |

## Renaming the client

Change the values in `Oily.Identity/OilyIdentity.cs`, plus `AssemblyName` in `osu.Desktop/osu.Desktop.csproj` and `osu_filename` in `osu.Desktop/NVAPI.cs`. Changing the data folder name moves existing users to an empty folder, so a rename after release needs a migration step.
