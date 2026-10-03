# Status: what runs, what does not, and what is left

Written against the build in this repository. Every claim below was checked by
running the engine and reading `Hybrid-RSDK-Main/sonic-hybrid/log.txt`, not by
reading code. Where something is claimed to work, the evidence is quoted.

## Working

**Sonic 1 — Green Hill, Marble Zone, Final Zone.** Each loads its own bytecode and
runs 600 frames with objects spawning. Evidence from the live object types in Green
Hill at frame 300:

```
type 40 Player Object     1        type 95 Buzz Bomber      11
type 49 Ring            165        type 97 Motobug          2
type 52 Monitor          10        type 100 Crabmeat        3
type 55 Yellow Spring     4        type 104 Newtron Fly     6
type 61 Special Ring      1
type 74 Spikes           26
```

Zero invalid-opcode warnings, zero runaway script events. Sonic 1's own object types,
its own enemies and its own platforms — that is Green Hill, not a stage that merely
advances a frame counter.

**Sonic 2 — unchanged.** Chemical Plant Zone Act 2 runs 600 frames with 489 live
entities, same warnings count. Sonic 2 was working before any of this and still is;
every change was regression-checked against it.

**Sonic CD — data present, no bytecode.** All 70 stages load their assets and object
lists, and their object names parse correctly from each stage's `StageConfig.bin`
(verified: `PataBata`, `TagaTaga`, `Flip Door`). Every one lacks
`Bytecode/Zone<CC><A><T>.bin`, so its objects never run. See "Sonic CD" below.

**Sonic 3 — no data.** `rsdk-source-data` holds only sonic1, sonic2 and soniccd. The
lone artefact `rsdk-source-data/sonic3.bin` is 4 MB, untracked, has no AIR signatures
and no `Data/` paths, and is not the ROM. `SONIC3_AIR_SETUP.md` needs a
user-supplied Sonic 3 & Knuckles ROM plus `sonic3air.exe`; neither is present.
`OxygenWrapper.cpp` correctly reports stub mode rather than pretending otherwise.

## Not working, and why

**Sonic 1 objects created by script still carry Sonic 1's own type numbers.** A
stage's Act layout file stores object placements as a raw type index
(`Scene.cpp:995`), and those are renumbered correctly now. But an object a *script*
creates has its type baked in as an integer constant when the script was compiled,
compiled against Sonic 1's single-game numbering. In the merged table that integer
names whatever sits there — often a Sonic 2 object, sometimes nothing at all.
Measured: Green Hill has live types up to 150 while the last registered type is 113.

There is no runtime lookup to fix, which I checked rather than assumed. Filtering the
name lookup by game changes Green Hill's live types not one bit, because `TypeName` is
resolved at compile time. Rewriting those constants needs a bytecode walker.

**The bytecode walker is at 61%.** `scripts/rsdkv4_walk.py` agrees with the engine on
346 of 569 script ranges. Three earlier walkers were deleted rather than shipped; this
one is kept because its docstring records two real engine bugs it found (see below)
and states plainly that it cannot be used to rewrite operands. Nothing depends on it.

**Sonic CD needs a compiler, not a decompiler.** The decompiler is finished —
7481 of 7481 subroutines emitted — but its output is text, and text is inert here.
CD ships RSDKv3 bytecode and the engine runs RSDKv4 bytecode. RSDKv4 chooses per
stage on whether `Bytecode/GlobalCode.bin` resolves (`Scene.cpp:675`), so CD needs
RSDKv3 bytecode compiled to RSDKv4 bytecode: a mapping between the two opcode tables
plus operand re-encoding. This is the largest single piece of work left.

**Sonic 1's 93 functions are merged but their callers are not renumbered.** Both games'
function tables are now present and shifted (190 entries). `CallFunction`'s operand is
an index into that shared table, so a Sonic 1 script calling function *N* still reaches
Sonic 2's *N*. Same root cause as the type constants: baked operands.

## Two engine bugs found along the way

Both were found by measuring against the bytecode rather than by reading code, and
both are recorded with their evidence.

**DrawText declared 7 operands; it has 3.** Its handler uses three
(`DrawTextMenu(&gameMenu[operands[0]], operands[1], operands[2])`). Because the
operand fetch loop runs *before* the handler, reading seven where the bytecode has
three consumed four words belonging to the next instruction, desynchronising every
script that draws text.

**The opcode table's `#if` walk ignored `!`.** `scripts/rsdkv4_opcodes.py` treated
`#if RETRO_REV00` and `#if !RETRO_REV00` identically, so `SetPaletteFade` was recorded
as 7 operands instead of 6.

There is also a trap worth naming, because it is easy to fall into: searching for an
operand width that reduces the walker's desync count produces convincing
improvements that are wrong. `GetVersionNumber` "size 3" and `Abs` "size 3" both look
like large wins; both handlers use exactly the declared number of operands. Read the
handler. Never trust the count.

## Where the object-name collision stands

Sonic 1 and Sonic 2 share 33 object names, and the engine resolves a name by scanning
the whole table, so whichever game registered first wins. The engine now scopes that
scan to one game when `Data/Game/ObjectGameSplit.bin` says where the global table
splits, and takes the stage's game from an `S1`/`S2` suffix on its folder name
(`GHZS1`, `MZS1`, `EHZS2`, `CPZS2`, …). Absent the split file the engine falls back to
the original single-game scan.

This is correct but currently unexercised on the bytecode path, for the reason above.
It does matter for text scripts, which are currently inert.

## Deliberately not claimed

- Sonic 1 and Sonic 2 have never been played through in sequence. Both work
  individually; the transition between them is untested.
- No stage has been verified with visible rendering. The engine cannot open a window in
  this environment, so everything here is established from object state, not pixels.
- The test suite is 18 passed, 2 failed, and is meant to be red. Both failures report
  real remaining defects — Sonic CD having no working stage, and the script-entrypoint
  check finding 748 inert text scripts. Do not make it green by weakening assertions.
- Emerald Hill and Death Egg are excluded from the probe by request.

## Next steps, in order of value

1. Finish the bytecode walker. Every remaining Sonic 1 defect reduces to rewriting a
   baked operand, and the same machinery is a prerequisite for compiling CD. The
   residue is spread across ordinary opcodes rather than concentrated, which suggests
   more `opcodeSize` archaeology of the DrawText kind — check handlers, not counts.
2. Rewrite Sonic 1's baked object-type and `CallFunction` operands.
3. Build the RSDKv3 → RSDKv4 bytecode compiler for Sonic CD.
4. Sonic 3, once someone supplies the ROM and `sonic3air.exe`.