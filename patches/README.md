# Engine patches

`Hybrid-RSDK-Main/RSDKV4-Decompilation` is a git submodule, so changes inside it
cannot be committed to this repository directly. Patches that belong to this
project live here instead, and are applied to the submodule before building.

## `rsdkv4-runaway-guards.patch`

Diagnostic guards, added while tracking down the hang that stops the engine
before the first frame. Each turns a failure that is otherwise **silent** into a
reported one, which is the point: a 100%-CPU spin with no log output is the worst
kind of bug to diagnose from outside the process.

- `Script.cpp` — a per-call instruction budget in `ProcessScript`. No legitimate
  object needs 500,000 instructions for one call, so exceeding it means a loop
  whose condition never changes. Reports the object type and event, then bails.
- `Audio.cpp` — bounds the music fill loop. On end-of-stream with looping
  enabled it seeks back to the loop point and continues; if that seek yields no
  samples, `ov_read` keeps returning 0 and the loop spins forever.
- `Object.cpp` — a heartbeat in `ProcessObjects`, logging the first 20 frames and
  every 300th after. Without it there is no way to tell a stall before the frame
  loop from a merely slow load.

### What they found

Neither the script budget nor the music guard trips, and `ProcessObjects` is
**never reached** - not even frame 1. The engine spins at ~100% of one core with
the log stopping dead after the last asset load, identically for a stage and for
the title screen.

So the stall is not a runaway object script and not the OGG loop; it happens
before the frame loop's object dispatch begins. That is where the investigation
stopped, and it is recorded here so the next attempt starts from the narrowing
rather than repeating it.

Apply with:

    git -C Hybrid-RSDK-Main/RSDKV4-Decompilation apply ../../patches/rsdkv4-runaway-guards.patch
