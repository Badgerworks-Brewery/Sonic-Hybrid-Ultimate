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

**The bytecode walker's operand decoding is verified against the engine, but the
check that verifies it was wrong, and the figure it produced is withdrawn.**

The engine can report, for every instruction it executes, how many words it consumed
and which operand tags it read (`RSDK_TRACE_ALL=1`), and `scripts/oracle_check.py`
compares those against the walker's decoding. It reported **24,508 of 24,508 distinct
instruction sites confirmed, 100%**, across every regular stage of Sonic 1 and Sonic 2.

That number is withdrawn. `check()` had three bare `continue`s that dropped any entry
which did not line up - no container covers the word index, the two read different
opcodes, the walker's decoder threw. A checker that discards what it cannot explain can
only ever report success, so "every entry that survived the filter agreed" was a
statement about the filter, not about the walker. Every one of those paths is now
counted and printed, and the summary separates *placed* from *confirmed*:

```
instructions the engine executed: 29789
instruction sites placed against the walker's containers: 950 (3.2%)
of those, confirmed: 950 (100%)
```

Two things follow, and the first is the serious one.

**Stage-object code is not being placed at all.** Every distinct site resolves inside
`GlobalCode.bin`, whose 115,998 words cover the globals; no traced instruction lands in
the 30,381 words of `GHZS1.bin` or `CPZS2.bin`, even though those containers load and
their objects demonstrably run - Green Hill spawns Buzz Bombers, Crabmeats and Newtron
Fly, which are stage objects. Either the walker's placement base for a regular stage is
wrong, or stage-object code is executing from somewhere other than where the container
says it lives. Distinct coverage per stage fell from ~831 before the object-numbering
work to exactly 190 now, the same figure for all five stages, which is the shape of
something that stopped rather than something that varies. **This is unresolved and is the
next thing to look at.**

Until it is, the honest statement is narrow: *where the walker and the engine could be
compared on a site the walker had placed, they agreed on 950 of 950.* That is worth
something - widths and tags both, on real executed code - and it is a great deal less
than 24,508.

What is still genuinely established, because each was checked against the engine rather
than inferred:

- The derived opcode table matches the engine's compiled table exactly, at 149 entries.
  The stock table wrongly included `LoadFontFile` and `DrawText`, which sit inside
  `#if !RETRO_REV02` and never compile, so every opcode after them was shifted by one.
- All 107 handlers stay within their declared operand count.
- `scripts/inspect_act.py` reads Act layouts with a parser independent of the packer's.
- The Act layout type bounds hold, checked against a re-injected second shift.

The measurement lesson is the same one this project keeps relearning, and it is now
written down: **an aggregate that should have been checked from the start.** `max` of a
type range was 150 where it should have been 111, and it took printing the high end to
see it. Here, the aggregate that was missing was the count of instructions that could
*not* be compared - reported as 0 because the code that would have counted them was a
`continue`.

The trace itself had to be uncapped before any of this meant anything. The opcode line
was silently limited to 300 instructions while the word-count line below it logged all
7,000-odd, so the checker read 300 of them and reported an identical "142 confirmed"
from five completely different stages. Identical totals from different stages were the
tell, and I read past them twice.

`scripts/oracle_check.py --from N --to M` sweeps a range of stages;
`scripts/probe_stages.py --scene N` boots any single one by index. Both now write
`DisableFocusPause=1`, because with no window to take focus the engine sees
`hasFocus=0` and pauses, which reports as a stage that stops after 20 frames - a failure
that looks exactly like a broken stage and was not one.

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

1. **Find out why no stage-object instruction is placed.** `scripts/oracle_check.py`
   maps every distinct site it can confirm into `GlobalCode.bin` and none into the stage
   containers, though their objects run. Until that is explained the walker is verified
   on 950 sites rather than on the game, and it is the cheapest open question here -
   everything else about the bytecode is downstream of trusting it.
2. Build the RSDKv3 → RSDKv4 bytecode compiler for Sonic CD. It is the only game whose
   stages are still inert, and the decompiler that reads the format is finished.
3. Widen the oracle once it is trustworthy. It covers only what the engine *executes* in
   a 9-second headless run, so branches needing player input or a boss trigger stay
   unverified regardless.
4. Sonic 3, once someone supplies the ROM and `sonic3air.exe`.
5. *No longer on the list, deliberately:* rewriting Sonic 1's baked operands in the
   bytecode. Doing it in the engine, from two numbers the pack already knows, is
   verifiable where a bytecode rewrite would mean trusting a decoder over compiled data
   across code that has never been seen run.