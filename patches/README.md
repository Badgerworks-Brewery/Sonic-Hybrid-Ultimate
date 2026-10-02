# Engine patches

`Hybrid-RSDK-Main/RSDKV4-Decompilation` is a git submodule, so changes inside it
cannot be committed to this repository directly. Patches that belong to this
project live here instead, and are applied to the submodule before building.

## `rsdkv4-runaway-guards.patch`

Diagnostics and guards added while tracking the stall that stopped the engine
before its first rendered frame. Every change exists to turn a failure that is
otherwise **silent** into a reported one - a 100%-CPU spin with no log output is
the worst kind of bug to diagnose from outside the process.

- `main.cpp`, `RetroEngine.cpp` - heartbeats from `main()` through
  `Init()` and `Run()`, plus a bounded spin counter and a per-iteration report of
  the frame's focus state. Without these there is no way to distinguish "never
  reached the loop" from "reached it and stalled inside".
- `Object.cpp`, `Scene.cpp` - heartbeats in `ProcessObjects` and `ProcessStage`,
  logging the first 20 calls and every 300th.
- `Script.cpp` - a per-call instruction budget in `ProcessScript`. No legitimate
  object needs 500,000 instructions for one call, so exceeding it means a loop
  whose condition never changes. Reports the object type and event, then bails.
- `Audio.cpp` - bounds the music fill loop twice. On end-of-stream with looping
  enabled it seeks to the loop point and continues, and if that seek yields no
  samples `ov_read` keeps returning 0. Separately, the loop tops the stream up to
  `bytes_wanted` and only finishes when the audio device drains it; if nothing
  drains it, `SDL_AudioStreamAvailable` never grows and the loop spins forever.

### What they established

The game **is not hanging**. With `settings.ini` present the engine reaches its
frame loop and runs:

```
HEARTBEAT: RetroEngine::Run entered
HEARTBEAT: past ProcessEvents; hasFocus=1 focusState=0 disableFocusPause=0 vsPlaying=0
HEARTBEAT: ProcessStage call 1, stageMode 0
Loading Scene Regular Stages - DEATH EGG ZONE
...
HEARTBEAT: ProcessStage call 900, stageMode 1
HEARTBEAT: ProcessObjects frame 900, gameMode 1
```

900 frames, the stage loaded, its bytecode loaded. The engine is simply running
flat out because there is no vsync in this environment, which is why it looked
like a hang: no window, no visible output, one core saturated.

**The earlier "stall" diagnosis was wrong, and the reason it was wrong matters.**
For several runs the log stopped dead after the last asset load and CPU sat at
~100%. The cause was that `settings.ini` had been removed between runs. The
engine reads `settings.ini` from its resource path during `Init()`, and with no
`[Window] RefreshRate` present, `Engine.refreshRate` fell back to a value that
made `targetFreq` in `RetroEngine::Run()` degenerate - the loop then spins
without ever doing frame work, before any heartbeat inside it can run.

So: a missing config file presents as an infinite loop with no output. That is
worth keeping in mind for any future "the engine hangs" report, and it is why the
first instinct - an object script looping - was a dead end.

Apply with:

    git -C Hybrid-RSDK-Main/RSDKV4-Decompilation apply ../../patches/rsdkv4-runaway-guards.patch

### Building

`build_all.ps1` re-bootstraps vcpkg over the network and fails when that is
unavailable. Building the two relevant targets directly does not need it:

    msbuild build/Hybrid-RSDK-Main/rsdk_core.vcxproj /p:Configuration=Release
    msbuild build/Hybrid-RSDK-Main/rsdkv4.vcxproj  /p:Configuration=Release

Both are required: `rsdk_core` is the static library and `rsdkv4` is the
executable, and rebuilding only the library leaves the old `rsdkv4.exe` in place
with none of the changes in it.
