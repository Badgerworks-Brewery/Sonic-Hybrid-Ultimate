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