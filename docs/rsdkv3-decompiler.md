# RSDKv3 → RSDKv4 script decompiler — design notes

## Why this exists

Sonic CD ships **no text scripts**. Its entire gameplay is 88 files of compiled
RSDKv3 VM bytecode (`Data/Scripts/ByteCode/*.bin`). The RSDKv4 engine loads
*either* text scripts *or* bytecode, chosen by one global switch
(`Scene.cpp:675`), so CD cannot simply be dropped into the merged RSDKv4 pack —
it currently loads its background, tiles and collision but has no objects.

Two escape routes were considered:

1. **Graft RSDKv3 support into the RSDKv4 engine.** Rejected: the two VMs differ
   in instruction set, variable model (`Player.X` vs `object.valueNN`), object
   tables and collision routines. Grafting means hand-wiring ~360 KB of
   RSDKv5U's v3 legacy layer into a binary whose state model it does not match.
2. **Swap to RSDKv5U.** Rejected: `InitEngine()` (`RetroEngine.cpp:643`) picks
   `Legacy::v4::LoadGameConfig` or the v3 path from `engine.version`, which is
   set once at startup by `DetectEngineVersion()` from the GameConfig signature.
   One pack therefore carries one GameConfig format, so S1/S2 (v4) and CD (v3)
   cannot coexist. That needs a v4→v3 GameConfig converter *plus* per-stage
   version switching the engine only wires to `modsChanged`.

Decompiling to RSDKv4 text avoids both: one GameConfig, one stage list, one
engine, and the S1 → CD → S2 chain that already works by list adjacency.

## Stage 1 — bytecode reader (done, verified)

`SonicHybridRsdk.Generator/RsdkV3ScriptReader`

All 88 shipped scripts parse to **exactly their byte length**:
302,594 instructions, 2,497 scripts, 3,873 functions.

Container layout (from `LoadBytecode()` in the RSDKV3 decompilation):

```
u32  scriptCodeCount
block-encoded words    opcode stream
u32  jumpTableCount
block-encoded words    branch targets
u16  scriptCount
  per script: 4x u32 scriptCodePtr  (Main, PlayerInteraction, Draw, Startup)
  per script: 4x u32 jumpTablePtr  (same four)
u16  functionCount
  per function: u32 scriptCodePtr, u32 jumpTablePtr
```

Values are block-encoded: a length byte whose bit 7 selects width and whose low
7 bits give the count. Wide blocks hold 32-bit little-endian words, narrow
blocks hold single zero-extended bytes.

Each instruction is an opcode followed by that opcode's tagged operands:
`1` = variable (array-mode byte, index bytes, variable id), `2` = int constant,
`3` = string constant.

### Two things that are easy to get wrong

* A string constant of N characters consumes **`floor(N / 4) + 1` words**, not
  `ceil(N / 4)`. The engine only advances its cursor on `c % 4 == 3` and then
  increments once more at the end. Getting this wrong desynchronises the whole
  stream one word at a time.
* There are **four** script pointers per script, not five. With five, nine
  files still "parsed" but ran off the end of the buffer.

The reader therefore asserts exact byte consumption, and the build fails if any
shipped script cannot be read — a silently partial decompiler would reproduce
the original "loads a background and nothing else" symptom.

## Stage 3 — entry-point resolution (done)

The subroutine entry points are not encoded. The engine keeps a single global
`scriptCode[]` array and appends each bytecode file to it, resetting only via
`ClearScriptData()` (`Script.cpp:2123`). `GS000.bin` — the global object code —
is loaded first, so every later stage file stores indices into that shared array
rather than into its own file:

```
GS000 declared scriptCodeSize : 34554
RS019 first stored entry point : 34554
```

`RsdkV3ScriptReader` carries `ScriptCodeBase` / `JumpTableBase` and `Resolve()`,
converts stored global pointers to local indices, and treats `0x3FFFF` as the
"no such subroutine" sentinel.

**The base offset is measured in raw words, not decoded instructions.** GS000 is
34554 words but only 5833 instructions, because each instruction spans several
words (opcode plus tagged operands). Using the instruction count left every
pointer out of range, which is what made this look like an unsolved encoding.
All 82 entry points in RS019 resolve, and the 14 absent ones read as `0x3FFFF`.

## Stage 4 — opcode mapping (done)

`RsdkV3ScriptWriter` emits all 88 files:

```
subroutines emitted : 7481 / 7481
write failures      : 0
unmapped opcodes    : 0
emitted lines       : 292,975
control-flow anomalies : 13  (0.17%)
```

### Control flow

The bytecode is branch-based but laid out in structured order and marks every
block, so emission is linear and uses those markers for nesting. The jump table
supplies what the stream does not record. Given subroutine start `S` and
jump-table base `J`:

- `If* [k,a,b]` — on failure jumps to `S + jt[k]`, the first instruction of the
  `else` body, or the `endif` when there is no `else`. Pushes `k`.
- `else` — pops `k` and jumps to `S + jt[k+1]`, past the matching `endif`. Both
  paths out of an if therefore converge on `S + jt[k+1]`.
- `W* [k,a,b]` — on failure jumps to `S + jt[k+1]` (the exit); otherwise pushes `k`.
- `loop` — pops `k` and jumps back to `S + jt[k]`, which is the `while` itself,
  so the condition is re-tested each pass.
- `switch [k,sel]` — `low = jt[k]`, `high = jt[k+1]`; out-of-range values go to
  `S + jt[k+2]`, in-range ones to `S + jt[k+4+(sel-low)]`.

Every one of these was confirmed against RS019's instruction stream: all jump
entries land exactly on instruction boundaries. Switch **case values are not
stored anywhere** — they are recovered because the case bodies appear in
ascending case order, so `jt[k+4+i]` is the body for case `low+i`.

Each construct is then cross-checked against the target the engine would use, and
a mismatch is reported rather than emitted.

### Variable names are generated, not typed

The bytecode stores a variable as an index into RSDKv3's `ScrVariable` enum, so
a wrong name does not crash — it silently reads a different property.
`scripts/gen_rsdkv3_variables.py` parses both engine sources and emits the
mapping; 207 of 229 variables map cleanly. See the commit message for the
hand-written enum that this replaced and why it was wrong.

The 22 with no RSDKv4 equivalent are RSDKv3's per-script player physics tuning
(`topSpeed`, `acceleration`, `jumpStrength`, …), which RSDKv4 does not expose.
They are 1649 references, 0.5% of the total, and are emitted as
`/*UNMAPPED v3 NAME*/` rather than given a wrong name.

### Opcodes that cannot be emitted verbatim

Emitted as `// TODO` with the reason, counted by the build:

| occurrences | note |
|--|--|
| 1784 | `PlayerObjectCollision` — RSDKv4 needs `foreach (GROUP_PLAYERS,…)` + `BoxCollisionTest` |
| 912 | `CallFunction` — RSDKv4 calls a function by name, not index |
| 697 | `Sin` — different unit |
| 312 | `Cos` — different unit |
| 60 | `CopyPalette` — RSDKv4 takes five arguments |
| 51 | `Rand` — different arguments |
| 46 | `Cos256` |
| 38 | `Sin256` |
| 36 | `ATan2` — different return range |
| 20 | `RotatePalette` — RSDKv4 takes four arguments |
| 2 | `EngineCallback` — no equivalent |

### Known remaining anomalies — 13 of 7481

Reported by the build, not fatal. Two shapes:

- 4 routines where a switch's out-of-range target is not its `endswitch`.
- 9 routines with an `else`/`endif` pair that has no open `if`. `RS030`
  `script2.PlayerInteraction` is the clearest: its routine contains six `if`s and
  seven `endif`s, so the engine's own `jumpTableStack` goes negative there. The
  bytecode is unbalanced, not the decompiler — these are flagged for a human
  rather than guessed at.

## Stage 5 — emit the scripts (not yet done)

The writer is correct but nothing calls it to produce files yet.

**How the three games can coexist — resolved, not assumed.** RSDKv4 picks text
scripts or bytecode per scene with
`if (bytecodeExists && !forceUseScripts)` (`Scene.cpp:675`, and again at 787).
`bytecodeExists` is simply whether `Bytecode/GlobalCode.bin` is present in the
data folder. So shipping that file is what turns bytecode on; omitting it leaves
RSDKv4 on text scripts for everything.

Since Sonic 1 and Sonic 2 already ship text scripts, the answer is to ship **no**
bytecode and emit Sonic CD as text, so all three games run through the same
script path. That is the whole of Stage 5, and it is now unblocked.

## Why no object logic runs in ANY of the three games

Found while wiring up Stage 5. This is not specific to Sonic CD and it predates
the decompiler work.

RSDKv4 runs an object through exactly three entry points. On startup every
object's pointers are set to a sentinel:

```
Script.cpp:3318-3325   eventUpdate / eventDraw / eventStartup = SCRIPTCODE_COUNT - 1
```

Only two things ever overwrite that sentinel:

```
Script.cpp:2848/2856/2864   text parsing, on `eventObjectUpdate`,
                            `eventObjectDraw`, `eventObjectStartup`
Script.cpp:3191/3200/3209   loading bytecode
```

`Object.cpp` then guards every call with `scriptCode[...scriptCodePtr] > 0`, so an
object whose pointers are still the sentinel has no behaviour whatsoever.

**The tracked S1/S2 text scripts contain none of those markers.** Across all 748
files:

```
eventObjectUpdate    0
eventObjectDraw      0
eventObjectStartup   0
function Main        0
function Draw        0
function Update      0
function StartUp     0
files with no function at all : 319
```

They use a `function <Name> ... end function` dialect with names like
`Bridge_DebugDraw`. Those functions are only reachable via `CallFunction`; they
are not entry points. So the engine parses them, registers the functions, and
never calls any of them.

The pack does not ship bytecode either. `Build.cs` mounts only `Data/` and
`Scripts/`, and `rsdk-source-data/sonic1/Bytecode/` and `sonic2/Bytecode/` —
which contain `GlobalCode.bin`, `Zone01.bin` and the rest, i.e. the real object
behaviour — are never copied. So `bytecodeExists` is false, RSDKv4 takes the text
path, and every object ends up with sentinel entry points.

That is the actual cause of the original symptom this project shipped with:
stages load and draw their background, and nothing else happens. It is not a CD
problem and not a decompiler problem.

### What this means for the plan

CD is not the odd one out. S1 and S2 have working RSDKv4 bytecode sitting
un-copied in `rsdk-source-data/*/Bytecode/`; only CD needs converting, and CD's
bytecode is RSDKv3 format.

RSDKv4 picks bytecode or text **per stage load**, keyed on whether
`Bytecode/GlobalCode.bin` resolves — a single global file. So the two formats
cannot be mixed: if that file is present every stage uses bytecode, and if it is
absent every stage uses text. That leaves two coherent routes:

1. Ship S1/S2 bytecode and convert CD to RSDKv4 **bytecode**, which needs an
   RSDKv4 script *compiler* (text or RSDKv3 bytecode in, RSDKv4 bytecode out).
2. Emit everything as **text**, which means S1/S2's behaviour has to be
   recovered from their bytecode first, since their `.txt` files are not in
   engine format.

Route 1 is less work, because S1/S2 already have valid bytecode. It does mean
writing a compiler rather than a decompiler.

### Why the S1/S2 bytecode cannot simply be copied in

Worth stating precisely, because the obvious fix is a trap.

RSDKv4 resolves bytecode per stage as `Bytecode/<folder>.bin`, where `<folder>`
is the stage's `Path` from the merged stage list (`Script.cpp:3075-3085`), plus
`Bytecode/GlobalCode.bin` for global objects. The per-stage files could be
renamed into place with no trouble:

```
sonic1/Bytecode/Zone01.bin  ->  ZoneGHZ.bin
sonic2/Bytecode/Zone01.bin  ->  ZoneEHZ.bin
```

The collisions are all in the non-stage files, which have the *same names* in both
games and each serve a different game:

```
Continue.bin  Credits.bin  Ending.bin  GlobalCode.bin
LSelect.bin   Special.bin  Title.bin
```

`GlobalCode.bin` is the hard one. It holds the global object scripts indexed by
object type, and RSDKv4 loads exactly one of them for the whole process. Sonic
1's is 73,448 bytes and Sonic 2's is 88,160, and they disagree about what each
object type is. Concatenating is not valid: the container is
`scriptCode | jumpTable | scriptCount | per-script pointers`, and the pointers are
absolute indices into the shared arrays, so merging needs the indices remapped.

There is also no partial fix. The text/bytecode choice keys on whether
`Bytecode/GlobalCode.bin` resolves, which is a single global file: if it is
absent every stage uses the text scripts, and if it is present every stage uses
bytecode. Shipping per-stage bytecode without a merged `GlobalCode.bin` would
change nothing, because the text path would still be taken - and the text path is
the one with no entry points.

So the bytecode route needs a merger that concatenates two `GlobalCode.bin`
containers and rewrites every pointer in the later one. That is the same kind of
job as the compiler above: it needs the container format and the object-type
table understood well enough to renumber them.

## Sonic 2 boots its scripts, then hangs in an infinite loop

With Sonic 2's bytecode shipped, the stage load is correct - verified from the
engine's own log:

```
Loading Scene Regular Stages - EMERALD HILL ZONE 1
Loaded Data File 'Data/Stages/ZoneEHZ/StageConfig.bin'
Set Object (4) name to: Stage Setup
Set Object (45) name to: EHZ Setup
Loaded Data File 'Bytecode/ZoneEHZ.bin'
Loaded Data File 'Data/Music/EmeraldHill.ogg'
```

That is the right stage, the right setup object, the right bytecode and the right
music. The merged stage list, the renamed bytecode lookup and the per-stage object
table are all working.

**But the log then stops, and the process sits at 99% of one core.** Measured:

```
CPU used in 8s wall: 7.95s  (99% of one core)
```

The log is byte-identical at 301 lines whether the engine is given 22 seconds or
45, which is the signature of a spin rather than slow progress. So the stage never
reaches gameplay: an object script is looping without terminating, so the frame
never completes.

This is also what made the earlier spritesheet symptom look worse than it was.
`Data/Sprites/MBZ/Objects.gif` - Sonic 1's Marble Zone - is loaded in *both*
Sonic 2 stages, which is a real oddity, but the music is correct
(`EmeraldHill.ogg`) and the stage assets are correct. What looked like "wrong
sprites" was a load sequence that stops dead before the zone's own sheet is
reached. Fixing the hang is the prerequisite; the sprite question should be
re-checked afterwards rather than chased now.

Not yet isolated: which script loops. Candidates are the stage setup object's
Startup and the player object, both of which run before the first frame
completes. Isolating it needs either a breakpoint on `ProcessScript` or a run
that dumps the script name when a single script executes more than N instructions
in one frame - the latter is the better fix, because a runaway loop is exactly
the failure mode that must never be silent.

### Also unresolved

`Failed to load string... (en, StageName13..16)` and `SaveStageName26`. These were
previously dismissed as harmless because `strStageList`/`strSaveStageList` are
written but never read by the engine. That reasoning still holds for the list
itself, but now that stages actually load, the missing names are the kind of
thing that shows up as blank stage titles, so they are worth merging rather than
leaving.

## Verification standard

A stage only counts as working when it has been **run** and objects are seen
spawning. A stage that compiles but spawns nothing is not progress — that is the
exact failure this project shipped for months.