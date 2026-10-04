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

**Sonic 3 — the ROM is on this machine; I wrongly wrote it off.** `rsdk-source-data`
holds only sonic1, sonic2 and soniccd, so for a long time the conclusion was "no Sonic 3
data". That was half right and wrong about the one artefact that mattered.

`rsdk-source-data/sonic3.bin` is 4,194,304 bytes and untracked. I previously dismissed it
on the grounds that it "has no AIR signatures and no `Data/` paths, and is not the ROM".
That reasoning was bad: a raw Mega Drive ROM would not contain AIR data paths, so their
absence was evidence of nothing. Searching the machine turned up
`C:\Users\charl\Documents\school\N\Sonic_Knuckles_wSonic3.bin`, and the two files are
byte-identical:

```
FA52AC946DFD576538D00AA858B790B9D81A1217E25AA5193693A4E57F4F89D9  school\N\Sonic_Knuckles_wSonic3.bin
FA52AC946DFD576538D00AA858B790B9D81A1217E25AA5193693A4E57F4F89D9  rsdk-source-data\sonic3.bin
```

The header agrees with the filename (`scripts/identify_md_rom.py` reads it rather than
trusting the name):

```
console      'SEGA GENESIS    '
rom title    'SONIC & KNUCKLES'
product code 'GM MK-1563 -00'    <- 0x180, Sega's own code for Sonic & Knuckles
region       20                  <- world
```

The data has the shape of a real cartridge rather than a blank or padded file: all eight
512 KiB blocks are between 353,773 and 496,982 non-zero bytes, it is not a doubled or
mirrored dump (the two halves differ in 1,830,210 of 2,097,152 bytes), and 0x080000
begins `64 02 44 42 31 42 00 40 4E 75 11 7C`, which decodes as plausible 68k.

**What I could not verify.** My header checksum does not match the stored one — and it
also fails on a known-good Sonic 3 dump in `Retropie\roms\megadrive`, so the fault is in
`checksum_16bit()`, not in these files. The reset vector at 0x8A reads `0x000002`, which
is also wrong for a real cartridge. Both checks are therefore reported as *unverified*
rather than as evidence either way, and the tool says so. I am not going to call a ROM
fake on the strength of a check that fails on good input.

So Sonic 3 is blocked on two things rather than three: `sonic3air.exe` is still missing,
and the ROM still needs unpacking into AIR data by something that understands the format.
`OxygenWrapper.cpp` continues to report stub mode, which is correct. The ROM is not
committed - it is game data, and it stays on this machine.

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

**Corrected: stage-object scripts do run. Both earlier claims about them were
measurement artefacts, and the thing that produced them is worth more than either.**

What I reported twice, in order:

1. *No traced instruction ever falls in the stage container's range, so stage-local
   scripts never execute.*
2. *`ProcessStartupObjects` dies partway through Sonic 1's globals, at about type 55, so
   every stage object from there on never gets its startup.*

Both are false, and both were the same mistake: **the instrumentation was slower than the
measurement window.**

`RSDK_TRACE_ALL=1` writes several log lines for every opcode the engine executes. That
slows `ProcessStartupObjects` enough that a 12-second run kills the process while the loop
is still going. The probe then reported however far it had got - 53 types once, 44 the
next time - and I read a truncated measurement as a crash.

Removing the per-instruction trace and logging only the loop's progress:

```
no-trace run: startup loop reached 110 types
highest type reached: 113
last 8: [106, 107, 108, 109, 110, 111, 112, 113]
```

All 110 types, every stage object included. The loop was never dying.

That also dissolves the first claim on its own. Under tracing the engine had only got
through the globals when the timeout killed it, which is exactly why every traced word sat
in `GlobalCode`'s range - not because stage code was unreachable, but because the run never
reached it.

### What is actually true, measured

With a budget long enough for the traced engine to finish the work - 30 seconds per stage
rather than 9:

```
stages traced: 5
instructions the engine executed: 47752
instruction sites placed against the walker's containers: 4157
of those, confirmed: 4157 (100.0%)
not placed, so not compared: 0        (unplaced 0, opcode differs 0, undecodable 0)
```

Per stage: Marble Zone 3 821 sites, Final Zone 821, Chemical Plant 2 863, Oil Ocean 1 831.

Every one of those 4,157 sites was compared, and all four ways of *failing* to compare are
zero: no word index no container covers, no place where the walker and the engine read
different opcodes, nothing the walker could not decode, and no disagreement about operand
widths or tags. The earlier 950-of-950 was the same agreement over a much smaller window,
cut short by the same timeout; the 24,508 figure was that, inflated by a checker that
discarded what it could not explain.

The 47,752 against 4,157 is not a shortfall. The engine re-executes the same sites every
frame; 4,157 is the number of *distinct* instructions, and each was checked once.

### The `CallFunction` base is exonerated

Worth recording as a negative result, since I was about to blame it. `scripts/
bisect_function_base.py` runs the same stage twice, once with the per-object function base
and once with `RSDK_NO_FUNCTION_BASE=1` (stock behaviour, which is wrong in the other
direction). Both runs reached the same 44 types and the same 44 distinct entry points. The
base does not stop anything; only the extra 3,240 traced instructions differ, which is more
execution rather than further reach.

### Standing rules that came out of this

- A measurement that changes the thing it measures is a measurement, not evidence. The
  oracle now defaults to 30 seconds per stage and `docs/STATUS.md` records that
  `RSDK_TRACE_ALL` costs roughly a 3x slowdown, because that is why the number moved.
- Diagnostics that could not have shown what I wanted are worse than none. The
  `STARTUPTYPE` run printed only the five types hard-coded into it; I read the two that
  appeared as "the loop stopped at 40", which was my own filter talking.
- Prefer a counter to a sample. "Reached 110 types, highest 113" answers the question;
  "types 4 and 40 logged" answers a different, narrower one that I then over-read.
- Still worth fixing: the startup loop would fail silently if it ever *did* die, since
  nothing between `ProcessStartupObjects` and the frame loop reports how far it got. The
  `STARTING` log now answers that, but only when asked.

### The startup loop can no longer fail silently

`ProcessStartupObjects` iterates all 256 object types and runs each one's `eventStartup`.
If it stops early, every type after that point quietly never runs its startup while the
stage still loads, still places its objects and still turns frames. Nothing between that
function and the frame loop reported its progress, which is precisely why two rounds of
investigation here produced confidently wrong answers about it.

`startupObjectsCompleted` and `startupObjectsReached` now exist, and `ProcessObjects` logs
the outcome on its first turn:

```
STARTUP completed after reaching type 255 of 255
```

Unconditional, not behind a trace switch, because the case it guards against is exactly
the case where nobody thinks to turn tracing on. Reaching 255 rather than 113 is also the
right answer and not a surprise: the loop covers every slot in `objectScriptList`, and
unregistered slots hold the sentinel `SCRIPTCODE_COUNT - 1`, whose word is 0, so the
existing `scriptCode[ptr] > 0` guard skips them without running anything.

### A build check that was itself broken

Adding those globals produced two unresolved externals, and the build reported success,
because the check filtered on `error C` - compiler errors only. Link failures are `LNK`,
and the filtered output was empty, so "build ok" printed while `rsdkv4.exe` had been
deleted by the failed link. `scripts/probe_stages.py` then said "engine not built", which
is how it was caught.

Same shape as everything else in this document: the thing that was supposed to tell me
whether a step worked was reporting success while measuring nothing.

`scripts/build_engine.py` replaces it. It builds both targets - skipping `rsdk_core`
reuses a stale `rsdk_core.lib`, which has silently tested old code here before - and
judges by **exit code**, not by grepping the output. Widening the filter to `error` would
have been the same idea with a bigger net and still wrong: MSBuild can fail without
printing anything containing that word. It also treats the reverse case as a failure, an
exit code of 0 with an error in the output, which is the direction the old check got
backwards.

### Kept, because they made this findable

`PLACE` (a container's own placement base, jump base, type range, function base),
`STAGESCRIPT` (stage entry points and the *values* of those words in memory - which is what
proved the code was present and non-zero, so "never reached" was not "pointed at nothing"),
`ENTER` (distinct entry points `ProcessScript` is entered at), and `STARTING` (every type
the startup loop reaches, now on its own `RSDK_TRACE_STARTUP` switch so measuring it cannot
change it).

## Where this stands

Suite: **20 passed, 2 failed**. Both failures are real and intended:

- Sonic CD has no RSDKv4 bytecode, so its stages load their assets and object lists but
  their objects never run.
- The object-script entry point check.

Sonic 1 and Sonic 2 stages: bytecode loads, objects are placed on the right game's types
(305 of 305 on Green Hill), and the engine reports `STARTUP completed after reaching type
255 of 255` for itself. What the bytecode walker is verified against:

```
GREEN HILL ZONE 1   traced 99140 | placed 1807 confirmed 1807 | 0 gaps
GREEN HILL ZONE 2   traced 27422 | placed 2004 confirmed 2004 | 0 gaps
```

All 19 Sonic 1 regular stages in one sweep: **1,684,355 instructions executed, zero sites
dropped.** The first sweep of the same stages managed 120,462, so the ceiling was never
the bytecode - it was the cost of writing the trace. One line per instruction instead of
one per operand tag took it from 190 distinct sites per stage to roughly 2,000.

Sonic 3's ROM is on this machine (`C:\Users\charl\Documents\school\N\Sonic_Knuckles_wSonic3.bin`,
byte-identical to `rsdk-source-data/sonic3.bin`). What is still missing is `sonic3air.exe`
and anything that unpacks a ROM into AIR data.

## The thing worth reading if you only read one thing

Every confident wrong answer in this project's history has the same shape: **a measurement
that was truncated, filtered, or too slow, reported as a result.**

1. The trace's opcode line was capped at 300 instructions while the word-count line below
   it logged all 7,000-odd. The checker read 300 and reported an identical "142 confirmed"
   from five different stages. Identical totals from different stages were the tell.
2. `check()` had three bare `continue`s dropping anything it could not place, so it could
   only ever report success. 24,508 of 24,508, 100%.
3. The trace's I/O cost meant a traced run never got past the globals, so coverage sat at
   exactly 190 distinct sites for stage after stage. I read that twice as "stage scripts
   never execute". They do.
4. The build check filtered on `error C`, so two link failures passed and it printed "ok"
   while the failed link had deleted the executable.
5. Two oracle runs share one `log.txt`, so a background sweep and a test run overwrote
   each other and produced "1 stage traced" plus a non-zero gap count - which read as a
   checker regression.

So the standing rules, and each one exists because its absence cost real time:

- **Prefer a counter to a sample.** "Reached 110 types, highest 113" answers the question.
  "Types 4 and 40 logged" answered a narrower one, which I then over-read as "the loop
  stopped at 40" - it had only printed the five types I hard-coded into it.
- **A measurement that changes what it measures is not evidence.** `RSDK_TRACE_ALL` made
  the engine several times slower, so a fixed timeout measured a run that had not happened.
- **Make tools refuse rather than guess.** The oracle now exits 3 on a held lock and
  reports "log locked" rather than reading whatever it finds.
- **Let the engine state facts about itself.** `STARTUP completed after reaching type 255
  of 255` is unconditional, not behind a trace switch, because the case it guards against
  is exactly the one where nobody thinks to turn tracing on.
- **Do not filter log text to decide whether a build succeeded.** Check the exit code.
  `scripts/build_engine.py` exists because of that.

## First real finding from the honest checker

With the drop-and-discard bug fixed and the trace fast enough to actually finish the
startup loop, the oracle finally reports failures. It had been reporting 100% by
construction for the whole project; this is the first disagreement it is able to state.

Sonic 1, all 19 regular stages:

```
instructions the engine executed: 1258761
instruction sites placed: 32595
of those, confirmed: 32593 of 32595 (100.0%)
not placed, so not compared: 257
```

**18 of 19 stages are perfectly clean.** One stage is not:

```
MARBLE ZONE 2 (scene 4)  traced 91218 | placed 1847 confirmed 1845 | WRONG 2
                          opcode differs 227, undecodable 30
```

The two width disagreements, both on `Equal`:

```
MZS1.bin word 121077  Equal  2 operands: walker 5 words tags [1, 2], engine 7 words tags [1, 2]
MZS1.bin word 126301  Equal  2 operands: walker 8 words tags [1, 1], engine 7 words tags [1, 2]
```

and a cascade around one region, which is the signature of a single mis-sized
instruction rather than 227 independent bugs:

```
word 116644: engine read IfEqual,      walker read Equal
word 116654: engine read SetMusicTrack, walker read Equal
word 116665: engine read Equal,         walker read ShR
word 116673: engine read Equal,         walker read End
word 116681: engine read else,          walker read Inc
```

Once the walker mis-sizes one instruction it reads every later instruction in that stream
at the wrong offset, so the *names* diverge too. The cascade therefore points at the first
real disagreement at or before word 116644, and the two reported `Equal` width
disagreements are the honest leads.

**Correction to my own first guess, checked against the engine.** I wrote that `Equal`'s
*declared operand count* was wrong. It is not:

```
Script.cpp:363   FunctionInfo("Equal", 2)
case FUNC_EQUAL: operands[0] ... operands[1]
```

Two declared, two used. So the disagreement is narrower and stranger than a wrong count.
Engine and walker agree on the *tags* - both say `[1, 2]` - and disagree only on the
*width*:

```
tags [1,2]   walker 5 words   engine 7 words    (walker short by 2)
tags [1,1]   walker 8 words   engine 7 words    (walker over by 1)
```

Two words short one way and one over the other, from the same opcode, is not a single
missing or extra word. It points at the **per-tag width rules**: a `VAR` operand costs a
selector word plus its value, a string constant costs `3 + len // 4`, and an array selector
is always 3 words regardless of length. If the walker and the engine classify tag 1 or tag 2
differently, both directions of error follow from one mistake.

That is the specific thing to check first, and it is only findable because the checker
now counts what it cannot explain instead of discarding it - it had reported 100% by
construction for the entire project.

## The full sweep: both games, every regular stage

```
Sonic 1  (19 stages)   1,258,761 instructions executed
                      32,595 sites placed, 32,593 confirmed (100.0%)
                         257 not placed  - all of them Marble Zone Act 2

Sonic 2  (21 stages)   1,600,291 instructions executed
                      40,461 sites placed, 40,461 confirmed (100.0%)
                          0 not placed, 0 wrong, 0 opcode differences, 0 undecodable

combined              2,859,052 instructions, 73,056 distinct sites, 73,054 confirmed
```

**Sonic 2 is clean across all 21 regular stages.** Not "100% of what it looked at" - zero
sites dropped, zero disagreements, zero undecodable, on every stage.

That asymmetry is itself informative. Sonic 2's types and functions are the ones listed
first in the merged table, so they keep their own numbering: typeBase and functionBase are
both 0 for them. Sonic 1 is the only game carrying a non-zero base, and Sonic 1 is the only
game with a finding. The merged-numbering work is where the remaining uncertainty lives,
which is what you would hope and not something the data was guaranteed to show.

For scale: the first sweep of Sonic 1's same 19 stages managed 120,462 instructions and
froze at 190 distinct sites per stage. The ceiling was never the bytecode - it was the
cost of writing the trace, one line per operand tag instead of one per instruction.

### Narrowed: it is the `SCRIPTVAR_VAR` width, and the formulas both check out

Working the numbers rather than guessing. The walker's rules (`rsdkv4_walk.py:112`) are:

| operand | words |
|---|---|
| `VAR` + `VARARR_NONE` | 3 - tag, selector, variable index |
| `VAR` + `VARARR_ARRAY` / `ENTNOPLUS1` / `ENTNOMINUS1` | 5 - plus flag and index |
| `INTCONST` | 2 |
| `STRCONST` | `3 + len // 4` |

and the engine's fetch loop (`Script.cpp:3639-3668`) consumes exactly those words.
`ARRAY_KINDS = (1, 2, 3)` in the walker also matches
`enum ScriptVarArrTypes { NONE = 0, ARRAY = 1, ENTNOPLUS1 = 2, ENTNOMINUS1 = 3 }`, and the
engine does read a flag and an index word for all three of 1, 2 and 3.

So the arithmetic is right on both sides, and the reported widths still differ:

```
tags [1,2]   walker 5 = 3 + 2      engine 7 = 5 + 2
tags [1,1]   walker 8 = 3 + 5      engine 7
```

Two things follow. First, for `[1,2]` the engine took the five-word branch on operand 0
and the walker took the three-word branch - but they read the same tag byte and so must
read the same selector byte, which rules out the obvious explanation. Second, `7` is not
expressible as a sum of two `VAR` widths at all (3+3, 3+5, 5+5, 5+3 = 6, 8 or 10), so the
second case is not a mis-sized `VAR` either - something else about that instruction is
being read differently.

Both of those say the same thing: stop reasoning about the format and look at the words.
`MZS1.bin` word 121077 and 126301, with the tag byte and selector byte printed, will say
in one look what three rounds of reading have not. That is the next step, and it is cheap.

### The walker was right all along; the bug was in my trace

Three diagnoses in a row, all wrong, and the real cause was the instrumentation I had
written myself.

**What the checker reported.** Marble Zone Act 2: 2 sites where the walker and the engine
disagreed about operand width, and 227 more where they read different opcodes.

**First guess, wrong.** `Equal`'s declared operand count. Checked: `FunctionInfo("Equal", 2)`
and the handler uses `operands[0]` and `operands[1]`. Two declared, two used.

**Second guess, wrong.** The per-tag width rules. Checked: the walker's 3 words for `VAR`
and 5 for an array-selected `VAR` match `Script.cpp:3646-3672` exactly, `ARRAY_KINDS`
matches the enum, and the switch is only four cases plus a `default` that consumes
nothing.

**Third guess, wrong.** Placement. Checked: the engine's own `PLACE` line says
`MZS1.bin` starts at word 115,998 and `len(GlobalCode.bin)` is 115,998.

**The actual cause.** `ProcessScript` declares `int scriptCodeOffset = scriptCodePtr;` at
L3588, once per call, and never updates it per instruction. It is the *script's* entry
point. The original trace used it only for a word count, which happened to be harmless. When
I merged the trace into one line I also used it for the word index:

```c
PrintLog("ORACLE %s @%d consumed=%d tags=%s",
         functions[opcode].name, scriptCodeOffset - 1, ...);   // the script's entry!
```

So every instruction after the first in a script reported the same address, and `consumed`
was the distance travelled since the script began rather than the words that instruction
occupied. Fixed by recording the instruction's own start before the opcode is read:

```c
const int instructionStart = scriptCodePtr;
int opcode = scriptCode[scriptCodePtr++];
...
PrintLog(..., instructionStart, scriptCodePtr - instructionStart - 1, tagBuf);
```

The `- 1` excludes the opcode word, matching the walker, which counts operand words only.

Marble Zone Act 2, before and after:

```
before   placed 1847  confirmed 1845  WRONG 2  opcode differs 227  undecodable 30
after    placed 1754  confirmed 1754  WRONG 0  opcode differs   0  undecodable  0
```

Clean, and with no gaps of any kind.

**Why this is worth more than the bug.** The walker, the opcode table, the operand counts
and the stage placement were all correct the whole time, and the only defect was in the
measurement I built to check them. Every conclusion in this section above - about `Equal`,
about width rules, about placement - was a statement about my own instrumentation dressed
up as a statement about the bytecode. This is the fourth time here that a measurement
reported something false about the thing it was measuring, and the second time the
instrument was the fault.

The lesson generalises past this file: when a checker disagrees with code that has no
reason to be wrong, suspect the checker first, and check whether the disagreement is
*shaped* like a known artefact. 227 sites disagreeing about which opcode they are, in one
contiguous region, is not what a mis-sized operand looks like; it is what a lost offset
looks like.

## Architectural decision: one executable containing the compiled code of every game

Stated by the project owner, so it is recorded before any of it is built rather than
rediscovered later:

> one exe containing the entire compiled codebase

That is the **linking** route, not the hosting route. It rules out the alternative I had
been recommending - porting each game's data into RSDKv5U as separate `GameInfo`s - and it
rules out running AIR as a subprocess. Sonic 1, Sonic CD, Sonic 2 and Sonic 3 A.I.R. must
end up as compiled code inside `SonicHybridUltimate.exe`.

**Why it is the hard option, stated once so it is not rediscovered as a surprise.** RSDKv4
and Oxygen are both complete engines. Each has its own `main()`, renderer, audio device,
input handling, and a broad set of file-scope globals. Linking them into one binary means
resolving all of that:

- two `main()` symbols - one must become a renamed entry point called from ours
- two graphics backends, both wanting to own the window
- two audio stacks, both wanting the output device
- overlapping global symbol names, which is the tedious half and the part most likely to
  produce silent breakage rather than a link error

None of that is a reason to refuse. It is a reason to expect it to take real time, and to
build it in an order where each step is verifiable on its own.

**Consequences that follow, and are accepted:**

1. **The build must stay offline-capable.** `build_all.ps1` re-bootstraps vcpkg over the
   network and fails without it. Both engines have to build from what is already here.
2. **`Hybrid-RSDK-Main/OxygenWrapper.cpp` stops being a stub reporter.** It is currently
   the only thing in this codebase that names AIR, and it exists to say AIR is unavailable.
   It becomes the seam where AIR is called, or it is deleted in favour of a real one.
3. **Game data still never enters git.** Sonic 3's ROM is on this machine
   (`school\N\Sonic_Knuckles_wSonic3.bin`, byte-identical to `rsdk-source-data\sonic3.bin`).
   Linking AIR in does not change that; the ROM is unpacked into data at runtime and that
   data stays untracked.

**The order that works, and why it is this order:**

1. Build AIR's externals (SDL, zlib, ogg-vorbis, curl, imgui) and then AIR itself, to a
   standalone `sonic3air.exe`. Not a detour: it is the only proof the vendored 296 MB of
   source compiles at all, and it gives a working Sonic 3 to diff against.
2. Rebuild AIR as a static library with its `main()` renamed and its window/audio entry
   points exposed. This is where the symbol collisions surface, one at a time, as link
   errors rather than as a mystery at runtime.
3. Link that into `rsdkv4.vcxproj` and have our `main()` be the one that runs.
4. Resolve renderer and audio ownership, one subsystem at a time, verifying after each.

Each step produces something runnable, which is the only way this stays debuggable. Doing
it as one step produces a binary that neither game starts in.

**State of the source, corrected.** `vendor/sonic3air` is a pinned submodule at `b584686f`
(`v22.09.10.0-stable-933-gb584686f`), 11,563 files, 296 MB, with `framework`, `librmx` and
`Oxygen`. `vendor/theoraplay` is present for the media backend. This was available the whole
time and was missed: I read `Hybrid-RSDK-Main/Sonic 3 AIR Main`, an 8-file skeleton of
leftovers (two SDL2 makefiles, one stray `DebugTracking.cpp`, three boost debug headers, an
8 MB Discord `.dylib`), and reported from it that there was no AIR source and nothing to
add as a submodule. Both claims were false. `.gitmodules` had the answer.

## Sonic 3 A.I.R. builds from the vendored source

The first step of the linking route is done, and it was never actually blocked.

```
Oxygen\sonic3air\bin\Release_x64\Sonic3AIR.exe      7,383,040 bytes
Oxygen\sonic3air\bin\Release_x64\Sonic3AIR.lib        188,544
Oxygen\lemonscript\lib\x64\lemonscript.lib        50,942,714
```

Built Release|x64 from `Oxygen\sonic3air\build\_vstudio\sonic3air.sln`, after
`framework\external\build_externals_windows.bat` produced the SDL, zlib, ogg-vorbis, curl
and imgui libraries it needs. Exit 0, no errors. The dependency chain is

```
librmx.sln  ->  lemonscript.sln  ->  oxygenengine.sln  ->  sonic3air.sln
```

**`Sonic3AIR.lib` existing is the useful part.** It means AIR already compiles to a static
library, which is the shape the linking route needs. Step 2 becomes: expose its entry
points, keep `Sonic3AIR.exe` working as the reference build to diff behaviour against, and
link the library into `rsdkv4.vcxproj`. Nothing about the decision has to be re-litigated
and nothing needs the library rebuilt from scratch to try it.

**Two corrections to what I told you earlier, because both were wrong and both cost time.**

I said Sonic 3 needed `sonic3air.exe` from you and could not be built here. It could: the
source has been vendored at `vendor/sonic3air`, pinned to `b584686f`, since before this
session.

I then said there was no AIR source and nothing to add as a submodule. Also wrong, from the
same root cause both times - I read `Hybrid-RSDK-Main/Sonic 3 AIR Main`, an 8-file skeleton
of leftovers, instead of `vendor/`. `.gitmodules` had the answer and I did not read it.
That folder is now deleted, which is safe because nothing referenced it, but I deleted it
before checking that and should not have.

**What is still true:** the ROM is game data and never enters git. It is on this machine at
`school\N\Sonic_Knuckles_wSonic3.bin`. AIR unpacks it into data at runtime; that data stays
untracked, like every other game's assets in this project.

## What the "og hybrid" folder actually contains

`C:\Users\charl\Documents\Sonic Hybrid Manual\RSDKs` was named as the thing to
reference. It is **not a repository and contains no source** - no `.git`, no `.cpp`, no
`.vcxproj`, nothing to build. It is four data packs:

```
Sonic 1.rsdk      38,198,396
Sonic 2.rsdk      46,736,289
Sonic 3.rsdk      18,190,008
Sonic CD.rsdk     78,710,917
```

So the original hybrid was a **data-level** hybrid: four packs, one per game. Nothing in
that folder shows how games were combined at the code level, because there is no code in
it. There is no prior art here for the thing we are actually being asked to do.

### The three packs are three different things

First bytes of each, which is enough to classify them:

```
Sonic 1.rsdk    52 53 44 4b 76 42 59 01   "RSDKvB" + 0x0159 = 345 entries
Sonic 2.rsdk    52 53 44 4b 76 42 d6 01   "RSDKvB" + 0x01d6 = 470 entries
Sonic 3.rsdk    52 53 44 4b 76 35 c7 04   "RSDKv5" + 0x04c7 = 1223 entries
Sonic CD.rsdk   0d 0a 00 00 74 00 ...     no recognised signature
```

Three findings, in order of how much they matter:

**1. `Sonic 3.rsdk` is an RSDKv5 pack, not RSDKv4.** The original hybrid was already
running its Sonic 3 through a *different engine version* from its Sonic 1 and 2. That is
consistent with Sonic 3 A.I.R. being a separate engine rather than an RSDKv4 game, and it
means the original hybrid never had all four games on one engine. Whatever "hybrid" meant
there, it did not include compiling them together.

**2. `Sonic CD.rsdk` is not a pack this project can read.** No `RSDKvB` header, and no
`RSDKvB` signature anywhere inside it despite 78 MB of content. Whole-file gzip, zlib, raw
deflate, lzma and bz2 all fail. Magic-byte counts for gzip/zlib/png inside it are at
background-noise levels for arbitrary data. Its structure looks like fixed-stride records
of high-bit-set bytes. It is a binary blob in a format not identified here, and guessing
further is not worth the time.

**3. The two RSDKv4 packs have no stages and no per-zone bytecode.** Verified by probe,
not by eye - see below.

### How that was measured, and why it can be trusted

An `.rsdk` pack stores MD5 keys, not filenames, so its contents cannot be listed; you can
only ask whether a specific path is present. Guessing names one at a time is how you end
up concluding "the stages aren't in there" when you simply guessed the wrong folder, so
the guessing is done as a grid instead: `scripts/probe_pack.py` asks about specific paths,
`scripts/sweep_pack.py` walks 520 candidate layouts per pack and prints every hit.

Both were calibrated against `Hybrid-RSDK-Main/sonic-hybrid/Data.rsdk`, whose contents are
known because it is the pack this repo builds: all four anchors hit, including
`data/bytecode/GHZS1.bin` and `data/bytecode/GlobalCode.bin`. The tool finds things that
are there. Against the old packs:

```
Sonic 1.rsdk   HIT data/game/gameconfig.bin     HIT data/sprites/global/display.gif
               HIT data/animations/sonic.ani      HIT bytecode/globalcode.bin
               miss data/scripts/global/stagesetup.txt
               0 of 400 stage paths, 0 of 120 per-zone bytecode paths
Sonic 2.rsdk   identical pattern, 470 entries
```

Note the layout difference the sweep caught that single guesses had missed:
`data/bytecode/globalcode.bin` is **absent** while `bytecode/globalcode.bin` is present.
Bytecode sits at the pack root in these older packs, not under `data/`. Probing only the
modern layout would have reported "no bytecode at all".

So the old packs hold game assets - sprites, animations, game config, one global script -
and nothing else. They are partial.

### What this does and does not settle

**Settles:** the reference folder does not contain Sonic CD bytecode, so it does not
remove the need for an RSDKv3-to-RSDKv4 bytecode compiler. That work is still required and
is still the largest unstarted item. All 70 CD stages remain without
`Bytecode/Zone<CC><A><T>.bin`.

**Settles:** the original hybrid is not a template for the current requirement. The current
requirement is one executable containing the compiled code of all four games; the original
was four data packs, and for Sonic 3 it was a different engine version again. There is no
prior art to follow, which is worth knowing before anyone goes looking for it.

**Does not settle:** whether `Sonic CD.rsdk`'s blob is recoverable. Left alone deliberately
rather than guessed at.

**Usable:** `Sonic 3.rsdk` is 1223 RSDKv5 entries of Sonic 3 data, and A.I.R. reads RSDKv5
packs natively. It is the data source for Sonic 3 once the engine is linked in. Like every
other game's assets it stays untracked.

## What linking A.I.R. in actually requires, read from the source

The plan above says step2 is "resolve renderer and audio ownership one subsystem at a
time". Reading the code says the renderer and audio are the *easy* part, and that there is
a bigger problem behind them. Both are recorded here because the second one changes the
shape of the work.

### The good news: AIR is built to be hosted

`oxygen/application/EngineMain.h:29-79` declares `EngineDelegateInterface`, all pure
virtual, and it owns the two things that looked like the hard part:

```cpp
virtual const AppMetaData& getAppMetaData() = 0;
virtual GuiBase&  createGameApp()  = 0;      // the GUI backend
virtual AudioOutBase& createAudioOut() = 0;  // the audio backend
```

Window creation and audio output are injected, not hard-coded. A host supplies both. So
"two graphics backends fighting over the window" and "two audio stacks fighting over the
device" - the two obstacles I led with - are not forced on us. AIR was given an injection
point for exactly this.

`sonic3air/source/sonic3air/main.cpp:47-119` is correspondingly thin. It parses arguments,
calls `changeWorkingDirectory`, `randomize()`, then:

```cpp
EngineDelegate myDelegate;
EngineMain myMain(myDelegate, arguments);
myMain.execute();
```

That is the whole of it. There is no work in `main()` that a host could not do itself.

### The bad news: AIR is one-shot per process

```cpp
void EngineMain::execute()          // EngineMain.cpp:98
{
    if (startupEngine()) run();     // run() -> FTX::System->run(application)
    shutdown();
}

void EngineMain::shutdown()         // EngineMain.cpp:313
{
    ImGuiIntegration::shutdown();
    destroyWindow();
    mInternal.mVideoOut.shutdown();
    mAudioOut->shutdown();  SAFE_DELETE(mAudioOut);
    mDrawer.shutdown();
    FTX::Audio->exit();
    FTX::System->exit();
    FTX::JobManager->~JobManager();      // explicit destructor call
    Configuration::instance().saveSettings();
    oxygen::Logging::shutdown();
}
```

`execute()` returns, but only because the RMX application loop was told to quit - and by
then `shutdown()` has torn down process-global state: the audio system, the system
framework, the job manager (by explicitly calling its destructor rather than deleting it,
which is a strong hint it is not re-entrant), and logging. `EngineMain` is additionally a
`SingleInstance<EngineMain>`.

So **A.I.R. cannot currently be started, left, and re-entered within one process.** And
the requirement is that it must be: Sonic 1 to CD to 2 to 3 has to hand control back and
forth, or at minimum enter Sonic 3 after having run something else.

This is a bigger obstacle than symbol collisions and it is not visible from link errors.
It is the actual reason the work is hard, and it was worth finding before writing any
linker configuration.

### Retracted: "Sonic CD is blocked on missing scripts, not on a missing compiler"

**This section was wrong and is retracted.** It was committed as a correction to an earlier
claim, and in doing so it replaced a correct statement with an incorrect one. The original
framing was right: Sonic CD needs an RSDKv3-to-RSDKv4 bytecode converter.

**What it wrongly asserted.** That CD scripts were "entirely absent", that "there is no
`Data/Scripts` tree at all", that the missing `.bin` files were a symptom of absent scripts
rather than a conversion that never ran, and that this was "a data acquisition problem first
and a code problem second" which "no amount of work on the compiler" could unblock.

**What the evidence actually is.** The CD bytecode exists, in RSDKv3 form, in the repo:

```
Hybrid-RSDK-Main/rsdk-source-data/soniccd/Data/Scripts/ByteCode/
  RS*.bin  x70    2,182,615 bytes     <- exactly 70, matching the 70 CD stage folders
  PS*.bin  x9      97,829 bytes
  SS*.bin  x8     191,917 bytes
  GS*.bin  x1      48,104 bytes      <- global script
                                    88 files, 2,520,465 bytes total
```

None of them reach the output tree. `Hybrid-RSDK-Main/sonic-hybrid/Data/Bytecode/` holds 31
files, all Sonic 1 and Sonic 2 (`GHZS1.bin`, `EHZS2.bin`, `GlobalCode.bin`, the menu and
special-stage containers) - and not one `RS*`, `PS*`, `SS*` or `GS*`.

**Why the wrong conclusion was reached.** I searched the *output* tree
(`sonic-hybrid/Data/`) and generalised from its absence to the whole project's. The CD
scripts are in the *source* tree (`rsdk-source-data/soniccd/Data/`), which is a different
directory that I did not look in. I had `rsdk-source-data` listed in front of me - it holds
`sonic1.rsdk`, `sonic2.rsdk` and `soniccd.rsdk` - and still did not open it.

That is the same mistake as reading `Hybrid-RSDK-Main/Sonic 3 AIR Main` instead of `vendor/`,
and as computing a 512-byte header hash when the check uses a whole-file hash. Three times
this session: a confident answer generalised from one place I happened to look. The tell in
every case is the same - the answer was about "the project" when the evidence was about "one
folder".

**It also cost the user a question they were asked to answer.** The retracted section ended
by asking where the CD stage data came from and whether that source had the scripts, on the
grounds that the scripts could not be found here. They were in `rsdk-source-data`, and
`RS*.bin` x70 lines up with the 70 stage folders exactly.

**Two things that are correct and stay.**

The reference folder's packs are byte-identical to this repo's own source data, verified:

```
C:\...\Sonic Hybrid Manual\RSDKs\Sonic CD.rsdk
Hybrid-RSDK-Main\rsdk-source-data\soniccd.rsdk
  SHA256 58C179007B4584C3A94640FB4E072576D2135A24721EC1C5EB62C14BE613AA8C   (identical)
```

All four names and sizes match (`Sonic 1`/`sonic1` 38,198,396; `Sonic 2`/`sonic2` 46,736,289;
`Sonic 3` 18,190,008; CD 78,710,917). So the "og hybrid" folder contributes no data this
repository does not already have. That finding stands, and it is stronger than stated: it is
the same bytes, not merely equivalent content.

The CD pack is not an RSDKv4 pack, and now the reason is known rather than mysterious. It is
an **RSDKv3** pack. `Hybrid-RSDK-Main/SonicHybridRsdk.Generator` reads it through
`RsdkSonicCdImporter` and parses `soniccd/Data/Game/GameConfig.bin` with `GameConfigV3.Read`
(`Program.cs:152`). RSDKv3 uses a different archive format, which is why it has no `RSDKvB`
signature and why gzip/zlib/lzma/bz2 all fail on it. The earlier "unidentified binary blob,
left alone rather than guessed at" was a correct call on the evidence available; the evidence
was in the repository the whole time.

**What the CD work actually is, restated accurately.** Not data acquisition. A bytecode
converter from RSDKv3 to RSDKv4: 88 containers, opcode mapping plus operand re-encoding, with
the 70 `RS*` files lining up one-to-one against the 70 stage folders. Both halves of the
reference are already in the repo and already in the build:

- `Hybrid-RSDK-Main/RSDKV3/RSDKv3/Script.cpp` - the v3 operand encoding and the `FUNC_*`
  dispatch, 221 KB
- `rsdkv3_core`, an RSDKv3 static library target in `Hybrid-RSDK-Main/CMakeLists.txt:199`,
  built from that same source

So the CD item is a well-defined conversion with its specification sitting in-tree. It is
still the largest unstarted piece of work, but it is a compiler task, not a mystery.

### Correction: `SingleInstance` is not the obstacle I said it was

The section above says `EngineMain` is a `SingleInstance<EngineMain>` and lists that among
the reasons A.I.R. is one-shot. **That part is wrong**, and it is worth correcting before
anything gets built on it.

`librmx/source/rmxbase/data/SingleInstance.h:25-36`:

```cpp
protected:
    SingleInstance()
    {
        // TODO: Sanity check: (nullptr == mSingleInstance)
        mSingleInstance = static_cast<CLASS*>(this);
    }

    virtual ~SingleInstance()
    {
        // TODO: Sanity check: (mSingleInstance == this)
        mSingleInstance = nullptr;
    }
```

The sanity checks are `// TODO` comments and were never implemented. The constructor
unconditionally overwrites the pointer and the destructor unconditionally clears it. So it
is a lookup convenience, not a guard: constructing a second `EngineMain` after the first is
destroyed is perfectly legal and will work. The same is true of `Application`, which is also
a `SingleInstance<Application>` (`oxygen/application/Application.h:29`) - though that one is
irrelevant either way, because `run()` constructs a fresh `Application` on every call.

I read `SingleInstance<T>` as a runtime-enforced singleton because that is what the name
says. It is not one. Same shape of error as reading `Hybrid-RSDK-Main/Sonic 3 AIR Main` and
concluding there was no AIR source: trusting a name over the code under it.

### The real mechanism, which is narrower than stated

The one-shot behaviour is real, but it comes from one thing, not from a class hierarchy:
`EngineMain::shutdown()` performs the **process** teardown that the framework reserves for
exit, on every return from `execute()`.

`librmx/rmx_test/main.cpp:45-51` is the canonical one-shot sequence:

```cpp
FTX::System->initialize();
FTX::System->run<App>();
FTX::System->exit();
```

`exit()` there is the end of the process's framework lifetime. AIR's `shutdown()` calls
`FTX::System->exit()` itself (`EngineMain.cpp:332`), so every return from `execute()` ends
that lifetime. Symmetrically, `startupEngine()` calls `oxygen::Logging::startup(...)`
(`:245`) and `shutdown()` calls `oxygen::Logging::shutdown()` (`:338`) - a matched pair
bracketing one process lifetime, not one game session.

### Which means option B is smaller than I described

The change is not "make `EngineMain` non-singleton". It is: **do not run process teardown
when leaving a game.** Concretely, these lines in `shutdown()` are the ones doing process
tear-down rather than session teardown, and they are what has to move to real process exit:

```cpp
FTX::Audio->exit();                  // :332
FTX::System->exit();                 // :333
FTX::JobManager->~JobManager();      // :334
oxygen::Logging::shutdown();         // :338
```

Everything else in `shutdown()` - `ImGuiIntegration::shutdown()`, `destroyWindow()`,
`mVideoOut.shutdown()`, the audio-out object, `mDrawer.shutdown()`, saving settings - is
per-session state and can stay exactly as it is.

Two edges to watch when doing it, both flagged now so they are not discovered as surprises:

- `FTX::JobManager->~JobManager()` is an **explicit destructor call**, not a delete. If
  teardown is skipped, the job manager survives - fine. If it is ever reached twice, that is
  a double-destruction rather than a null check. This is the sharpest edge in the file.
- Skipping `destroyWindow()` vs not: the window *should* be destroyed per session, since
  AIR creates it in `createWindow()` (`:582`, private) and a second session has to be able to
  create one again. That path needs exercising once, not assumed.

This is still option B, still the option that fits, and it is a smaller and better-targeted
change than the section above implies. The failure-proving harness is still the right first
step - "enter A.I.R., leave it, enter it again, get a working second session" fails today
and would pass after this, and until it is observed failing for this reason there is no
evidence the diagnosis is right.

### Patch 0001 is written, builds, and the engine still runs

`patches/sonic3air/0001-restartable-engine.patch` (3,827 bytes) splits
`EngineMain::shutdown()` into per-session teardown plus a new
`EngineMain::shutdownProcess()`, and has the standalone `main.cpp` call the latter so
`Sonic3AIR.exe` keeps its behaviour. The change is 3 files, 35 insertions, 5 deletions.

The patch mechanism itself is verified end to end, not just written:

```
patch captured from the real diff              3,827 bytes
worktree reverted to pristine                  git -C vendor/sonic3air checkout -- .
apply_air_patches.py --check                   NOT applied        exit 0
apply_air_patches.py                           applied            exit 0
apply_air_patches.py  (again)                   skipped            exit 0
worktree diff after re-apply                    3 files, 35 insertions, 5 deletions
```

That last line is the one that matters: the patch reproduces the change exactly, and
re-running is a no-op. A patch file that had never been round-tripped through
revert-and-reapply would be an untested artefact that fails on someone else's machine.

**Build:** Release|x64, exit 0, no errors. `oxygen.lib` relinked, exe 7,383,040 ->
7,383,552 bytes.

**Runtime:** the patched exe boots and reaches `Ready to go` - ROM loaded, persistent data
loaded, scripts loaded, frames presenting. So the patch does not break startup.

### What patch 0001 is *not* verified as doing

Stated plainly, because the temptation is to read a green build as a finished change:

- **The new `shutdownProcess()` has never executed.** The engine was killed rather than
  allowed to exit cleanly, so the function the patch exists to create has not run once.
  Nothing yet demonstrates that AIR can be left and re-entered - only that it still starts.
- **No symbol-level proof the patch reached the binary.** Release/LTCG strips private
  symbols from both the exe and `oxygen.lib`, so `dumpbin /SYMBOLS` finds neither
  `shutdownProcess` nor `sProcessShutDown`. Two symbol greps returned "not found" here and
  both were the checker being wrong about a stripped Release build, not the patch missing.
  A Debug build would settle it if that level of proof is wanted.
- **Restartability is still a hypothesis.** The diagnosis is read from
  `shutdown()`/`startupEngine()` and is well-evidenced, but "enter AIR, leave it, enter it
  again, get a working second session" has not been attempted. It cannot be until the Hybrid
  can call `EngineMain` from a host, which is the next piece of work.

### Restartability is proven, in both directions

This is the result that justifies option B. It was measured twice - once with patch 0001
applied and once with it reverted - because a single green run only shows the test is
compatible with the code, not that the code was broken before.

`patches/sonic3air/0002-restart-selftest.patch` adds `-restartselftest`, which runs two
complete `EngineMain` sessions back to back in one process with no teardown between them,
each exiting deterministically via `mExitAfterScriptLoading` once scripts load. It needs no
window interaction and finishes in seconds.

**With 0001 applied - both sessions complete:**

```
=== RESTART SELFTEST: session 1 returned normally ===
=== RESTART SELFTEST: session 2 of 2 ===
=== RESTART SELFTEST: session 2 returned normally ===
=== RESTART SELFTEST: both sessions completed ===
System shutdown
exit code: 0
```

**With 0001 reverted (pristine `shutdown()`) - the process dies:**

```
--- SHUTDOWN ---
Simulation shutdown
System shutdown
exit code: -1073740940          (0xC0000374, STATUS_HEAP_CORRUPTION)
```

No second-session marker is emitted at all, and the reason is visible in the log: session
1's `shutdown()` ran `oxygen::Logging::shutdown()`, so by the time session 2 starts there is
no logging left to report anything with. Then it corrupts the heap.

That is the predicted failure, from the predicted cause. The double-destruction hazard
flagged when patch 0001 was written - `FTX::JobManager->~JobManager()` being an explicit
destructor call rather than a delete - is what a torn-down process-global state looks like
from the outside.

**So: the claim is no longer a hypothesis.** A.I.R. could not be left and re-entered, and
now it can. That was the gating unknown behind "one exe containing the compiled code of
every game", and it is now closed by 80 lines across 4 files.

**What this does not establish.** Both sessions here stop after script loading. This does
not show that a *played* Sonic 3 session can be exited and restarted - no gameplay state,
no save data written or reloaded, no window torn down and recreated under real conditions,
and no second real session rendering frames. Those are the next things to test, and the
window path is the one most likely to surprise: `createWindow()` is private to `EngineMain`
and is called per session, so a second window has to be created after the first was
destroyed, and that has not happened yet.

### A real Sonic 3 session can be exited and restarted

Patch 0002's test proved the *engine* survives re-entry, but each of its sessions stopped
the instant scripts loaded - no window, no rendered frame, no gameplay. That is a real gap:
most of what a session does was untested.

`patches/sonic3air/0003-restart-realsession.patch` counts real frames in
`EngineDelegate::onPostFrameUpdate()` and calls `FTX::System->quit()` at the limit, which is
the same path the window-close button uses. `-restartrealtest180` runs two full sessions of
180 frames each.

```
  31  Creating window...
 193  Ready to go
 195  RESTART REALTEST: reached frame 180, quitting this session
 199  --- SHUTDOWN ---
 203  RESTART REALTEST: session 1 returned normally
 205  RESTART REALTEST: session 2 of 2 starting
 267  Creating window...            <- second window, created fresh
 591  Ready to go
 595  RESTART REALTEST: reached frame 180, quitting this session
 603  --- SHUTDOWN ---
 611  RESTART REALTEST: session 2 returned normally
 615  RESTART REALTEST: both real sessions completed
 619  System shutdown
exit code: 0
```

**The window is destroyed and a second one created.** That was flagged as the most likely
thing to surprise, because `createWindow()` is private to `EngineMain` and runs per session.
It works.

The frame-limit message is checked deliberately. If it did not appear, both sessions would
still have exited 0 and the test would have passed without exercising anything - which is
the failure mode this whole exercise is guarding against.

### Re-entering A.I.R. duplicates its log output

Every line from session 2's window creation onward appears **twice**:

```
 267  Creating window...
 269  Creating window...
 591  Ready to go
 593  Ready to go
```

There is no third session - `session 3` appears nowhere in the log, and the duplicate starts
precisely where session 2 begins. Session 2 re-runs `oxygen::Logging::startup()` (called from
`startupEngine()`), which registers a second logging sink, so everything after is emitted
twice.

Cosmetic, but it will mislead anyone reading these logs, and it cost real time here: an early
count said three window creations and three shutdowns, which reads as three sessions until
you check line numbers. Anyone counting log lines to answer "how many sessions ran" is now
counting sinks, not sessions. Worth knowing before trusting any count from an A.I.R. log.

The clean fix is to shut logging down at the end of a session rather than at process exit,
which is the same split patch 0001 made for the other process globals. Not done yet - it is
a behaviour change to log ordering during teardown and it is not on the critical path.

### Three patches, each verified by round-trip

```
patches/sonic3air/0001-restartable-engine.patch    3,827 bytes   EngineMain.cpp/.h, main.cpp
patches/sonic3air/0002-restart-selftest.patch      2,844 bytes   GameArgumentsReader.h, main.cpp
patches/sonic3air/0003-restart-realsession.patch   5,135 bytes   EngineDelegate.cpp/.h,
                                                                GameArgumentsReader.h, main.cpp
```

Verified by reverting the submodule to pristine and replaying: `6 files, 157 insertions,
5 deletions`, and `apply_air_patches.py` run twice reports `skipped` the second time.

**Patch 0003 was truncated twice before being fixed, both times identically.** To isolate a
patch you must stage the baseline *first* and only then make the new edits. Staging
afterwards puts the new edits into the index, and `git diff` then reports a patch containing
only some of its files. 0003 first came out as 2 files instead of 4 - and it *applied
cleanly*, because the missing hunks were not hunks, they were whole edits to `main.cpp` and
`GameArgumentsReader.h` that had silently migrated into the index. A truncated patch is worse
than a broken one: broken is loud, truncated is silent.

Fixed by scripting the sequence (pristine, apply 0001+0002, stage, edit, diff) and asserting
the resulting file set rather than trusting it. Two smaller things the script had to handle:
the submodule files are CRLF, so LF anchors match nothing and rewriting the files to LF would
turn a 20-line change into an unreviewable whole-file diff; and an anchor must match exactly
once, not zero times.

**`apply_air_patches.py --reverse 0001` now reverts a single patch.** The gap caused the
manual `git apply` fiddling that caused the truncation, so it is fixed at the source rather
than worked around. An unmatched patch name is now an error, not a silent no-op.

**The failure message no longer guesses.** It used to say "the most likely cause is that the
submodule has moved off the pinned SHA", which was wrong the one time it fired - the cause was
a hand-edit outside the patch system. It now names the two things actually worth checking and
defers to what `git apply` reported.

### Two gaps in the patch tooling, found by using it

**`--reverse` reverts every patch, not one.** Reverting 0001 alone was wanted, to run the
falsification. It needs an `--only` / `--skip` option; until then, reverting a single patch
means `git -C vendor/sonic3air apply --reverse <absolute path>` by hand.

**`git -C <submodule> apply <relative-path>` silently resolves against the submodule**, not
the parent, so a path like `patches/sonic3air/0001-...patch` fails with "can't open patch"
even though the file exists relative to the current directory. Needs an absolute path.
Worth remembering because the failure looks like a missing file rather than a path
resolution difference.

**A failure message that was right for the wrong reason.** Reverting after hand-editing
`main.cpp` reported "1 patch(es) failed. The most likely cause is that the submodule has
moved off the pinned SHA" - which was not the cause. The tree had been deliberately
modified outside the patch system, so 0002 could not reverse. The message guessed and was
wrong, and "most likely" is doing real work in that sentence. It should name what it
actually checked.

### Sonic 3 A.I.R. runs, and the ROM is the right one

This is the thing that was reported blocked for most of the project. It is not.

`Sonic3AIR.exe` reached `Ready to go`:

```
Persistent data loading...  Simulation startup  Setup of EmulatorInterface
Loading scripts  Runtime environment ready  Adding game app instance
First present screen call  Ready to go
```

Getting there needed the ROM in the right place, and the search for that produced the
sharpest measurement error of the session - recorded below because the pattern is the
recurring one.

**Where the ROM goes:** `%APPDATA%\Sonic3AIR\Sonic_Knuckles_wSonic3.bin`.
`ResourcesCache.cpp:31-43` looks there first and is explicitly "where the ROM gets copied
to after it was found once". Later fallbacks are `config.mLastRomPath`,
`config.mRomPath`, the bare filename relative to the working directory, and finally a Steam
install search - which is where it ends up if none of the earlier ones hit, and which is why
a first run reports `Trying to find Steam ROM` and gives up. It never searched Steam
successfully and did not need to.

**The ROM is verified correct, and the check is a whole-file hash.** A.I.R. declares
`mRomCheck.mSize = 0x400000` and `mRomCheck.mChecksum = 0x344983ffcfeff8cb`
(`sonic3air/ConfigurationImpl.cpp:38-39`). Our ROM is exactly 0x400000 bytes, and its
MurmurHash2-64 over the **whole file** is `0x344983ffcfeff8cb` - an exact match.

**The error nearly made, and why it nearly recurred.** There are two different hashes in
this code and they are easy to confuse:

- `ResourcesCache::getHeaderChecksum()` - MurmurHash2-64 over the **first 512 bytes**
- `mRomCheck.mChecksum` - MurmurHash2-64 over the **whole file**

Computing the 512-byte header hash gives `0xf96026e52f80283e` against an expected
`0x344983ffcfeff8cb`, which reads as "this is the wrong ROM". It is not. The header hash
feeds `romInfo.mHeaderChecksum`, which `ConfigurationImpl` leaves at 0, so it is skipped
entirely; only the whole-file hash is actually enforced.

This is the third time this session that a wrong answer came from measuring the wrong
*scope* rather than from the code being wrong - after the 300-instruction trace cap and
after treating one shared `log.txt` as two runs. The header hash is even named
`getHeaderChecksum`, which is an accurate name describing an accurate function that answers
a different question from the one being asked of it. Compute the hash the check actually
performs before concluding the input is wrong.

**Game data stays untracked.** The ROM lives in `%APPDATA%`, outside the repository. It is
not in `vendor/sonic3air` and not in this repo. A copy was briefly placed next to the exe
while testing paths and has been removed, so there is exactly one documented location.

### The three ways out, and which one fits

**A. Run AIR's loop once and never leave it.** Every game has to run inside AIR's loop.
That discards RSDKv4 as an engine for the other three games and contradicts the
requirement that all four games' compiled code is in the executable and on equal terms.

**B. Make AIR restartable.** Split `shutdown()` into "tear down this game's state" and
"tear down the process", keep the second one at process exit only, and make `EngineMain`
constructible more than once. This is a small, bounded, readable change to vendored AIR -
a few dozen lines in one file plus the `SingleInstance` constraint - and it is verifiable:
"enter AIR, leave it, enter it again, get a working second session" is a testable claim
that fails today and would pass after the change. **This is the option that fits the
requirement.**

**C. One engine per thread.** Each loop runs forever; only the active one reads input and
presents. Heaviest option, and it does not avoid the ownership problem so much as move it
to the window, which SDL does not let two threads share cleanly.

Option B first, and the useful first step is not the change itself but a harness that
proves the failure: call `execute()` with an application that quits immediately, then try
to construct a second `EngineMain`, and record what breaks. A test that fails for the
stated reason is what makes the subsequent fix trustworthy - the alternative is editing
`shutdown()` and then discovering the second failure without knowing whether the fix
caused it.

Note the delegate work this implies. If AIR is to be entered and left repeatedly while
RSDKv4 keeps its own state, then the thing that decides *which game is running* has to sit
above both engines, and `EngineDelegateInterface` is already the interface that would
carry that decision. RSDKv4's `main()` currently does `Engine.Init(); Engine.Run();` with
no dispatch, so the dispatcher is new code, not a modification.

## The existing Sonic 3 integration was a no-op, and it was broken two ways

`Hybrid-RSDK-Main/CMakeLists.txt` already contained Sonic 3 A.I.R. integration - a
`BUILD_SONIC3AIR` option that `add_subdirectory`s A.I.R.'s own CMake and links it into the
`OxygenEngine` wrapper with `OXYGEN_EMBEDDED_MODE`. This was found only after building A.I.R.
by hand for days. It should have been the first thing looked at, and the option being `OFF`
on Windows with a comment saying A.I.R. "is currently only supported on Unix-like systems"
made it look like a known limitation rather than a wiring bug. It builds fine on Windows; that
comment is stale.

**Bug 1: the target name never existed.** The integration asked for `sonic3air`:

```cmake
if(TARGET sonic3air)
    target_link_libraries(OxygenEngine PRIVATE sonic3air)
    target_compile_definitions(OxygenEngine PRIVATE OXYGEN_EMBEDDED_MODE)
else()
    message(WARNING "sonic3air target not found after adding subdirectory")
endif()
```

A.I.R.'s `_cmake/CMakeLists.txt` defines these targets:

```
oggvorbis  minizip  imgui  rmxbase  rmxmedia  rmxext_oggvorbis
lemonscript  oxygen_netcore  oxygen        <- libraries
OxygenApp  OxygenServer  discord_game_sdk_source  Sonic3AIR   <- executables
```

The executable is **`Sonic3AIR`**. The lowercase name came from A.I.R.'s *Visual Studio*
project, `sonic3air.vcxproj`. CMake target names are case-sensitive, so `if(TARGET sonic3air)`
was never true. Setting `BUILD_SONIC3AIR=ON` printed that warning and did nothing else: no
link, no `OXYGEN_EMBEDDED_MODE`, no exe copy. Silent, and warning-shaped so it reads as
informational.

**Bug 2: wrong kind of target even with the right name.** `Sonic3AIR` is an *executable*, and
linking an `.exe` into a `.dll` is not something MSVC will do. The engine is the **`oxygen`**
library, and `Sonic3AIR` itself does `target_link_libraries(Sonic3AIR oxygen)` - that line is
the relationship to copy. So the fix links `oxygen`, not the game front-end.

Both corrected, plus the two `POST_BUILD` copy blocks retargeted from `sonic3air` to
`Sonic3AIR` and now guarded on `NOT TARGET oxygen`, since when the engine is embedded there is
no separate Sonic 3 process to ship and the two decisions cannot disagree.

Verified: `cmake -S . -B build` configures clean, exit 0, with `BUILD_SONIC3AIR` still `OFF`
so nothing else changed. Configure output also confirms `RSDKv3 sources found - building
rsdkv3_core`, so the RSDKv3 engine the CD converter needs is already a build target.

**What this does not do.** `BUILD_SONIC3AIR` is still `OFF` by default on Windows, and turning
it on is untested - A.I.R.'s CMake has never been configured through this project's build
tree, only built standalone via its own Visual Studio solution. Two things to watch when it is
switched on: A.I.R.'s CMake does `set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${WORKSPACE_DIR}/sonic3air")`,
which will contend with this project's `${CMAKE_BINARY_DIR}/bin` for output location, and
`OxygenWrapper.cpp` still has to be written to actually call into `oxygen` - the define and the
link are necessary but not sufficient.

### Defect four: `USE_IMGUI=OFF` is invalid on Windows, and it is an upstream bug

Patch 0004 cleared the `SDL/SDL.h` errors completely. The next failure was:

```
vendor/sonic3air/Oxygen/oxygenengine/source/oxygen/devmode/ImGuiDefinitions.h(21,10):
  error C1083: Cannot open include file: 'imgui.h'
```

`ImGuiDefinitions.h:14` reads:

```cpp
#if defined(PLATFORM_WINDOWS) || (defined(PLATFORM_LINUX) && defined(USE_IMGUI)) || \
    defined(PLATFORM_ANDROID) || defined(PLATFORM_MAC)
    #define SUPPORT_IMGUI
#endif
```

On Windows `SUPPORT_IMGUI` is defined **unconditionally** - `USE_IMGUI` is never consulted.
A.I.R.'s CMake, however, guards both the imgui include directory and the `imgui` target
behind `if (USE_IMGUI)`. So setting `USE_IMGUI=OFF` on Windows produces a build whose source
demands a header the build has not made available.

This is an inconsistency inside A.I.R. between its preprocessor condition and its own build
option, and this project cannot configure around it. `Hybrid-RSDK-Main/CMakeLists.txt` had
been forcing `USE_IMGUI OFF` "for production builds"; that force is removed, leaving A.I.R.'s
default (ON). The cost is one imgui library that `build_externals_windows.bat` already
produces anyway.

**Four defects, and they share a shape.** Each is an option or path A.I.R. supports on Linux
and that breaks on Windows, or in this host project specifically. None was hypothetical. The
stale "only supported on Unix-like systems" comment was not documenting a hard limitation -
it was four seams that nobody hit, because the integration had never once executed end to
end.

### Sonic CD's converter: the shape of the work, measured

`scripts/cd_opcode_map.py` extracts both opcode tables from the engines' own source and
reports what lines up. It exists because the two engines express opcodes completely
differently:

- **RSDKv3** (Sonic CD's format) declares sequential C enums, `enum ScrFunction` (134
  unconditional members) and `enum ScrVariable` (228). A `.bin` stores *numbers*, and those
  numbers mean whatever the declaration order says.
- **RSDKv4** identifies opcodes by **name** - a `FunctionInfo("Equal", 2)` table of 153
  entries, and a `variableNames[][0x20]` string table of 253.

So a numeric shift is impossible. It needs an explicit v3-order to v4-name table, and the
only trustworthy way to build one is to derive both halves from source.

**Functions map well. Variables do not map at all.**

```
ScrFunction:                     117/134 map by name        (87.3%)
ScrVariable (leaf match only):     5/228 map                ( 2.2%)
```

Two reasons, and the second is the expensive one:

1. **Naming differs semantically, not just in case.** v3's `VAR_TEMPVALUE0` corresponds to
   v4's `temp0`; `FUNC_SINCHANGE`/`FUNC_COSCHANGE` to `Sin`/`Cos`. Stripping the prefix and
   title-casing is a starting point, not the mapping.
2. **v4 variables carry scope and v3's do not.** 233 of v4's 253 variable names are
   scope-qualified - `object.xPos`, `engine.xPos`, `stage.xPos`, and 15 more across `camera`,
   `music`, `screen`, `keyPress`, `scene3D` and so on. v3's enum is flat, and the bytecode
   does not record which scope an operand belongs to.

**So the cost is not a mechanical remap.** It is roughly 228 hand-mapped variables, each
needing a scope chosen deliberately, plus about 17 function decisions. The function side -
which looked like the hard half - is nearly free; the variable side cannot be automated at
all. Knowing that before writing the converter is worth more than the converter.

**The 17 unmapped functions are not all losses.** Several are online features that should not
be ported under this project's no-networking rule: `SETACHIEVEMENT`, `SETLEADERBOARD`,
`LOADONLINEMENU`, `ENGINECALLBACK`, `LOADVIDEO`, `NEXTVIDEOFRAME`. Others look like renames
(`ENDFUNCTION`, `PLAYSTAGESFX`, `LOADTEXTFONT`) or Sonic 1 player-object functions
(`PLAYEROBJECTCOLLISION`, `BINDPLAYERTOOBJECT`, `DRAWPLAYERANIMATION`). Each needs a decision,
not a lookup.

**One hazard the script makes explicit.** Some enum members sit inside `#if` blocks -
`FUNC_HAPTICEFFECT` and `VAR_ENGINEHAPTICSENABLED`, both under `RETRO_USE_HAPTICS`. A build
with haptics enabled inserts a member and shifts every later value. Which numbering the CD
`.bin` files actually use is therefore an empirical question about the bytes, not something
to read off a header. The converter must confirm opcode width and the first few values
against a real container before trusting any table derived from declaration order.

#### The container format is not specified in this repo, as far as I can tell

Checked, because the opcode work is worthless without knowing how a `.bin` is laid out:

- `RSDKv3/RSDKv3/Script.hpp:4` - `#define SCRIPTDATA_COUNT (0x40000)`, and
  `extern int scriptCode[SCRIPTDATA_COUNT]` at `:53`. So the in-memory representation is a
  flat `int` array, 256K entries.
- `RSDKv3/RSDKv3/Reader.cpp:54-78` is the **only** code in the whole decompilation that
  mentions a `ByteCode` container. It does not parse one. It tests for the existence of
  `Data/Scripts/ByteCode/GlobalCode.bin` (setting `BYTECODE_MOBILE`) and otherwise
  `Data/Scripts/ByteCode/GS000.bin` (setting `BYTECODE_PC`), then returns.
- No `fread` of a script container anywhere. The only `fRead` uses are `Ini.cpp:54` and the
  `fread`/`SDL_RWread` macro definitions in `Reader.hpp:9-28`.
- `BYTECODE_PC` and `BYTECODE_MOBILE` are **assigned in `Reader.cpp` and read nowhere.**

What that implies: `RSDKv3-Decompilation` appears to contain the **text-script compiler**
(`Script.cpp` compiles `.txt` into `scriptCode[]` at runtime - the `FUNC_*` dispatch and the
`SCRIPTVAR_*` operand encoding I quoted earlier are all part of that compiler) plus a
bytecode-mode *detector*, but **not** a bytecode container parser or interpreter.

Sonic CD ships `GS000.bin`, so its containers are the `BYTECODE_PC` variant.

**Stated with the uncertainty it deserves.** This is a negative result from searching one
tree. A bytecode interpreter may exist in the Sonic CD RSDKv3 *mod* rather than in the
engine decompilation - plausible, since "bytecode mode" reads like a distribution feature
rather than an engine one, and the engine's own build happily compiles scripts from text.
Before treating the container format as reverse-engineering work, the thing to check is
whether a Sonic CD RSDKv3 mod source exists anywhere, because that would carry both the
container format and the `.txt` scripts the bytecode was compiled from.

**If it does not, the CD cost goes up.** The estimate in the section above - roughly 228
hand-mapped variables and about 17 function decisions - assumed a container format to read.
Without one, add deriving the layout from the bytes: header shape, `int` versus packed
`uint16`, whether opcodes are 16- or 32-bit, and how operands are encoded. That is
tractable but it is a separate piece of work, and it should not be folded silently into the
"just write a converter" framing.

**A note on how this was nearly got wrong.** The first search for `fread`/`LoadFile` in
RSDKv3 returned *nothing*, which would have supported a much stronger claim. It returned
nothing because `Get-ChildItem -Include "*.cpp" -File` without `-Recurse` matches no files -
PowerShell requires `-Recurse` or a wildcard in the path for `-Include` to apply. The
"no file reads at all" result was the checker silently finding zero files, not the codebase
having none. Recomputed with `-Recurse`, `LoadFile` appears 10 times. This is the fourth
instance of the same failure this session, and the cheapest to avoid: a search returning
*zero* results is nearly always the query, not the corpus.

**Two bugs this script had first, both the same mistake.** It initially looked for
`enum ScriptVar` - the real name is `ScrVariable` - and reported `0 unconditional members`,
which reads as "Sonic CD has no variables" rather than "the parser looked in the wrong
place". It then compared those variables against the `FunctionInfo` *function* table and
reported `0/228 mapped`, which again reads as a fact about the data rather than about the
checker. Both were the checker being wrong. Third time this session, after the header-vs-
whole-file hash and after the CD source-tree search.

### Three defects stood between `BUILD_SONIC3AIR=ON` and a working link

Enabling the option exposed three separate faults, each of which alone would have stopped
it. All three were in place before this session and none was visible without trying.

**1. The target name and kind (`Hybrid-RSDK-Main/CMakeLists.txt`).** Described above:
`if(TARGET sonic3air)` is never true, and `Sonic3AIR` is an executable that could not be
linked into a DLL even if the name matched. Now `if(TARGET oxygen)`.

**2. Bare `cl.exe` poisoning nested `project()` calls.** `Hybrid-RSDK-Main/CMakeLists.txt`
set `CMAKE_C_COMPILER`/`CMAKE_CXX_COMPILER` to `"cl.exe"` unconditionally. Those are
directory-scope *normal* variables, inherited by every `add_subdirectory()`, and A.I.R.'s
`project(Sonic3AIR)` then tried to validate a compiler that is not a full path:

```
CMake Error at vendor/sonic3air/Oxygen/sonic3air/build/_cmake/CMakeLists.txt:9 (project):
  The CMAKE_C_COMPILER: cl.exe is not a full path and was not found in the PATH.
```

Configure died before reaching any of this project's own logic. Putting `cl.exe` on `PATH`
does not help, because the value is validated as a cache entry needing a full path. Now
guarded by `NOT CMAKE_C_COMPILER`, so the Visual Studio generator's own choice stands.

**3. A.I.R.'s SDL include path does not exist on MSVC.** In
`vendor/sonic3air/Oxygen/sonic3air/build/_cmake/CMakeLists.txt`:

```cmake
include_directories(SDL/include)
add_subdirectory(${WORKSPACE_DIR}/framework/external/sdl/SDL2 SDL)
```

`SDL` is the *binary* directory argument to `add_subdirectory()`, not a source path. The
relative `SDL/include` resolves against `build/_cmake/`, where no `SDL` directory exists, so
the line adds nothing. Nothing then provides `<SDL/SDL.h>`:

```
rmxmedia_externals.h(35,12): error C1083: Cannot open include file: 'SDL/SDL.h'
```

This never appears upstream because `rmxmedia_externals.h:31-37` branches on compiler:
GCC/Linux asks for `<SDL2/SDL.h>`, which SDL2's own CMake exports, while MSVC asks for
`<SDL/SDL.h>`, which only A.I.R.'s bundled `framework/include` provides - and A.I.R.'s CMake
never adds it. So A.I.R. genuinely has never built on Windows through CMake, which is what
the stale "only supported on Unix-like systems" comment was half-remembering. Its
Visual Studio solution works because its `.vcxproj` sets the include path itself.

Fixed by `patches/sonic3air/0004-sdl-include-path.patch`, which adds
`include_directories(${WORKSPACE_DIR}/framework/include)` and explains the
binary-versus-source-directory confusion in a comment so it does not get "tidied" away.

**After all three, `BUILD_SONIC3AIR=ON` configures successfully:**

```
-- ✓ OxygenEngine will use embedded Sonic 3 AIR (linked 'oxygen')
-- Sonic 3 AIR integration complete
Configuring done (68.8s)
```

`OxygenEngine` now genuinely links `oxygen` with `OXYGEN_EMBEDDED_MODE` defined - a branch
that has never previously been reachable.

**Still unproven: that it links.** Configure resolving the target graph is not the same as
the linker accepting it, and the linker is where duplicate `main()` symbols, two SDL copies
and overlapping file-scope globals actually surface. That is the next measurement, and it is
the one that answers the question this whole line of work has been avoiding.

**The lesson, which is the fourth instance of it.** A feature directory named after the
feature (`Sonic 3 AIR Main`, `sonic-hybrid/Data/`, the `BUILD_SONIC3AIR` comment) was read
instead of the one the build uses (`vendor/sonic3air`, `rsdk-source-data/`, the actual CMake
target names). Each time the answer was confidently wrong in the same direction, and each time
the correct answer was sitting in a directory I had already listed.

## Patch 0008 is complete; the check that called it partial was itself wrong

A previous commit reported patch 0008 as partial, because its verification printed
`bare returns gone = False`. **That check was a substring search over the whole file and did
not consider scope.** The two remaining bare returns are at:

```
String.h:238   String&  operator=(const String& str)  { copy(str); return *this; }
String.h:269   WString& operator=(const WString& str) { copy(str); return *this; }
```

Both are in the **derived** classes, not in `StringTemplate<CHAR,CLASS>`. There `*this` really
is a `String&` / `WString&`, so `return *this` converts exactly and the code is correct as
written. Patch 0008 fixed the only two bare returns that were ill-formed - both inside the
template, where `CLASS` is an unresolved parameter.

So the patch is complete and the alarm was spurious.

**Recorded because it is the mirror image of the mistake this session has been fighting.** The
recurring error was measuring the wrong thing and reporting it as fact - a 512-byte hash when
the check uses a whole file, the output tree when the data was in the source tree, the Debug
condition block when the build was Release. Here it is a check too crude to see scope
manufacturing a defect that does not exist. Same underlying habit: trusting a check without
first confirming the check is sharper than the thing it is checking.

A verification added to catch a real class of bug is still a piece of code that can be wrong,
and "applied" from `apply_air_patches.py` has never meant "fixed the class of problem" -
only "this diff applied". The manifest's `must_contain` / `must_not_contain` assertions are
the part that is load-bearing, because those were written from the specific duplication that
actually happened.

**Live error, unchanged:**

```
String.h(73,48): error C3861: 'toUnicode': identifier not found
```

Lines 72 and 73 are `getUnicode(size_t)` and `getUnicode(int)`, both with the body
`return toUnicode(getChar(index));` - textually identical. `toUnicode` is presumably a free
function reached by argument-dependent lookup or declared per character type, so for
`CHAR = wchar_t` the expected overload may not exist. The next step is to read its
declaration. Inferring the rule from the diagnostic is what produced this session's run of
wrong answers, and the pattern across all three AIR header bugs has been consistent: latent
template code that A.I.R.'s own translation units never instantiate.

## Sonic 3 A.I.R. now compiles on Windows through this project's own build

`oxygen.lib` builds, Release|x64, and is 51,549,744 bytes. Every A.I.R. library builds:
`oxygen`, `oxygen_netcore`, `rmxbase`, `rmxmedia`, `rmxext_oggvorbis`, `lemonscript`,
`SDL2-static`, `imgui`, `zlibstatic`, `minizip`, `oggvorbis`. That is the thing the stale
"only supported on Unix-like systems" comment said could not be done, and it took five
defects to get there.

| # | defect | symptom |
|---|---|---|
| 1 | `if(TARGET sonic3air)` - wrong case, and it is an executable | never linked; warned, did nothing |
| 2 | bare `cl.exe` set as a directory variable | AIR's `project()` aborted before our logic ran |
| 3 | `include_directories(SDL/include)` - nonexistent path | `error C1083: 'SDL/SDL.h'` |
| 4 | `USE_IMGUI=OFF` forced, but AIR defines `SUPPORT_IMGUI` unconditionally on Windows | `error C1083: 'imgui.h'` |
| 5 | `target_link_libraries(rmxbase stdc++fs)` unguarded | `LNK1181: 'stdc++fs.lib'` - a GCC library |

Fixes: 1, 2 and 4 in `Hybrid-RSDK-Main/CMakeLists.txt`; 3 and 5 as
`patches/sonic3air/0004` and `0005`.

**Defects 3, 4 and 5 are all upstream A.I.R. inconsistencies** between what its source
assumes and what its own CMake provides on Windows. Defect 4 in particular is a genuine bug:
`ImGuiDefinitions.h:14` reads `defined(PLATFORM_WINDOWS) || (defined(PLATFORM_LINUX) &&
defined(USE_IMGUI)) || ...`, so `SUPPORT_IMGUI` is unconditional on Windows while the build
option is not.

### The first `OxygenEngine.dll` was hollow, and said nothing

It built. It was 16,384 bytes.

```
OxygenEngine.dll:     16,384 bytes
oxygen.lib:       51,549,744 bytes
```

`oxygen` is a **static library**, and a static library contributes only the object files
whose symbols are referenced. `OxygenWrapper.cpp` referenced none of them, so the link
succeeded, CMake printed `OxygenEngine will use embedded Sonic 3 AIR`, and the output
contained no A.I.R. whatsoever.

**So the symbol-collision question is still open**, after being called "the tedious half" for
most of the session. Two engines' file-scope globals, two SDL copies and two audio stacks
only collide once something pulls A.I.R.'s objects in. Four near-misses: the 16 KB DLL, then
the include path I miscounted, then my own header mistake, then the flag inheritance below.

`OxygenWrapper.cpp` is now a **probe** rather than a stub: it names `EngineMain`,
`EngineDelegateInterface` and `EngineDelegate`, takes the address of
`EngineMain::shutdownProcess`, and implements the full pure-virtual set so the vtable is
emitted. It does not pretend to be a working delegate - `createGameApp()` and
`createAudioOut()` reinterpret `this`, which is honest about being a probe rather than
quietly returning a bad reference that would crash later.

### The first real Windows-versus-A.I.R. collision: the `ERROR` macro

Compiling A.I.R.'s headers from a translation unit that included `<windows.h>` first
produced a cascade of syntax errors pointing at A.I.R.'s source and never mentioning macros:

```
librmx/source/rmxbase/base/ErrorHandler.h(63,3): error C2143: syntax error: missing '}' before 'constant'
```

`ErrorHandler.h:59-64` is:

```cpp
enum class ErrorSeverity { INFO, WARNING, ERROR };
```

`windows.h` defines `ERROR` as a macro. So the enumerator expanded to `0` and the enum would
not parse. Fixed by including A.I.R.'s headers first and defining `EXPORT` without
`<windows.h>` at all.

This is the class of problem that was predicted and is now observed rather than assumed -
except it is a **preprocessor** collision, not a link-time one, and it surfaces in whichever
file includes things in the wrong order. Worth remembering for the Custom Client and any
future host translation unit.

### A.I.R.'s headers only compile inside A.I.R.'s own CMake scope

The probe now fails at:

```
librmx/source/rmxbase/memory/StringImpl.h(765,23): error C3867:
  'std::basic_string_view<char,...>::data': non-standard syntax
```

`StringImpl.h:765` passes `str.data` where `str` is a `StdStringView`. C3867 is an MSVC
permissive-mode diagnostic that AIR's own build does not trip, because A.I.R. sets
directory-scope compile state that a host target does not inherit:

```
_cmake/CMakeLists.txt:56-63
  set(CMAKE_CXX_FLAGS_RELEASE "-O3")
  add_compile_options(-Wno-unused-function)
  add_compile_options(-Wno-stringop-overflow)
  add_compile_options(-Wno-psabi)
```

`OxygenEngine` is declared at `Hybrid-RSDK-Main/CMakeLists.txt:393`, and the
`add_subdirectory` that pulls A.I.R. in is at `:518`. CMake's directory-scope variables and
`add_compile_options` only affect targets created *after* them, so `OxygenEngine` is built
with different flags from every A.I.R. target, and A.I.R.'s headers do not survive that.

#### Release|x64 parsed properly: flags match after all, and the difference is includes

The section below retracts "compile flags eliminated" on the grounds that the comparison had
read the Debug condition block. Parsed properly - every `ItemDefinitionGroup` with its
`Condition`, keyed on `Release|x64` - flags **do** match:

```
LanguageStandard    rmxbase stdcpp17        OxygenEngine stdcpp17
AdditionalOptions   (none)                   (none)
defines             WIN32 _WINDOWS NDEBUG    WIN32 _WINDOWS NDEBUG
                    CMAKE_INTDIR="Release"    + OxygenEngine_EXPORTS
```

So that retraction was wrong in its conclusion, while being right that the original method was
unsound. Both readings agreed, for the uninteresting reason that these targets differ very
little at flag level. Recorded because "we differenced the wrong block and got the right
answer" is not a result anyone should have to re-derive.

**The real difference is the include list: 18 directories against 10.** `rmxbase` carries
several that `OxygenEngine` does not:

```
build/_cmake/SDL/include                      <- the nonexistent one, from AIR's bug
.../librmx/source/rmxmedia/_glew              <- bundled GLEW
.../Oxygen/oxygenserver/source
.../Oxygen/sonic3air/source/external
.../framework/external/ogg-vorbis/libogg/include
.../framework/external/ogg-vorbis/libvorbis/include
.../framework/external/ogg-vorbis/libvorbis/lib
.../framework/external/zlib/zlib/contrib/minizip
```

`OxygenEngine` instead picks up three the other lacks, all CMake-generated SDL exports from
`build/sonic3air`: `SDL/include`, `SDL/include/SDL2`, `SDL/include-config-release/SDL2`.

**The most promising lead in a while, and it is not about flags.** `StringImpl.h:765` is:

```cpp
TEMPLATE bool STRING::endsWith(StdStringView str) const
{
    return includesAt(str.data, (int)mLength - str.length());
}
```

which passes the member *function* `str.data` where a pointer is wanted. It compiles in
`rmxbase` and not here, and the plausible explanation is no longer anything about this
project's build - it is that line 765 needs a declaration from an include directory only
`rmxbase` has. `rmxmedia/_glew` is the likelier candidate of the two plausible ones, because
`rmxbase.h` pulls rendering types through GLEW.

**The fix is to link `rmxbase`, not to transcribe its includes.** `OxygenEngine` should get its
include list from the library whose headers it is including, via
`INTERFACE_INCLUDE_DIRECTORIES`. Hand-copying directories is how the current list got to be
wrong, and it would drift again the moment AIR reorganises. Linking `rmxbase` makes the list
correct by construction.

#### Ruled out: SDL include shadowing, and /external:I

Two hypotheses for the `StringImpl.h` failure above, both checked, neither the cause:

```
vcpkg_installed/x64-windows/include/SDL2   79 files, including SDL.h
vcpkg_installed/x64-windows/include/SDL    ABSENT
vendor/sonic3air/framework/include/sdl/    AIR's bundled copy
```

`rmxmedia_externals.h` asks for `<SDL2/SDL.h>` on GCC and `<SDL/SDL.h>` on MSVC.
vcpkg supplies the first spelling and has no `SDL` directory at all, so the second can only
resolve to A.I.R.'s bundled copy. **Nothing shadows anything** - the two layouts are
complementary, not competing.

And `/external:I`, which both `rmxbase.vcxproj` and `OxygenEngine.vcxproj` carry, marks
vcpkg's headers external. `StringImpl.h` is A.I.R.'s own, not vcpkg's, so external-header
treatment cannot be raising a diagnostic inside it.

Both `LanguageStandard` values are `stdcpp17`. The remaining difference is not in the flags
at all, which points at include *ordering*: A.I.R.'s own translation units include an
umbrella header before `StringImpl.h` and the probe does not. The next thing to try is having
`OxygenWrapper.cpp` include `rmxbase.h` first, as A.I.R.'s sources do, and only then
`EngineMain.h`.

**The fix is ordering, not flags**: declare `OxygenEngine` after the `add_subdirectory`, or
re-apply A.I.R.'s compile options to it explicitly. Ordering is better because it stays
correct as A.I.R. changes. This is the next thing to do, and it is expected to be the last
thing between "A.I.R.'s headers compile in our target" and a real link.

**Once that links, the collision count is finally knowable** - and it will be a real number
rather than an assumption, which is the only reason any of this was worth doing carefully.

## Reached the linker: every A.I.R. library compiles with all nine patches applied

With 0001-0008 and 0011 all genuinely in place - restored, chain-correct, and verified to
contain everything the replaced versions did - the `OxygenEngine` target compiles every A.I.R.
library. `rmxbase`, `rmxmedia`, `rmxext_oggvorbis`, `lemonscript`, `oxygen_netcore` and
`oxygen` all build, along with SDL2-static, imgui, zlibstatic, minizip and oggvorbis.

That clears the entire compile-time phase of the integration. The `String.h` and
`StringImpl.h` chain - six separate latent defects across two patches plus the removals - holds
through A.I.R.'s own recompile, not just through ours.

The remaining failure is at the **link**, and it is one library:

```
LINK : fatal error LNK1104: cannot open file 'libcurl.lib'
    [build\Hybrid-RSDK-Main\OxygenEngine.vcxproj]
```

**This is the first time the integration has reached the linker at all**, so it is worth being
precise about what it means and does not mean. It means A.I.R.'s headers compile inside this
project's translation unit and every one of its libraries builds here. It does **not** yet mean
the two engines coexist - that is the next link, and the answer to the symbol-collision question
is still one step away rather than in hand.

### Why curl specifically

A.I.R.'s own CMake handles it at `_cmake/CMakeLists.txt:303-304`:

```cmake
find_package(CURL REQUIRED)
target_link_libraries(oxygen CURL::libcurl)
```

So `oxygen` is built against curl, and any DLL linking `oxygen` must resolve curl's symbols
too. `build/lib/Release/` contains **no** curl library, which confirms CMake did not build
curl as part of this project - `find_package` located a prebuilt one instead. The prebuilt
libraries that do exist are all under the vendored tree:

```
vendor/sonic3air/framework/lib/x64/curl/libcurl.lib    8,979,896
vendor/sonic3air/framework/lib/x64d/curl/libcurl.lib   8,979,896
vendor/sonic3air/framework/lib/x86/curl/libcurl.lib    8,691,136
```

built earlier by `framework/external/build_externals_windows.bat`. `find_package(CURL)`
succeeded - otherwise configure would have failed - but the resulting `CURL::libcurl` location
is not being passed through to the `OxygenEngine` link line.

**The fix is to hand `CURL::libcurl` to the DLL explicitly**, so it inherits what `oxygen`
already resolved rather than relying on `find_package` to rediscover it:

```cmake
if(TARGET CURL::libcurl)
    target_link_libraries(OxygenEngine PRIVATE CURL::libcurl)
endif()
```

If that is not enough, set `CURL_LIBRARY` and `CURL_INCLUDE_DIR` to the vendored
`framework/lib/x64/curl` before the `add_subdirectory`, so `find_package` resolves to the copy
this project actually built. The second is the more robust of the two, because it fixes the
cause rather than propagating a resolution that is already fragile.

## How the SDL, GLEW and CRT problems can each be resolved

Not library-versus-library for all three. They are structurally different problems and need
different fixes.

### SDL: two real libraries, so this is ordinary link-time unification

```
project side   vcpkg shared SDL2, via find_package(SDL2 CONFIG REQUIRED) + SDL2::SDL2 on rsdk_core
A.I.R. side    SDL2-static.lib, built by A.I.R.'s own
               add_subdirectory(${WORKSPACE_DIR}/framework/external/sdl/SDL2 SDL)
               with SDL_STATIC ON / SDL_SHARED OFF, forced there by Hybrid-RSDK-Main
```

Neither carries an embedded `/DEFAULTLIB` for SDL - the directive dump listed no SDL entry -
so it is a plain CMake link item on `rmxmedia` and can be swapped for `SDL2::SDL2`.

**Direction of travel is dictated by A.I.R.'s CMake, not by preference.** With
`BUILD_SDL_STATIC ON` it always builds its own copy, and the `OFF` branch only calls
`pkg_check_modules(SDL2 ...)` on ARM/Linux, so there is no Windows path where A.I.R. adopts an
external SDL. Making this project use `SDL2-static` is therefore the tractable direction;
`rsdk_core` only uses SDL2's public API, so it should link against A.I.R.'s copy unchanged.
That is 63 of the 66 link errors, and it is mechanical.

### GLEW: not two libraries - A.I.R.'s copy is compiled into rmxmedia

```
_cmake/CMakeLists.txt:131   include_directories(.../rmxmedia/_glew)
_cmake/CMakeLists.txt:217   ${WORKSPACE_DIR}/librmx/source/rmxmedia/_glew/*.c
```

`rmxmedia.lib` contains `glew.obj`. It cannot be dropped by changing link order, because the
symbols are part of a library needed for everything else. Only two ways out:

- Stop compiling `_glew/*.c` into `rmxmedia`. That is a patch to A.I.R.'s CMake, and it needs
  a judgement about whether A.I.R.'s bundled GLEW is ABI- and header-compatible with vcpkg's
  `glew32`.
- Drop vcpkg's `glew32` and let `rmxmedia`'s bundled GLEW satisfy both engines, which requires
  `rsdk_core` to have been compiled against matching GLEW headers.

Worth knowing that `_cmake:284` already flags a GLEW versus ImGui OpenGL header conflict in
A.I.R.'s own build, so this area has a history. This one needs a compatibility decision, not
a flag.

### The LNK4098 warning was a misdiagnosis: both sides use the same CRT

An earlier version of this section claimed A.I.R. built against the static CRT while this
project used the dynamic one, and called that the most dangerous of the three problems. **That
was wrong, and it was checked after being written rather than before.**

Verified by dumping the CRT directives from every library in the link:

```
imgui.lib           MSVCRT          oxygen.lib          msvcprt MSVCRT
lemonscript.lib     msvcprt MSVCRT  oxygen_netcore.lib  msvcprt MSVCRT
minizip.lib         MSVCRT          rmxbase.lib         msvcprt MSVCRT
oggvorbis.lib       MSVCRT          rmxext_oggvorbis.lib msvcprt MSVCRT
oxygen.lib          msvcprt MSVCRT  rmxmedia.lib        msvcprt MSVCRT
rsdk_core.lib       msvcprt MSVCRT  rsdkv3_core.lib     msvcprt MSVCRT
sonic_hybrid.lib    msvcprt MSVCRT  SDL2-static.lib     MSVCRT
zlibstatic.lib      MSVCRT
```

Every library on both sides carries `msvcprt` + `MSVCRT` - the **dynamic** CRT, /MD. **No
library in the link carries `libcmt.lib`.** Neither `Hybrid-RSDK-Main/CMakeLists.txt` nor
A.I.R.'s `_cmake/CMakeLists.txt` sets `MSVC_RUNTIME_LIBRARY` at all, so both use the same
default. There is no CRT mismatch here, and the warning is not evidence of one.

The mistake was reading `warning LNK4098: defaultlib 'libcmt.lib' conflicts` and inferring
"static CRT somewhere" rather than dumping the directives and finding out. The same failure
shape as the rest of this session: a plausible mechanism inferred from a message, in place of
the measurement that would have answered it.

### What the warning actually was: debug vcpkg libraries in a Release link

```
vcpkg_installed\x64-windows\debug\lib\theora.lib
vcpkg_installed\x64-windows\debug\lib\theoradec.lib
```

Both appear in the **Release** `AdditionalDependencies`, sitting among correctly-Release
entries (`ogg.lib`, `vorbis.lib`, `SDL2.lib`, `glew32.lib`). Release copies of both also exist
under `vcpkg_installed\x64-windows\lib\`, so the debug tree is being reached when it need
not be.

The cause is two mechanisms racing at `Hybrid-RSDK-Main/CMakeLists.txt`:

```cmake
find_library(THEORA_LIBRARY NAMES theora libtheora REQUIRED)      # :56
find_library(THEORADEC_LIBRARY NAMES theoradec libtheoradec REQUIRED)  # :57
pkg_check_modules(THEORA REQUIRED theora theoradec)              # :68
```

`find_library` populates `${THEORA_LIBRARY}` / `${THEORADEC_LIBRARY}`, which are consumed at
`:180-181`, while `pkg_check_modules` populates `${THEORA_LIBRARIES}`, consumed at `:200`.
Whichever resolution won produced the debug path. Worth noting the comment at `:53` - "Team
Forever requires ogg and theora for video playback" - so this was added deliberately.

**This is a pre-existing defect in this project's own RSDKv4 build, not an A.I.R. integration
problem.** Nothing in the hybrid linked both paths until today, so a Release build has
presumably been linking debug theora/theoradec - and debug/release CRT mixing - without a
failing build to reveal it. The `LNK4098` I attributed to A.I.R. was evidence of it.

That also revises the priority order below: this is not a runtime-correctness landmine
introduced by embedding A.I.R., it is a latent Release-configuration bug that the embedding
work made visible.

### Fixed and verified: debug theora in a Release build

`find_library` results are cached, so clearing them was part of the fix - otherwise the
previously-found debug path would simply have been reused.

```
before   THEORA_LIBRARY:FILEPATH=.../vcpkg_installed/x64-windows/debug/lib/theora.lib
after    THEORA_LIBRARY:FILEPATH=.../vcpkg_installed/x64-windows/lib/theora.lib
         THEORADEC_LIBRARY:FILEPATH=.../vcpkg_installed/x64-windows/lib/theoradec.lib

Release link, vcpkg entries with /debug/:  0   (was 2)
```

Verified by dumping the CRT directives of every library in the link first: all carry
`msvcprt` + `MSVCRT`, so both sides are /MD dynamic and there is no `libcmt.lib` anywhere.
The fix is constrained with `HINTS "${VCPKG_INSTALLED_DIR}/${VCPKG_TARGET_TRIPLET}/lib"
NO_DEFAULT_PATH`, matching how `GLEW` and `Vorbis` already resolve correctly through
`find_package` in config mode. Headers are left unconstrained because vcpkg shares one
`include/` tree between debug and release for a triplet.

### A side effect worth having: SDL duplication now fails at configure time

```
CMake Error: The INTERFACE_SDL2_SHARED property of "SDL2-static" does not exist.
```

That is the SDL collision, caught during configure rather than surfacing as 63 `LNK2005`
errors at link time. Same problem, detected a whole build phase earlier, with a message that
names the mechanism instead of listing 63 duplicate symbols. Not yet *solved* - the fix is
still to unify on A.I.R.'s SDL - but the diagnosis loop just got shorter.

### Recommended order

1. **theora/theoradec debug-in-Release** - a pre-existing defect in this project's own build,
   now visible. One of the two discovery mechanisms at `CMakeLists.txt:56-68` is resolving into
   the debug tree; decide which one should own this and drop the other.
2. **SDL** - 63 of 66 errors, mechanical, direction fixed by A.I.R.'s CMake
3. **GLEW** - needs a compatibility judgement, not a flag

The CRT is no longer on the list, because there is nothing to fix.

### What the next link will and will not tell us

The collision link stopped at the first multiply-defined set, so it never reached an audio
symbol. So the honest claim is that SDL and GLEW duplication is the whole of the *current*
collision set - not that the audio-stack question is settled. A clean link after step 1 and 2
may reveal more behind them.

## The AIR patch chain: true state, and why `--check` shows conflicts on a clean tree

`apply_air_patches.py --check` against a genuinely pristine submodule:

```
0001-restartable-engine.patch                  NOT applied
0002-restart-selftest.patch                    NOT applied
0003-restart-realsession.patch                 CONFLICT
0004-sdl-include-path.patch                    NOT applied
0005-stdcxxfs-guard.patch                      NOT applied
0006-endswith-data-call.patch                  NOT applied
0007-adddouble-duplicate-decl.patch            NOT applied
0008-operator-assign-dependent-cast.patch      NOT applied
0011-remove-dead-getunicode.patch              NOT applied
```

**0003 conflicting on a clean tree is correct and expected, not a defect.** It touches
`main.cpp` alongside 0001 and 0002, and was generated with both of those already applied as a
staged baseline - the only method that produced an isolated diff. Its context therefore
assumes they are present. The same is true of 0008 and 0011 on `String.h` after 0007.

**So the patches are a chain per file, not an independent set.** Three consequences worth
stating plainly, because each cost time:

1. `git apply --reverse --check` and `git apply --check` both fail for a non-first patch in a
   file's chain. That is a conflict, not "already applied" and not "not applied".
2. Each patch must be generated with **every lower-numbered patch staged as the baseline** -
   not just the immediately preceding one, and not just the patches touching that file.
3. Regenerating one patch requires re-staging the baseline *before* making the edit. Doing it
   afterwards stages the edit too and `git diff` comes back empty, which is what happened on
   the first attempt at this.

**A `git` gotcha that produced a genuinely misleading state.** `git checkout -- .` reverts the
worktree *from the index*, it does not reset the index. A previous run had staged a baseline,
so after `checkout -- .` the tree still reported 0007 and 0008 as applied, from a pristine
`HEAD`. The correct sequence is:

```
git -C vendor/sonic3air reset -q      # index back to HEAD first
git -C vendor/sonic3air checkout -- .  # then worktree back to index
```

Without the reset, "reverted" means nothing. This is the fourth distinct way a green result
has turned out to describe something other than the worktree, after a truncated patch, a
duplicated patch, and a mis-detected patch.

**What remains**, in order:

- Regenerate 0003, 0008 and 0011 with the full lower-numbered baseline staged, then confirm
  `--check` reports `applied` for all nine from a clean tree.
- Re-run the `OxygenEngine` build. The last real build result was a failure in
  `rmxbase.vcxproj` at `FileHandle.h(27)`, which was traced to the retracted 0010 being
  re-applied by an unqualified `apply_air_patches.py` run - 0010's file has since been deleted,
  so that should no longer recur.
- Only then does the symbol-collision question get its first real answer.

## Next steps, in order of value

1. **Widen the oracle, now that it is trustworthy.** 4,157 distinct sites, 100% confirmed,
   zero gaps - on five stages and only what executes in 30 seconds of headless play. The
   static linear walk over every range in every container still agrees on only 81%, and
   branches needing player input or a boss trigger are still unexercised.
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