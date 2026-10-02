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

### Two things found by tracing, one still open

**`functions[]` has 151 entries, not 153.** The source declares 153
`FunctionInfo` entries but two sit inside `#if !RETRO_REV00` and are not compiled
in. Anything indexing the table by position must skip the guarded ones or every
opcode after the first guard is off by one or two. `scripts/rsdkv4_opcodes.py`
walks the guards properly and is the single place the table is derived; a
disassembly that disagrees with the engine's own opcode trace is the symptom.

**Sonic 1 Green Hill runs; other Sonic 1 zones hang in the Stage Setup object.**

With the per-stage pointers renumbered (see below) Green Hill reaches 600 frames.
Marble Zone and Final Zone run 0 frames, and the engine's instruction budget
reports:

```
WARNING: runaway script: object type 4, event 0, over 500000 instructions in one call
```

Object type 4 is `Stage Setup`. Its trace is a tight cycle:

```
WLower at word 25932
GetTableValue at word 25940
SetTableValue at word 25949
Inc at word 25958
loop at word 25962
```

So the loop's condition is never satisfied. Not yet diagnosed: a static walk of
script 3 desynchronises at word 25072, which means the operand encoding has more
structure than "opcode followed by `opcodeSize` words" - operands are tagged and
variable operands carry an extra index word, so a naive fixed-width walk drifts.
The engine's own trace is trustworthy; a static disassembler built on the naive
model is not, and building one correctly is the next step rather than guessing at
the loop's cause from a bad disassembly.

### Why the per-stage pointers had to be renumbered

Per-stage containers hold absolute indices into the engine's global
`scriptCode`/`jumpTable` arrays, built by appending each file as it loads. Which
base a file uses depends on its stage list, measured across all 33 shipped
containers rather than assumed:

    regular / bonus / ending / continue              base = GlobalCode word count
    presentation (Title, LSelect, Credits, Special)  base = 0

Merging the two `GlobalCode.bin` files changes the global word count, so every
regular-stage file has to shift by the same delta. Before this, Sonic 2's stages
ran whatever script happened to sit at the old offset, which is why adding the
merge broke a game that had been working.

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
