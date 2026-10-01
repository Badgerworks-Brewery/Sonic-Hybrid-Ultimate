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

## Stage 2 — control flow (specified, not yet implemented)

The jump table stores **pairs**. For a conditional at slot `k`:

| Opcode    | Behaviour |
|-----------|-----------|
| `IfEqual [k,a,b]`   | if `a != b` jump to `jumpTableStart + jumpTable[k]`; push `k` |
| `else`              | jump to `jumpTableStart + jumpTable[top + 1]`; pop |
| `endif`             | pop |
| `WEqual [k,a,b]`    | if `a != b` jump to `jumpTableStart + jumpTable[k+1]`, else push `k` |
| `loop`              | jump back to `jumpTableStart + jumpTable[top]` |

So `jumpTable[k]` is the false-branch distance and `jumpTable[k+1]` is the
loop-back / else distance. Structured `if / else / end if` and
`while / loop` are recoverable by pattern-matching those opcodes against the
table rather than by tracing execution.

`switch` / `case` / `break` / `endswitch` and `CallFunction` / `EndFunction`
come from the function table, which gives each function's script and jump-table
base pointers.

## Stage 3 — entry-point resolution (root cause found; one bug outstanding)

**Root cause of the "encoded" entry points — confirmed.** They are not encoded.
The engine keeps a single global `scriptCode[]` array and appends each bytecode
file to it, resetting only via `ClearScriptData()` (`Script.cpp:2123`).
`GS000.bin` — the global object code — is loaded first, so every later stage
file's stored entry points are indices into that shared array rather than into
their own file.

The numbers confirm it exactly:

```
GS000 declared scriptCodeSize : 34554
RS019 first stored entry point : 34554
```

so RS019's local index is `stored - 34554`. `RsdkV3ScriptReader` now exposes
`ScriptCodeBase` / `JumpTableBase` and `Resolve()` for this, and `0x3FFFF` is
recognised as the "no such subroutine" sentinel.

**Outstanding bug.** After this change, resolving entry points against a base of
5833 rather than 34554 leaves every pointer out of range, and GS000 itself
decodes to 5833 code words in C# when its header declares 34554. An independent
Python decode of the same bytes yields the full 34554 words and lands at the
correct offset, so the format is right and the **C# `ReadBlocks` is wrong for
this file**. Note the reader still reports exact byte consumption for all 88
files, so the byte accounting is self-consistent while the word count is not —
which is why the earlier "parses cleanly" checks did not catch it.

Next step: reconcile C# `ReadBlocks` against the reference decoder for GS000,
specifically the wide/narrow block selection, before any emission is trusted.

Until then the writer correctly refuses to run: it throws when an entry point
falls outside the instruction stream rather than emitting an empty script.

## Stage 4 — opcode → RSDKv4 mapping (not yet started)

## Stage 4 — opcode → RSDKv4 mapping (not yet started)

`rsdkv3-to-rsdkv4.md` documents the semantic differences that matter:

* `CopyPalette(a,b)` → `CopyPalette(a,0,b,0,256)`
* `RotatePalette(a,b,c)` → `RotatePalette(0,a,b,c)`
* `PlayerObjectCollision(t,l,top,r,b)` → a `foreach (GROUP_PLAYERS, ...)` loop
  around `BoxCollisionTest`, since RSDKv3 hardcoded the single player
* `PlaySfx(22,0)` → `PlaySfx(SfxName[Boss Hit], 0)` (numeric IDs become names)
* properties: `Object.XVelocity` → `object.xvel`, `Player.Timer` →
  `object.value1`, `TempValue0` → `temp0`, and so on (full table in the notes)
* object-array scans (`while ArrayPos0 < 1056`) become `foreach`, which is both
  shorter and dramatically faster

The 135-opcode table and the 233-entry `ScrVariable` enum are already
extracted; only the variable-id → RSDKv4-name mapping beyond the object/player
property block still needs filling in.

## Verification standard

A stage only counts as working when it has been **run** and objects are seen
spawning. A stage that compiles but spawns nothing is not progress — that is the
exact failure this project shipped for months.