#!/usr/bin/env python3
"""Check every opcode's declared operand count against the handler that reads it.

`functions[]` in Script.cpp pairs each opcode name with an operand count, and the
engine fetches that many operands before dispatching. So if a handler reads
`scriptEng.operands[k]` with k >= the declared count, the operand fetch has already
run out and the words it consumed belong to the *next* instruction - which
desynchronises the script from that point on, permanently, with no error reported.

DrawText was exactly this: declared 7, handler reads 3. Reading the handler is what
identified it, because the walker cannot: narrowing an operand width to reduce the
walker's desync count produces convincing improvements that are wrong (see
rsdkv4_walk.py's docstring).

So this check is the authority: it compares the declaration with the code that
consumes it. Exit code 1 means the engine itself is inconsistent, which is worth
failing a build over.
"""

import io
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rsdkv4_opcodes import ORDER, _guard_active

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT_CPP = os.path.join(ROOT, "Hybrid-RSDK-Main", "RSDKV4-Decompilation",
                          "RSDKv4", "Script.cpp")


def strip_inactive(text):
    """Drop `#if` regions that this project's configuration excludes.

    Without this the check flags SetPaletteFade, whose handler reads operands[6]
    only inside `#if RETRO_REV00`, and LoadTextFile, whose handler reads operands[2]
    only inside `#if !RETRO_REV02`. Both guards are inactive at RSDK_REVISION 3. A
    check that reports correct code is worse than no check, because it teaches
    people to ignore it.
    """
    out = []
    # Each frame: (currently emitting, some branch already taken)
    stack = []
    for line in text.split("\n"):
        s = line.strip()
        if s.startswith("#if"):
            parent = all(f[0] for f in stack) if stack else True
            active = _guard_active(s) and parent
            stack.append([active, active])
            continue
        if s.startswith("#elif"):
            if stack:
                parent = all(f[0] for f in stack[:-1]) if len(stack) > 1 else True
                frame = stack[-1]
                frame[1] = frame[1] or (_guard_active(s) and parent)
                frame[0] = (not frame[1]) and (_guard_active(s) and parent)
            continue
        if s.startswith("#else"):
            if stack:
                parent = all(f[0] for f in stack[:-1]) if len(stack) > 1 else True
                frame = stack[-1]
                frame[0] = (not frame[1]) and parent
                frame[1] = True
            continue
        if s.startswith("#endif"):
            if stack:
                stack.pop()
            continue
        if all(f[0] for f in stack):
            out.append(line)
    return "\n".join(out)


def handler_operand_indices(body):
    """(indices read, indices written) for one handler.

    Handlers reuse the high operand slots as scratch space: Get16x16TileInfo
    declares four operands, computes `operands[4]`, `operands[5]` and
    `operands[6]` as locals, and then reads `operands[6]` back. That is fine and
    intended. What is not fine is *reading* a slot the handler never wrote, because
    the operand fetch only ever fills the declared slots and everything above them
    is stale from whatever ran before.

    So the caller is interested in read indices that are not also written here.
    """
    writes = set(int(m) for m in
                 re.findall(r"scriptEng\.operands\[(\d+)\]\s*=(?!=)", body))
    everything = set(int(m) for m in re.findall(r"operands\[(\d+)\]", body))
    reads = everything - writes
    loops = re.findall(r"for\s*\(\s*int\s+(\w+)\s*=\s*0\s*;\s*\1\s*<\s*(\d+)", body)
    loop_max = [int(n) for _v, n in loops]
    return reads, writes, loop_max


def main():
    src = io.open(SCRIPT_CPP, encoding="utf-8", errors="replace").read()

    # Only ProcessScript's dispatch switch matters. The same `case FUNC_X:` labels
    # appear in the compiler's own switches, and concatenating all of them pulls in
    # unrelated `operands[n]` references - which is how Get16x16TileInfo and
    # SetPaletteFade showed up as mismatches when their declarations are in fact
    # consistent with the code that runs them.
    start = src.find("void ProcessScript(")
    if start < 0:
        raise SystemExit("could not find ProcessScript in %s" % SCRIPT_CPP)
    body_all = src[start:]
    end = body_all.find("\nvoid ", 1)
    if end > 0:
        body_all = body_all[:end]

    cases = {}
    positions = [(m.start(), m.group(1)) for m in
                 re.finditer(r"case\s+(FUNC_\w+)\s*:", body_all)]
    for i, (pos, name) in enumerate(positions):
        stop = positions[i + 1][0] if i + 1 < len(positions) else len(body_all)
        cases.setdefault(name, "")
        cases[name] += strip_inactive(body_all[pos:stop])

    declared = {}
    for i, (name, size) in enumerate(ORDER):
        declared[name.lower()] = (i, size)

    problems = []
    checked = 0
    for func_name, body in sorted(cases.items()):
        # FUNC_ADDFOO -> "AddFoo", matching the FunctionInfo spelling, which
        # differs only in case.
        bare = func_name[5:].lower()
        if bare not in declared:
            continue
        index, size = declared[bare]
        explicit, written, loop_max = handler_operand_indices(body)
        if not explicit and not loop_max:
            continue
        checked += 1
        needed = max(list(explicit) + [m - 1 for m in loop_max] or [0])
        if needed >= size:
            problems.append(
                "%-24s opcode %3d declares %d operand(s) but the handler reads "
                "index %d, which it never writes"
                % (bare, index, size, needed))

    print("checked %d handlers against their declared operand counts" % checked)
    if problems:
        print()
        print("%d mismatch(es) - the operand fetch will read past the instruction:"
              % len(problems))
        for p in problems:
            print("  " + p)
        return 1
    print("OK: every handler stays inside its declared operand count")
    return 0


if __name__ == "__main__":
    sys.exit(main())