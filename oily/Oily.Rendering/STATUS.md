# Replay renderer: status (archived 2026-10-27)

Work on the o!ily client is paused, so this renderer is archived after Phase 0. Everything listed here is merged into `oily` (PR #2, merge commit `0213b9f`). Nothing is left unfinished on other branches.

Planning docs: [feasibility findings and phased plan](https://claude.ai/artifact/VJJghtB9KKRt3kd1xBCuaH).

## What works

- **Render replay (experimental)**, in the right-click menu of a local score in the song select leaderboard. It renders the first 10 seconds at 60 fps to numbered PNGs in `%APPDATA%\oily-development\renders\<date-time>\` (`oily` instead of `oily-development` for release builds).
- **Exact, repeatable frame timing.** Frame N always shows gameplay at exactly N / FPS, however fast the PC runs. Mod speed changes (DT, HT) are applied. Two renders of the same replay give the same gameplay times frame for frame.
- **No interference.** Keyboard and mouse input never reaches the player during a render. Escape cancels. The replay controls and the back button are hidden.
- **Tests.** `Oily.Rendering.Tests` has 12 headless tests covering: exact frame times at 60 and 240 fps, DT, the replay still hitting objects, repeat renders matching, and Escape cancelling.

### Tested on Windows (2026-10-27)

ilo's laptop (i5-12500H, RTX 3050 Laptop, 2560x1440 window):

- 600 of 600 frames, every one different (unique SHA-256 hashes), with no repeated or blank frames.
- Gameplay, cursor trail, judgements, HUD and leaderboard all draw correctly. The MP4 made from the frames looks smooth.
- **Speed: 14 min 36 s for 10 s of video**, about 1.46 s per frame.

## How it works

| Piece | File | Role |
| --- | --- | --- |
| Render loop | `ReplayRenderScreen.cs` | Hosts the player in a nested screen stack whose clock is a manual "video clock". It steps one frame, waits until the draw thread has drawn it twice, captures it, then steps again. Before gameplay starts it advances without capturing ("pre-roll"), because the player's entry transition runs on video time. |
| Gameplay clock | `RenderGameplayClockContainer.cs`, `RenderSourceClock.cs` | Replace the music track as the source of gameplay time. Time only moves when the render loop advances it, multiplied by the mod rate. |
| Player | `RenderReplayPlayer.cs` | `ReplayPlayer` with pause, skip, restart and results turned off. Starts 1 s before the first hit object. |
| Capture | `PngSequenceCapturer.cs`, behind `IFrameCapturer` | Uses `GameHost.TakeScreenshotAsync` and saves PNGs. |

Upstream hooks, all marked `// oily`:

- `FramedBeatmapClock` and `GameplayClockContainer` take an `interpolate` flag.
- `OsuGame` has `CanRenderReplays` / `CreateReplayRenderScreen`, plus `ScorePresentType.Render`.
- `OsuGameDesktop` provides the screen.
- `BeatmapLeaderboardScore` has the menu item.

## Key findings

1. **lazer's gameplay clock is not deterministic by default.** `FramedBeatmapClock` smooths time with an `InterpolatingFramedClock`, which runs on the wall clock. With it on, frame times are inexact and differ between runs; the tests fail. The renderer turns it off.
2. **Stepping must freeze time until the frame is on screen.** Waiting for two draw-thread frames after each step was enough to get 600 of 600 unique frames on Windows.
3. **Why it's slow.** `TakeScreenshotAsync` reads back the whole window and stalls the GPU each time (staging texture plus fence). On top of that, each 1440p frame is PNG-encoded before the next step, and output is tied to the window size.
4. **osu!framework is a NuGet package.** Fast off-screen capture (a framebuffer at the export size, plus an async readback ring of 3 or 4 staging textures) likely needs changes inside the framework. That means maintaining a framework fork, or getting a small public API accepted upstream.
5. **Audio is not done.** The plan was to log every sample played (time, file, volume, pan, rate) during the render and mix offline, rather than record live output.

## What's next, if resumed

1. Pipe raw frames to `ffmpeg.exe` instead of PNGs. This is the quick win: no PNG encoding, and real MP4 output.
2. Add resolution, FPS and quality options, and a render-from-results-screen entry.
3. Add off-screen capture at the export resolution with async readback (needs the framework decision above).
4. Phase 2: audio event log, offline mix, and mux.
5. Clean-up: the hitsounds that currently play live during a render; CodeFileSanity still rejects the o!ily licence header under `oily/`.

## Reusing this in a standalone app (for example a companion app)

This renderer runs **inside** the lazer game. It needs osu.Game, the user's beatmaps and skins, and a GPU window, so it can't be dropped into a separate app as a library. Realistic options:

- **Drive o!ily as a separate program.** Add a command-line mode such as `oily.exe --render <replay> --out <file.mp4>` that starts the game, imports the replay, renders and exits. The companion app then just launches it. This is the smallest step from what exists, but it only covers lazer replays.
- **For osu!stable replays (`.osr`), use [danser-go](https://github.com/Wieku/danser-go).** It is a mature offline renderer for stable replays with its own command line, and it powers o!rdr. It is GPL-3.0, so run it as a separate program and don't copy its code.
- **Ideas worth carrying over either way:** fixed-step video time instead of wall-clock time; turn off interpolation and offsets; freeze until each frame is drawn; capture off-screen and pipe raw frames to FFmpeg; mix audio from a sound event log.
