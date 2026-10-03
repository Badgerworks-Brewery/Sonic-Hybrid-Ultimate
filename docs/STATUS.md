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

1. Finish the bytecode walker. Every remaining Sonic 1 defect reduces to rewriting a
   baked operand, and the same machinery is a prerequisite for compiling CD. The
   residue is spread across ordinary opcodes rather than concentrated, which suggests
   more `opcodeSize` archaeology of the DrawText kind — check handlers, not counts.
2. Rewrite Sonic 1's baked object-type and `CallFunction` operands.
3. Build the RSDKv3 → RSDKv4 bytecode compiler for Sonic CD.
4. Sonic 3, once someone supplies the ROM and `sonic3air.exe`.