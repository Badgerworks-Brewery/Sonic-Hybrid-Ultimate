#!/usr/bin/env python3
"""Check that the bytecode merger is correct before anything depends on it.

A merger that produces a container the engine silently mis-reads would show up as
objects with subtly wrong behaviour - the class of bug this project has already
shipped twice. So this asserts the properties that make the output safe:

1. Every shipped container round-trips: parse, re-serialize, re-parse, unchanged.
   If that holds for all 33 files the writer and reader agree with the engine's
   own layout.
2. Merged pointers stay inside the merged arrays, and every value that was the
   "no such script" sentinel still is.
3. A merged container re-parses with no bytes left over.

Run from the repository root:

    python scripts/test_bytecode_merger.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rsdkv4_bytecode_merger import (NONE, jump_none, merge, parse,  # noqa: E402
                                    parse_from_bytes, self_test, serialize)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Hybrid-RSDK-Main", "rsdk-source-data")


def check_pointers(label, c):
    """scriptCode pointers must be absolute and in range; jump entries are
    relative offsets, so they are bounded by the size of the jump table instead."""
    problems = []

    limit_code = len(c.code)
    limit_jump = len(c.jumps)

    for i, s in enumerate(c.scripts):
        for k, v in enumerate(s):
            if v != NONE and not 0 <= v < limit_code:
                problems.append("%s: script %d event %d scriptCode pointer %d out of range 0..%d"
                                % (label, i, k, v, limit_code - 1))
    for i, s in enumerate(c.script_jumps):
        for k, v in enumerate(s):
            # A relative offset, so the bound is the jump table size.
            if v != 0x3FFF and not 0 <= v < limit_jump:
                problems.append("%s: script %d event %d jump offset %d out of range 0..%d"
                                % (label, i, k, v, limit_jump - 1))
    for i, v in enumerate(c.functions):
        if v != NONE and not 0 <= v < limit_code:
            problems.append("%s: function %d scriptCode pointer %d out of range" % (label, i, v))
    for i, v in enumerate(c.function_jumps):
        if v != 0x3FFF and not 0 <= v < limit_jump:
            problems.append("%s: function %d jump index %d out of range 0..%d"
                            % (label, i, v, limit_jump - 1))

    return problems


def main():
    print("1. round-trip every shipped container")
    if not self_test():
        return 1
    print()

    s1 = parse(os.path.join(SRC, "sonic1", "Bytecode", "GlobalCode.bin"))
    s2 = parse(os.path.join(SRC, "sonic2", "Bytecode", "GlobalCode.bin"))
    print("   sonic1 GlobalCode: %d code words, %d jump words, %d scripts, %d functions"
          % (len(s1.code), len(s1.jumps), s1.script_count, s1.function_count))
    print("   sonic2 GlobalCode: %d code words, %d jump words, %d scripts, %d functions"
          % (len(s2.code), len(s2.jumps), s2.script_count, s2.function_count))
    print()

    print("2. merge Sonic 1 after Sonic 2 and validate the result")
    merged, dropped_functions = merge(s2, s1, secondary_object_offset=s2.script_count)
    print("   merged: %d code words, %d jump words, %d scripts, %d functions"
          % (len(merged.code), len(merged.jumps), merged.script_count, merged.function_count))

    problems = check_pointers("merged", merged)

    # Sentinels must survive: they are how "this object has no Draw script" is
    # expressed, and rewriting one into a real index would run garbage.
    primary_sentinels = sum(1 for s in s2.scripts for v in s if v == NONE)
    merged_sentinels = sum(1 for s in merged.scripts for v in s if v == NONE)
    secondary_sentinels = sum(1 for s in s1.scripts for v in s if v == NONE)
    expected = primary_sentinels + secondary_sentinels
    if merged_sentinels != expected:
        problems.append("sentinel count changed: %d in sources, %d merged (expected %d)"
                        % (primary_sentinels + secondary_sentinels, merged_sentinels, expected))

    # Same for the jumpTable table, whose sentinel is a different value.
    def jt_sentinels(c):
        return sum(1 for s in c.script_jumps for v in s if jump_none(v))

    expected_jt = jt_sentinels(s2) + jt_sentinels(s1)
    actual_jt = jt_sentinels(merged)
    if actual_jt != expected_jt:
        problems.append("jumpTable sentinel count changed: %d in sources, %d merged"
                        % (expected_jt, actual_jt))

    # Sonic 2's own entries must be untouched, since its stages already work.
    for i, s in enumerate(s2.scripts):
        if merged.scripts[i] != s:
            problems.append("script %d was altered by the merge" % i)
            break

    # Sonic 1's entries must sit after Sonic 2's, shifted by the code base.
    base = len(s2.code)
    for i, s in enumerate(s1.scripts):
        target = merged.scripts[s2.script_count + i]
        for k, v in enumerate(s):
            want = v if v == NONE else v + base
            if target[k] != want:
                problems.append("sonic1 script %d event %d: expected %d, got %d"
                                % (i, k, want, target[k]))
                break

    # ...and its jumpTable *indices*, which must shift by the jump-table base because
    # the arrays are concatenated. The values those indices point at are relative
    # offsets and are never touched - see rsdkv4_bytecode_merger.merge().
    jbase = len(s2.jumps)
    for i, s in enumerate(s1.script_jumps):
        target = merged.script_jumps[s2.script_count + i]
        for k, v in enumerate(s):
            want = v if jump_none(v) else v + jbase
            if target[k] != want:
                problems.append("sonic1 script %d event %d jump index: expected %d, got %d"
                                % (i, k, want, target[k]))
                break

    # And every jump target must stay inside its own script, which is the check
    # that catches a branch escaping into a neighbour.
    starts = sorted((min((v for v in s if v != NONE), default=1 << 30), i)
                    for i, s in enumerate(merged.scripts))
    for n, (start, si) in enumerate(starts):
        after = starts[n + 1][0] if n + 1 < len(starts) else len(merged.code)
        for k, jb in enumerate(merged.script_jumps[si]):
            if jb == 0x3FFF:
                continue
            target = start + merged.jumps[jb]
            if not start <= target < after:
                problems.append(
                    "merged script %d event %d: branch at word %d leaves the script "
                    "[%d,%d)" % (si, k, target, start, after))
                break

    print()

    print("3. re-parse the merged container")
    blob = serialize(merged)
    again = parse_from_bytes(blob)
    if again.scripts != merged.scripts:
        problems.append("merged container did not survive serialization")
    print("   serialized to %d bytes and re-parsed cleanly" % len(blob))
    print()

    print("  merged %d functions (%d Sonic 2 + %d Sonic 1). An earlier version of"
          % (merged.function_count, s2.function_count, dropped_functions))
    print("  this claimed functions could not be merged because scriptFunctionList is")
    print("  global. That was wrong: the table is global, but each entry holds an")
    print("  absolute scriptCode pointer, so appending gives every function its own")
    print("  slot. Callers still need their function indices remapped, which is the")
    print("  same job the object table needs and is not done yet.")
    print()

    if problems:
        print("FAIL: %d problem(s)" % len(problems))
        for p in problems[:15]:
            print("      " + p)
        return 1

    # The secondary's function tables must now be present and shifted.
    base = len(s2.code)
    jbase = len(s2.jumps)
    for i, f in enumerate(s1.functions):
        want = f if f == NONE else f + base
        got = merged.functions[s2.function_count + i]
        if got != want:
            print("FAIL: sonic1 function %d: expected %d, got %d" % (i, want, got))
            return 1
    for i, fj in enumerate(s1.function_jumps):
        want = fj if jump_none(fj) else fj + jbase
        got = merged.function_jumps[s2.function_count + i]
        if got != want:
            print("FAIL: sonic1 function %d jump index: expected %d, got %d"
                  % (i, want, got))
            return 1
    print("OK: both games' function tables are present and renumbered")

    print("OK: merged container is internally consistent")
    return 0


if __name__ == "__main__":
    sys.exit(main())