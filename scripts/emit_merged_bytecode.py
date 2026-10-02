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
    ("Zone01", "ZoneEHZ"), ("Zone02", "ZoneCPZ"), ("Zone03", "ZoneARZ"),
    ("Zone04", "ZoneCNZ"), ("Zone05", "ZoneHTZ"), ("Zone06", "ZoneMCZ"),
    ("Zone07", "ZoneOOZ"), ("Zone08", "ZoneHPZ"), ("Zone09", "ZoneMPZ"),
    ("Zone10", "ZoneSCZ"), ("Zone11", "ZoneWFZ"), ("Zone12", "ZoneDEZ"),
    ("Title", "TitleS2"), ("LSelect", "LSelectS2"),
    ("Credits", "CreditsS2"), ("Ending", "EndingS2"),
    ("Continue", "ContinueS2"), ("Special", "Special2"),
]

SONIC1_ZONES = [
    ("Zone01", "ZoneGHZ"), ("Zone02", "ZoneMZ"), ("Zone03", "ZoneSYZ"),
    ("Zone04", "ZoneLZ"), ("Zone05", "ZoneSZ"), ("Zone06", "ZoneSBZ"),
    ("Title", "TitleS1"), ("LSelect", "LSelectS1"),
    ("Credits", "CreditsS1"), ("Ending", "EndingS1"),
    ("Continue", "ContinueS1"), ("Special", "Special1"),
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


def copy_stage_bytecode(game, zones, global_base_shift, jump_base_shift):
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

    Merging changes the global word count, so every regular-stage file must shift
    by the same delta. Presentation files keep base 0 and need no shift. Skipping
    this makes a stage's objects run whichever script happens to sit at the old
    offset - silent, and indistinguishable from "the game is broken".
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
            code_delta = jump_delta = 0
        else:
            code_delta, jump_delta = global_base_shift, jump_base_shift

        def shift(value, delta, sentinel):
            return value if value == sentinel else value + delta

        if code_delta:
            # Only the scriptCode pointers are absolute. jumpTable entries are
            # *relative* offsets from the script's own start - the engine computes
            # `scriptCodeStart + jumpTable[jumpTableStart + slot]`
            # (Script.cpp:4324) - and every shipped value is small (0..935),
            # consistent with a distance within one script. Adding a base to them
            # sends every branch into whichever script happens to sit at the
            # offset, which is exactly the infinite loop this merge produced:
            # Sonic 1's Stage Setup branched into Sonic 2's Player 2 Object.
            c.scripts = [[shift(v, code_delta, NONE) for v in s] for s in c.scripts]
        # Functions are indexed globally from 0 and are not merged, so per-stage
        # function pointers are left alone for the same reason as GlobalCode's.

        io.open(target, "wb").write(serialize(c))
        copied += 1
    return copied, missing


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    s1 = parse(os.path.join(SRC, "sonic1", "Bytecode", "GlobalCode.bin"))
    s2 = parse(os.path.join(SRC, "sonic2", "Bytecode", "GlobalCode.bin"))

    merged, dropped_functions = merge(s2, s1, secondary_object_offset=s2.script_count)
    blob = serialize(merged)
    io.open(os.path.join(OUT, "GlobalCode.bin"), "wb").write(blob)

    # Each game ships per-stage files whose scriptCode pointers assume its own
    # GlobalCode. Shift them by however much the merged global changed. Jump
    # tables need no shift: their entries are relative to each script's start.
    for game, zones, own in (("sonic2", SONIC2_ZONES, s2), ("sonic1", SONIC1_ZONES, s1)):
        code_shift = len(merged.code) - len(own.code)
        print("%s per-stage scriptCode shift: %+d (jump tables unchanged)"
              % (game, code_shift))
        globals()["_copied_" + game] = copy_stage_bytecode(
            game, zones, code_shift, 0)

    c1, m1 = globals()["_copied_sonic1"]
    c2, m2 = globals()["_copied_sonic2"]

    # The alignment that makes this work: config entry i must be the object whose
    # script is merged slot i+1.
    n1, n2 = object_names("sonic1"), object_names("sonic2")
    expected_objects = n2 + n1
    problems = []
    if len(expected_objects) != merged.script_count:
        problems.append(
            "object table has %d entries but the merged container has %d scripts"
            % (len(expected_objects), merged.script_count))

    # Every per-stage pointer must land inside the merged global + that stage's
    # own words. This is the check that catches a missed shift: a stage whose
    # pointers were not renumbered lands in the middle of the *global* code and
    # runs the wrong script, silently.
    total_code = len(merged.code)
    presentation = {"TitleS1", "LSelectS1", "CreditsS1", "Special1",
                    "TitleS2", "LSelectS2", "CreditsS2", "Special2"}
    for folder in [f for _s, f in SONIC1_ZONES] + [f for _s, f in SONIC2_ZONES]:
        path = os.path.join(OUT, folder + ".bin")
        if not os.path.exists(path):
            continue
        c = parse(path)
        # Presentation files sit at base 0; everything else follows the globals.
        base = 0 if folder in presentation else total_code
        for i, s in enumerate(c.scripts):
            for k, v in enumerate(s):
                if v == NONE:
                    continue
                if not base <= v < base + len(c.code):
                    problems.append(
                        "%s script %d event %d: word %d is outside its own range "
                        "[%d,%d)" % (folder, i, k, v, base, base + len(c.code)))
                    break

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
    if dropped_functions:
        print()
        print("KNOWN LIMITATION: %d Sonic 1 functions are not merged. RSDKv4 indexes"
              % dropped_functions)
        print("  scriptFunctionList globally from 0, so both games' tables cannot")
        print("  start there. A Sonic 1 script calling a named function may reach")
        print("  Sonic 2's instead of its own.")

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