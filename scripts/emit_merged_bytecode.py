#!/usr/bin/env python3
"""Wire the merged GlobalCode.bin and both games' stage bytecode into the pack.

Called from the build between the C# generator and the packer. Kept in Python
because the container format lives in scripts/rsdkv4_bytecode_merger.py, and
duplicating a reverse-engineered format in two languages invites exactly the
kind of drift that produced the earlier fabricated-enum bug.

RSDKv4 decides text-or-bytecode per stage load on whether Bytecode/GlobalCode.bin
resolves (Scene.cpp:675), and it pairs config object entry i with script slot
i+1. So the merged container's script order must match the merged config's object
order: all of Sonic 2's globals, then all of Sonic 1's.
"""

import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rsdkv4_bytecode_merger import (JUMP_NONE, NONE, merge, parse,  # noqa: E402
                                    serialize)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Hybrid-RSDK-Main", "rsdk-source-data")
OUT = os.path.join(ROOT, "Hybrid-RSDK-Main", "sonic-hybrid", "Data", "Bytecode")

# Source zone file stem -> the folder name the merged stage list uses. Per-stage
# files need no merging: a stage's scripts are addressed by object index only that
# stage defines, so they simply coexist under their own names.
SONIC2_ZONES = [
    ("Zone01", "EHZS2"), ("Zone02", "CPZS2"), ("Zone03", "ARZS2"),
    ("Zone04", "CNZS2"), ("Zone05", "HTZS2"), ("Zone06", "MCZS2"),
    ("Zone07", "OOZS2"), ("Zone08", "HPZS2"), ("Zone09", "MPZS2"),
    ("Zone10", "SCZS2"), ("Zone11", "WFZS2"), ("Zone12", "DEZS2"),
    ("Title", "TitleS2"), ("LSelect", "LSelectS2"),
    ("Credits", "CreditsS2"), ("Ending", "EndingS2"),
    ("Continue", "ContinueS2"), ("Special", "SpecialS2"),
]

SONIC1_ZONES = [
    ("Zone01", "GHZS1"), ("Zone02", "MZS1"), ("Zone03", "SYZS1"),
    ("Zone04", "LZS1"), ("Zone05", "SZS1"), ("Zone06", "SBZS1"),
    ("Title", "TitleS1"), ("LSelect", "LSelectS1"),
    ("Credits", "CreditsS1"), ("Ending", "EndingS1"),
    ("Continue", "ContinueS1"), ("Special", "SpecialS1"),
]


def object_names(game):
    """The global object names a game lists, in config order.

    Located by anchoring on "Player Object", which is global object 0 in both
    games, then walking back to the count byte and parsing the table.
    """
    path = os.path.join(SRC, game, "Data", "Game", "GameConfig.bin")
    data = io.open(path, "rb").read()
    anchor = data.find(b"Player Object")
    if anchor < 0:
        raise ValueError("%s: could not find the object table" % game)

    for back in range(1, 8):
        count = data[anchor - back]
        if not 20 <= count <= 60:
            continue
        pos = anchor - back + 1
        try:
            names = []
            for _ in range(count):
                n = data[pos]; pos += 1
                names.append(data[pos:pos + n].decode("latin-1")); pos += n
            paths = []
            for _ in range(count):
                n = data[pos]; pos += 1
                paths.append(data[pos:pos + n].decode("latin-1")); pos += n
        except Exception:
            continue
        if sum(1 for x in paths if x.endswith(".txt")) >= len(paths) * 0.8:
            return names
    raise ValueError("%s: object table did not parse" % game)


def copy_stage_bytecode(game, zones, deltas, global_count):
    """Copy a game's per-stage containers, renumbering their absolute pointers.

    Per-stage pointers are absolute indices into the engine's *global*
    scriptCode/jumpTable arrays, built by appending each file as it loads. Which
    base a file uses depends on its stage list, measured across all 33 shipped
    containers rather than assumed:

      regular / bonus / ending / continue        base = GlobalCode word count
      presentation (Title, LSelect, Credits, Special)  base = 0

    Presentation files load before the globals, so their words sit at the start of
    the global array; everything else follows the globals. Both games agree on
    this split, which is the only reason a single rule works for both.

    Within a regular-stage file there are four groups of pointer, and each moves by
    a different amount. Merging the globals reorders the global arrays, so "how
    far did this game's globals move" and "how far did this file's own code move"
    are different questions with different answers:

      scripts[i][k]        this file's own code, which now sits after both games'
                           merged globals                      -> stage_code
      script_jumps[i][k]   this file's own jump words          -> stage_jump
      functions[i]         i <  global_count: verbatim copies of the game's global
        (code)              functions, whose addresses are set by where that game's
                           globals ended up                    -> global_code
      functions[i]         i >= global_count: stage-local       -> stage_code
        (code)
      function_jumps[i]    i <  global_count: the globals' own jump indices, which
                           were based at 0 in the stock file   -> global_jump
      function_jumps[i]    i >= global_count: based at the stock global jump size
                                                            -> stage_jump
      jumps[index]         a *relative* offset within one script. Never shifted.

    Worked example, Sonic 1's Zone01. Stock it holds 1514 jump words and its
    highest jump index is 4878, which is 3366 + 1514 - so indices are absolute.
    Sonic 2 is the primary game, so its globals stay at words 0..3904 of the
    merged arrays and Sonic 1's land at 3905..7270. Zone01's stage-local jump
    indices (based at 3366 stock) must therefore move to 7271, a shift of +3905;
    its copies of Sonic 1's global functions (based at 0) must also move by
    +3905; and its function pointers move by +63679 to follow Sonic 1's globals.

    Skipping any of these shifts makes a stage's objects run whichever script
    happens to sit at the old offset - silent, and indistinguishable from "the
    game is broken".
    """
    src = os.path.join(SRC, game, "Bytecode")
    presentation = {"Title", "LSelect", "Credits", "Special"}
    copied = 0
    missing = []
    for stem, folder in zones:
        name = stem + ".bin"
        source = os.path.join(src, name)
        target = os.path.join(OUT, folder + ".bin")
        if not os.path.exists(source):
            missing.append(folder)
            continue
        c = parse(source)

        if stem in presentation:
            # Loads before the globals, so every pointer in it stays put.
            stage_code = stage_jump = global_code = global_jump = 0
        else:
            stage_code = deltas["stage_code"]
            stage_jump = deltas["stage_jump"]
            global_code = deltas["global_code"]
            global_jump = deltas["global_jump"]

        def shift(value, delta, sentinel):
            return value if value == sentinel else value + delta

        c.scripts = [[shift(v, stage_code, NONE) for v in s] for s in c.scripts]
        c.script_jumps = [[shift(v, stage_jump, 0x3FFF) for v in s]
                          for s in c.script_jumps]

        functions = []
        jumps = []
        for i in range(len(c.functions)):
            f = c.functions[i]
            fj = c.function_jumps[i]
            if i < global_count:
                functions.append(f + global_code if f != NONE else f)
                jumps.append(fj + global_jump if fj != 0x3FFF else fj)
            else:
                functions.append(f + stage_code if f != NONE else f)
                jumps.append(fj + stage_jump if fj != 0x3FFF else fj)
        c.functions = functions
        c.function_jumps = jumps

        io.open(target, "wb").write(serialize(c))
        copied += 1
    return copied, missing


def shift_act_object_types(folders, offset):
    """Add `offset` to every object type index in a game's Act layout files.

    An Act file stores object placements as a raw type *index*, not a name:
    Scene.cpp:995 reads one byte straight into `object->type`. Those indices are
    game-relative - in stock Sonic 1 "Stage Setup" is type 4 - so once both games'
    globals share one table, every Sonic 1 index names a Sonic 2 object instead.
    Green Hill was creating Sonic 2's Stage Setup and Sonic 2's Star Post, because
    that is what lives at those indices now.

    Name-based creation is not affected: that goes through TypeName, which the
    engine resolves per game. Only the layout files need renumbering.

    The file layout, from Scene.cpp:930-1073:

        u8   title card length, then that many bytes
        u8x4 active tile layers, then the mid-point
        u8   xsize, u8 unused, u8 ysize, u8 unused
        u16  xsize * ysize tile indices
        u16  object count
        then per object:
            u16 attribs, u8 type, u8 propertyValue, s32 xpos, s32 ypos
            then one field per set attrib bit, in the order below

    Every step is asserted against the file length, because a layout file that
    parses to the wrong offset silently produces a stage where a third of the
    objects are in the wrong place - which looks like a level design problem
    rather than a data problem.
    """
    STAGE_DIR = os.path.join(os.path.dirname(os.path.dirname(OUT)), "Data", "Stages")

    # attrib bit -> width in bytes, in the order Scene.cpp reads them
    ATTRIB_FIELDS = [
        (0x0001, 4), (0x0002, 1), (0x0004, 4), (0x0008, 4), (0x0010, 1),
        (0x0020, 1), (0x0040, 1), (0x0080, 1), (0x0100, 4), (0x0200, 1),
        (0x0400, 1), (0x0800, 4), (0x1000, 4), (0x2000, 4), (0x4000, 4),
    ]

    rewritten = 0
    objects = 0
    problems = []

    for folder in folders:
        stage_dir = os.path.join(STAGE_DIR, folder)
        if not os.path.isdir(stage_dir):
            continue
        for name in sorted(os.listdir(stage_dir)):
            if not (name.startswith("Act") and name.endswith(".bin")):
                continue
            path = os.path.join(stage_dir, name)
            data = bytearray(io.open(path, "rb").read())
            pos = 0

            title_len = data[pos]; pos += 1
            pos += title_len
            pos += 5                                   # 4 layers + mid-point
            xsize = data[pos]; pos += 1
            pos += 1
            ysize = data[pos]; pos += 1
            pos += 1
            pos += 2 * xsize * ysize                   # tile indices

            count = data[pos] | (data[pos + 1] << 8); pos += 2
            for i in range(count):
                start = pos
                attribs = data[pos] | (data[pos + 1] << 8); pos += 2
                pos += 1                               # the type byte
                pos += 1                               # propertyValue
                pos += 8                               # xpos, ypos
                for bit, width in ATTRIB_FIELDS:
                    if attribs & bit:
                        pos += width
                if pos > len(data):
                    problems.append("%s/%s: object %d runs past the end"
                                    % (folder, name, i))
                    break
                if offset:
                    data[start + 2] = (data[start + 2] + offset) & 0xFF
                objects += 1
            else:
                if pos != len(data):
                    problems.append(
                        "%s/%s: parsed %d of %d bytes - the layout changed shape"
                        % (folder, name, pos, len(data)))
                    continue
                io.open(path, "wb").write(bytes(data))
                rewritten += 1

    return rewritten, objects, problems


def main():
    # This script owns the pack's bytecode folder: it is the only thing that
    # writes it, and stale files from an earlier naming scheme would otherwise
    # linger and be packed. They are inert - the engine only ever opens
    # Bytecode/<stage folder>.bin - but they inflate the pack and make "which
    # files does a stage need" unanswerable by looking at the directory.
    if os.path.isdir(OUT):
        for stale in os.listdir(OUT):
            if stale.lower().endswith(".bin"):
                os.remove(os.path.join(OUT, stale))
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    s1 = parse(os.path.join(SRC, "sonic1", "Bytecode", "GlobalCode.bin"))
    s2 = parse(os.path.join(SRC, "sonic2", "Bytecode", "GlobalCode.bin"))

    merged, dropped_functions = merge(s2, s1, secondary_object_offset=s2.script_count)
    blob = serialize(merged)
    io.open(os.path.join(OUT, "GlobalCode.bin"), "wb").write(blob)

    # Object-name lookup is a linear scan over the whole table. Sonic 1 and Sonic 2
    # share 33 object names, so without help the first game to register a name wins
    # and a Sonic 1 stage asking for "Stage Setup" gets Sonic 2's. The engine
    # limits the scan to one game when it knows where the split is, and this file
    # is that number: how many global object types belong to Sonic 2, which is the
    # game listed first. Absent, the engine falls back to single-game behaviour
    # rather than guessing.
    # OUT is <pack>/Data/Bytecode, so the game's own folder is two levels up, not one.
    split_path = os.path.join(
        os.path.dirname(os.path.dirname(OUT)), "Data", "Game",
        "ObjectGameSplit.bin")
    if not os.path.isdir(os.path.dirname(split_path)):
        os.makedirs(os.path.dirname(split_path))
    io.open(split_path, "wb").write(bytes([s2.script_count]))
    print("object name split: types 0..%d are Sonic 2, %d..%d are Sonic 1"
          % (s2.script_count - 1, s2.script_count,
             merged.script_count - 1))
    shared = len(set(object_names("sonic1")) & set(object_names("sonic2")))
    print("  %d names exist in both games; the split is what keeps them apart"
          % shared)

    # Each game ships per-stage files whose pointers assume its own GlobalCode. Two
    # different deltas are involved, because a per-stage file contains pointers
    # into two different places:
    #
    #   script_delta  how far the file's *own* code block moves, which is however
    #                 much the merged globals grew. Applies to `scripts`.
    #   global_delta  how far that game's *global* code moves, which depends on
    #                 whether the game is the primary (stays at 0) or the secondary
    #                 (appended after the primary). Applies to the first
    #                 `global_count` function entries of each file, which are
    #                 verbatim copies of the game's global functions.
    #
    # The jump *values* those indices point at are relative to each script's
    # start and are never touched.
    s2_code, s1_code = len(s2.code), len(s1.code)
    s2_jump, s1_jump = len(s2.jumps), len(s1.jumps)
    merged_code, merged_jump = len(merged.code), len(merged.jumps)
    plan = (
        # Sonic 2 is the primary game: its globals keep words 0..s2_code-1 and
        # jump words 0..s2_jump-1, so both of its global groups shift by nothing
        # while its stage-local code and jumps move past Sonic 1's globals.
        ("sonic2", SONIC2_ZONES, s2.function_count, {
            "stage_code": merged_code - s2_code,
            "stage_jump": merged_jump - s2_jump,
            "global_code": 0,
            "global_jump": 0,
        }),
        # Sonic 1 is secondary: its globals now start after Sonic 2's.
        ("sonic1", SONIC1_ZONES, s1.function_count, {
            "stage_code": merged_code - s1_code,
            "stage_jump": merged_jump - s1_jump,
            "global_code": s2_code,
            "global_jump": s2_jump,
        }),
    )
    for game, zones, gcount, deltas in plan:
        print("%s per-stage: stage code %+d, stage jumps %+d, globals %+d/%+d"
              % (game, deltas["stage_code"], deltas["stage_jump"],
                 deltas["global_code"], deltas["global_jump"]))
        globals()["_copied_" + game] = copy_stage_bytecode(
            game, zones, deltas, gcount)

    c1, m1 = globals()["_copied_sonic1"]
    c2, m2 = globals()["_copied_sonic2"]

    # The alignment that makes this work: config entry i must be the object whose
    # script is merged slot i+1.
    n1, n2 = object_names("sonic1"), object_names("sonic2")
    expected_objects = n2 + n1
    problems = []

    # Act layout files hold raw type indices, which are game-relative, so each
    # game's placements have to be renumbered into the merged table. Sonic 2 is
    # listed first and keeps its indices; Sonic 1 shifts by however many globals
    # Sonic 2 contributed.
    s1_folders = [f for _s, f in SONIC1_ZONES]
    s2_folders = [f for _s, f in SONIC2_ZONES]
    a1, o1, p1 = shift_act_object_types(s1_folders, len(n2))
    a2, o2, p2 = shift_act_object_types(s2_folders, 0)
    problems += p1 + p2
    print("Act layouts: Sonic 1 %d files / %d objects renumbered by +%d, "
          "Sonic 2 %d files / %d objects unchanged"
          % (a1, o1, len(n2), a2, o2))
    if len(expected_objects) != merged.script_count:
        problems.append(
            "object table has %d entries but the merged container has %d scripts"
            % (len(expected_objects), merged.script_count))

    # Every per-stage pointer must land inside the merged global + that stage's
    # own words. This is the check that catches a missed shift: a stage whose
    # pointers were not renumbered lands in the middle of the *global* code and
    # runs the wrong script, silently.
    total_code = len(merged.code)
    total_jump = len(merged.jumps)
    s2_code, s2_jump = len(s2.code), len(s2.jumps)
    presentation = {"TitleS1", "LSelectS1", "CreditsS1", "SpecialS1",
                    "TitleS2", "LSelectS2", "CreditsS2", "SpecialS2"}

    # Each game's globals occupy a known half of the merged container, so a stage
    # file's copies of its own global functions can be checked against that
    # game's region instead of against the whole thing.
    s2_len = len(s2.code)
    globals_range = {"sonic2": (0, s2_len),
                     "sonic1": (s2_len, total_code)}
    # ...and the same for the jump table, which the two games share.
    globals_jump_range = {"sonic2": (0, s2_jump),
                          "sonic1": (s2_jump, total_jump)}
    function_count = {"sonic1": s1.function_count, "sonic2": s2.function_count}

    for game, zones in (("sonic1", SONIC1_ZONES), ("sonic2", SONIC2_ZONES)):
        glo, ghi = globals_range[game]
        gjlo, gjhi = globals_jump_range[game]
        real_gcount = function_count[game]
        for _stem, folder in zones:
            path = os.path.join(OUT, folder + ".bin")
            if not os.path.exists(path):
                continue
            c = parse(path)
            # Presentation files load before the globals, so their own words sit
            # at the start of both arrays and they carry no copies of the global
            # functions at all - a presentation file's every function is its own.
            is_presentation = folder in presentation
            base = 0 if is_presentation else total_code
            jump_base = 0 if is_presentation else total_jump
            gcount = 0 if is_presentation else real_gcount

            for i, s in enumerate(c.scripts):
                for k, v in enumerate(s):
                    if v == NONE:
                        continue
                    if not base <= v < base + len(c.code):
                        problems.append(
                            "%s script %d event %d: word %d is outside its own "
                            "range [%d,%d)"
                            % (folder, i, k, v, base, base + len(c.code)))
                        break

            for i, sj in enumerate(c.script_jumps):
                for k, v in enumerate(sj):
                    if v == 0x3FFF:
                        continue
                    # One past the end is allowed, and is not something this
                    # merge introduced: 11 such entries exist in the stock files
                    # at exactly the same script, event and value - Sonic 1's
                    # LSelect script 7 event 2 holds 374 in a 374-word table,
                    # Sonic 2's Credits scripts 3 and 4 hold 194 in a 194-word
                    # one, and so on. They always sit on the highest-numbered
                    # script, which suggests the compiler marks "this event
                    # continues at the next script" by pointing one past its own
                    # table. RSDKv4 evidently tolerates it. Asserting a strict
                    # bound here would flag stock data, which is worse than
                    # useless - it would train the check to be ignored.
                    if not jump_base <= v <= jump_base + len(c.jumps):
                        problems.append(
                            "%s script %d event %d: jump index %d is outside its "
                            "own table [%d,%d]"
                            % (folder, i, k, v, jump_base,
                               jump_base + len(c.jumps)))
                        break

            # A stage file's function table is not its own. Its first `gcount`
            # entries are verbatim copies of that game's *global* functions -
            # Sonic 1's Zone01 has 149 functions of which the first 93 are
            # identical to GlobalCode.bin's - and only the rest are stage-local.
            # The two groups therefore have to be checked against different
            # ranges, and conflating them is what left Sonic 1 calling into
            # Sonic 2's global code.
            for i, v in enumerate(c.functions):
                if v == NONE:
                    continue
                if i < gcount:
                    lo, hi, what = glo, ghi, "copy of a global"
                else:
                    lo, hi, what = base, base + len(c.code), "stage-local"
                if not lo <= v < hi:
                    problems.append(
                        "%s function %d (%s): word %d is outside [%d,%d)"
                        % (folder, i, what, v, lo, hi))

            for i, v in enumerate(c.function_jumps):
                if v == 0x3FFF:
                    continue
                # Two groups, two ranges: the copies of the game's global
                # functions address that game's global jump table, and the
                # stage-local ones address this file's own.
                if i < gcount:
                    lo, hi = gjlo, gjhi
                else:
                    lo, hi = jump_base, jump_base + len(c.jumps)
                if not lo <= v <= hi:
                    problems.append(
                        "%s function %d: jump index %d is outside [%d,%d]"
                        % (folder, i, v, lo, hi))

    print("merged GlobalCode.bin: %d bytes, %d scripts (%d Sonic 2 + %d Sonic 1)"
          % (len(blob), merged.script_count, s2.script_count, s1.script_count))
    print("  code words %d, jump words %d"
          % (len(merged.code), len(merged.jumps)))
    print("object table: %d Sonic 2 + %d Sonic 1 = %d entries"
          % (len(n2), len(n1), len(expected_objects)))
    shared = len(set(n1) & set(n2))
    print("  %d object names exist in both games; both are kept, since they are"
          % shared)
    print("  different objects with different scripts")
    print("stage bytecode: Sonic 1 %d files, Sonic 2 %d files" % (c1, c2))
    if m1:
        print("  Sonic 1 missing: %s" % ", ".join(m1))
    if m2:
        print("  Sonic 2 missing: %s" % ", ".join(m2))
    print()
    print("functions merged: %d (%d Sonic 2 + %d Sonic 1)"
          % (merged.function_count, s2.function_count, dropped_functions))
    print("  An earlier version of this script claimed Sonic 1's functions could not")
    print("  be merged because scriptFunctionList is global. That was wrong: the")
    print("  table is global, but each entry holds an absolute scriptCode pointer,")
    print("  so appending gives every function its own slot.")
    print()
    print("KNOWN LIMITATION: CallFunction operands still index Sonic 2's function")
    print("  table. A Sonic 1 script calling function N reaches Sonic 2's N, and a")
    print("  Sonic 2 script calling a function past its own table runs off the end.")
    print("  Remapping those operands is the same job the object table needed and is")
    print("  not done yet - it needs the function-name table for each game.")

    if problems:
        print()
        print("FAIL:")
        for p in problems:
            print("  " + p)
        return 1
    print()
    print("OK: merged object table and merged scripts line up")
    return 0


if __name__ == "__main__":
    sys.exit(main())