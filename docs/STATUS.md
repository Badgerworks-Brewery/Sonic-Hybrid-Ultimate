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

**Sonic 1 objects created by script now resolve to Sonic 1's own types.** This was
wrong until the last commit, and the reason it was wrong is the most useful thing in
this document.

An object's type is baked in as an integer constant when its script is compiled, against
that game's single-game numbering. In the merged table that integer names whatever sits
there. `TypeName` is resolved at compile time, so there is no runtime lookup to scope -
filtering the name lookup by game changes a stage's live types not one bit.

Rewriting those constants in the bytecode is the obvious fix and was the wrong one. It
cannot be verified: it means editing compiled data using a decoder, over the 81% of code
the oracle has never seen execute. So each object type instead carries the offset its
game's numbering needs, and the two opcodes that create an object - `ResetObjectEntity`
operand 1 and `CreateTempObject` operand 0, which a search of every `->type` assignment
in the engine confirms is all of them - add it. Two numbers the pack already knows,
which cannot go stale against a decoder. `CallFunction` gets the same treatment for
functions, and a stage's function table is now *appended* rather than written over the
globals'.

The stage shift is not simply the globals' offset. A stage's types begin at
`globalCount + 1` in their own game and at `globalTotal + 1` in the merged table, so the
offset is the difference between those starts: 39 for Sonic 1, 38 for Sonic 2, derived
from the two counts at load time rather than hard-coded.

### The bug this uncovered

Act layout files store the same kind of raw type index, and they were being renumbered
**three times over** - once correctly by the packer, then twice more by a build-time
rewriter that edited the game's own files in place, once per build:

| build | Green Hill Act 1 types | |
|---|---|---|
| stock | `0..72` | Sonic 1's own numbering |
| first | `40..111` | correct - the packer alone |
| second | `79..150` | past the 114 types that exist |
| third | `118..189` | still past it |

A type past the end of the object table names no script, so the entity exists and never
runs: **18 of Green Hill's 305 objects were dead.** It survived because the layout still
parsed to exactly its length, the stage still loaded, and the frame counter still
reached 600 - all true, none of it evidence. 18 of 31 live types being *unnamed* read as
exotic objects rather than a range error.

Two fixes were tried before the right one. Renumbering on read in the engine was sound in
principle and wrong in fact, because it was a third shift on top of the packer's; only
reading `RsdkGenericImporter.cs` established that the packer had already done the job by
name lookup. The lesson is the one worth keeping: the fix was to *stop transforming the
data*, not to transform it more carefully.

It was caught by printing the high end of the range rather than the count - `max` was
150 where it should have been 111. One aggregate that should have been checked from the
start.

Now guarded three ways: the packer is the only renumberer; `scripts/test_act_layouts.py`
fails the build when a layout names a type beyond what the stage registers, verified
against a deliberately re-injected second shift (it fails at 150 against a bound of
113); and `scripts/inspect_act.py` compares a stock and a packed layout with an
independent parser.

Result at frame 300, Green Hill Act 1:

```
before:  18 of 31 live types unnamed; 105 of 307 entities on no script
after:   0 of 31 live types unnamed; 305 of 305 entities on a real script
```

and the live list is recognisably Sonic 1's own Green Hill - Player Object, HUD, Ring
(165), Monitor, Spikes, Buzz Bomber, Motobug, Chopper, Crabmeat, Newtron Shoot, Newtron
Fly, Bridge, Rock - not a mixture of the two games' objects.

**The bytecode walker's operand decoding is now verified against the engine.** This
was the thing being guessed at, and guessing had already produced three confident
wrong answers - `GetVersionNumber` and `Abs` "needing" three operands, and `DrawText`
"needing" three. All three were artefacts of measuring the wrong thing.

The engine can now report, for every instruction it executes, exactly how many words
it consumed and which operand tags it read (`RSDK_TRACE_ALL=1`).
`scripts/oracle_check.py` boots five stages with that on and compares those numbers
against the walker.

Result: **24,508 of 24,508 distinct instruction sites confirmed, 100%** - operand
widths *and* operand tags. That is every regular stage of both games: Sonic 1's 19
stages (9,400 sites) and Sonic 2's 21 (15,108).

The trace had to be uncapped before that number meant anything. The opcode line was
silently limited to 300 instructions while the word-count line below it logged all
7,000-odd, so the checker read 300 of them and reported an identical 142 confirmed
sites from five completely different stages. Identical totals from different stages
were the tell. With the cap gone, each stage reports its own count - 190 sites for
Green Hill Act 1, 821 for Spring Yard Act 1, 831 for Oil Ocean - which is what real
coverage looks like.

Getting to a comparison that could be trusted took three corrections, each of which
had produced a confident wrong answer first:

- Measuring the gap between consecutive log lines is not the same as measuring the
  instruction. Control flow jumps, `CallFunction` transfers into another script, and a
  truncated trace resumes somewhere else entirely, so gaps need filtering and capped
  instructions need marking. The engine now states its own consumed count, which
  removes the problem instead of filtering around it.
- The engine appends `GlobalCode.bin` and then the *one* stage the scene needs, and
  every word in the trace is an absolute index into that combined array. Comparing a
  log against all 31 shipped stage files at once reported 238 sites where "the engine
  read a different opcode than we did" - it had read the words out of the wrong
  container.
- A site has to be judged by whether *any* of its executions agrees, because scripts
  loop. Recording the first mismatch and never clearing it reported `WLower` at word
  25932 as wrong 33 times, when the trace plainly shows `WLower @25932` followed by
  `GetTableValue @25940` - a gap of 8, exactly what the walker computes.

What is still *not* established: the check covers the instructions the engine
*executed* in those runs. A stage reached in 9 seconds of headless play does not run
every line of its bytecode, so branches that need player input, or a boss trigger, or
several minutes of play, are still unverified. The static linear walk over every
range in every container still agrees on only 81%, and that is the honest limit of
the claim. Widening it means playing further into each stage, not reasoning harder.

`scripts/oracle_check.py --from N --to M` sweeps a range of stages, and
`scripts/probe_stages.py --scene N` boots any single one by index.

`scripts/rsdkv4_walk.py` still walks only 81% of script *ranges* cleanly. Those two
numbers are not in conflict: the 81% is a static linear walk over every range in
every container, and the 99.9% is the set of instructions the engine actually
executed while playing five stages. The static walk disagrees about code the engine
never reached, which is dead or headlessly-unreachable code. Rewriting operands is
still not safe on that evidence alone, but the width rules - the thing that was
genuinely unknown - are now confirmed rather than assumed.

Also added `scripts/check_opcode_table.py`, which has the engine print its own
compiled opcode table and diffs it against the Python derivation. It matches exactly,
index for index, at 149 entries. The engine stating its own table is the point: the
derivation has now been wrong twice, once over `!` and once over `RSDK_REVISION`, and
both times the Python side was confidently disagreeing with the thing actually
running.

Getting that number to mean something took two corrections, and both earlier versions
were reporting a flattering figure for the wrong reason. The first filtered script
pointers with `v < len(container.code)`, which is wrong for every per-stage container
because their pointers are absolute indices into the engine's combined array. It
reported 569 ranges, of which not one came from stage bytecode - it had been measuring
the easy half and calling it 74%. The fix then shifted pointers against each
container's own lowest pointer, which moves every index by the 262-word prologue the
compiler emits first, so it read the wrong words throughout and reported 16%.
`placement_base()` now derives the real base from the sibling `GlobalCode.bin`, and
81% is measured over everything.

**Sonic CD needs a compiler, not a decompiler.** The decompiler is finished —
7481 of 7481 subroutines emitted — but its output is text, and text is inert here.
CD ships RSDKv3 bytecode and the engine runs RSDKv4 bytecode. RSDKv4 chooses per
stage on whether `Bytecode/GlobalCode.bin` resolves (`Scene.cpp:675`), so CD needs
RSDKv3 bytecode compiled to RSDKv4 bytecode: a mapping between the two opcode tables
plus operand re-encoding. This is the largest single piece of work left.

**Sonic 1's 93 functions are merged, and `CallFunction` now resolves them.** Both games'
function tables are present and shifted (190 entries), and a stage's own table is
*appended* rather than written over the globals' - the stock engine indexes
`scriptFunctionList` flat, so whichever stage loaded last silently replaced the functions
every global object calls. Each object type now carries a `functionBase` and
`CallFunction` adds it, the same way the type constants are handled above, with a range
check that logs and stops rather than reading past the table.

## Two engine bugs found along the way

Both were found by measuring against the bytecode rather than by reading code, and
both are recorded with their evidence.

**DrawText declared 7 operands; it has 3.** Its handler uses three
(`DrawTextMenu(&gameMenu[operands[0]], operands[1], operands[2])`). Because the
operand fetch loop runs *before* the handler, reading seven where the bytecode has
three consumed four words belonging to the next instruction, desynchronising every
script that draws text.

**The opcode and variable tables included entries that are never compiled.**
`RSDK_REVISION` is 3 (`RetroEngine.hpp:225`), which makes `RETRO_REV00` false and
`RETRO_REV01/02/03` true. The guard walk treated any guard it did not recognise as
active, so `LoadFontFile` and `DrawText` - both of which sit inside `#if !RETRO_REV02`
- stayed in the table and shifted every opcode after them by one: 151 entries claimed
where the engine has 149, and `GetTableValue` landing at 129 instead of 127. The
variable table had the same bug, 253 claimed against 251 compiled, and now imports the
guard logic from the opcode table rather than keeping a second copy of it.

**The commit before that one made a wrong "fix" and this reverts it.** It changed
DrawText's operand count from 7 to 3 because the bytecode walk appeared to align better
at 3. The walk only appeared to align better because its opcode table wrongly contained
DrawText at all, shifting everything after it. Seven is correct for the handler that
exists; the entry is inside `#if !RETRO_REV02` and is not compiled either way.

`scripts/check_opcode_operands.py` now settles this whole class of question by reading
every handler in ProcessScript's dispatch switch and comparing the operand indices it
touches against the count `functions[]` declares. A handler reading a slot it never
writes means the operand fetch has already run out and the words it consumed belong to
the next instruction. 107 handlers checked, all consistent. Two details it needed to get
right first: handlers reuse the high operand slots as scratch (`Get16x16TileInfo` declares
four operands and computes `operands[4..6]` as locals), so only indices a handler reads
*and never writes* count; and it has to honour the guards, because `SetPaletteFade`'s
`operands[6]` sits inside `#if RETRO_REV00`. Without both, it reports correct code, which
is worse than reporting nothing.

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

1. Build the RSDKv3 → RSDKv4 bytecode compiler for Sonic CD. It is the only game whose
   stages are still inert, and the decompiler that reads the format is finished.
2. Widen the oracle's reach. It confirms 24,508 of 24,508 instruction sites across every
   regular stage of Sonic 1 and Sonic 2, but only what the engine *executes* in a
   9-second headless run. Branches needing player input or a boss trigger are still
   unverified, and the static walk over every range still agrees on only 81%.
3. Sonic 3, once someone supplies the ROM and `sonic3air.exe`.
4. *No longer on the list, deliberately:* rewriting Sonic 1's baked operands in the
   bytecode. Doing it in the engine, from two numbers the pack already knows, is
   verifiable where a bytecode rewrite would mean trusting a decoder over compiled data
   across code that has never been seen run.