# o!ily: archived

**Status: archived on 27 October 2026.** Work on the o!ily client is paused for the foreseeable future. Everything below describes the state it was left in, so it can be picked up again later.

## What works

| Area | State |
| --- | --- |
| Separate install | Done. o!ily runs alongside official osu!lazer with its own window title, `oily.exe`, data folder (`%APPDATA%\oily`, or `oily-development` for debug builds) and IPC pipe. The official auto-updater, Discord Rich Presence and the legacy IPC port are off. See `oily/Oily.Identity` ([PR #1](https://github.com/cookievm/osu/pull/1)). |
| Replay renderer, Phase 0 spike | Done, verdict **go**. Right-click a score on the song select leaderboard and choose "Render replay (experimental)". It steps the game exactly one frame at a time and saves a numbered PNG sequence to `%APPDATA%\oily-development\renders\<timestamp>\`. On an i5-12500H / RTX 3050 laptop at 1440p, 10 seconds rendered 600/600 unique frames in 14 min 36 s (about 1.5 s per frame). The frames were turned into an MP4 with FFmpeg, and the motion looked smooth. See `oily/Oily.Rendering` ([PR #2](https://github.com/cookievm/osu/pull/2)). |
| Tests | `oily/Oily.Identity.Tests` and `oily/Oily.Rendering.Tests` (exact frame times at 60 and 240 fps, Double Time, two renders match). |

## How to build and run

You need Windows, [Git](https://git-scm.com/downloads) and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`global.json` asks for 10.0.100 or newer). In PowerShell:

```
git clone https://github.com/cookievm/osu.git C:\dev\oily
cd C:\dev\oily
dotnet run --project osu.Desktop
```

The first run takes a few minutes. To run the o!ily tests: `dotnet test oily/Oily.Identity.Tests` and `dotnet test oily/Oily.Rendering.Tests`.

This branch is based on osu!lazer at upstream commit `efac271` (October 2026). Picking it up much later means merging a newer ppy release into `oily` first (see "Branches" below). Expect conflicts in the files marked `// oily:`.

## Branches

- `oily`: the fork's main branch, and the GitHub default.
- `master`: an untouched copy of ppy's `master`. Never edited, only synced from upstream.
- `feature/*`: one branch per change, merged into `oily` through pull requests. (A branch can't be called `oily/...` while `oily` exists.)

## Planned next, not started

1. **Renderer Phase 1:** a faster offscreen frame readback, piping raw frames straight to FFmpeg for a silent MP4. osu!framework comes as a NuGet package, so this probably means forking osu!framework or getting a public API added upstream.
2. **Renderer Phases 2 to 5:** offline audio mixing, a correctness test suite, an export screen (resolution, FPS, quality, output folder) and polish (hardware encoders, motion blur, custom mods).
3. **Client UI customisation and custom mods, including Skin Cycle:** planned only.

## Decisions made

- **Name:** the display name is o!ily. Paths, file names and code use `oily`. "osu!" and "ppy" are trademarks and must not be used as o!ily's own name or icon.
- **Platform:** Windows only (Direct3D 11) for the first release.
- **osu! accounts: linked account plus lazer import (chosen 27 Oct 2026, not built).** ppy's developers state that unofficial builds are blocked from osu!'s live servers, and that bypassing that gets accounts restricted ([forum, Sep 2026](https://osu.ppy.sh/community/forums/topics/2243137)). So o!ily must never reuse official lazer's client ID or submit scores. The chosen plan:
    - Sign in through the public [osu! API v2](https://osu.ppy.sh/docs/index.html) with o!ily's own OAuth app, to show the profile, rank and friends, and online leaderboards on song select. Open question: does osu!'s OAuth support PKCE, so a desktop app needs no client secret?
    - One-click import of beatmaps, skins and replays from official lazer's data folder.
    - Release builds use no live osu! servers. Debug builds keep using `dev.ppy.sh`, as upstream does.
- **Known issue:** the upstream CodeFileSanity check rejects the o!ily licence header on files under `oily/`. GitHub Actions doesn't run on this fork anyway.
