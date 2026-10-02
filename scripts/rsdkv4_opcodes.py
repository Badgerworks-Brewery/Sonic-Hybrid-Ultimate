#!/usr/bin/env python3
"""RSDKv4's opcode table, in the order the engine actually compiles it.

`Script.cpp` declares `const FunctionInfo functions[]` inside `#if !RETRO_REV00`
guards, so the source contains 153 FunctionInfo entries but only **151** survive
to the binary. Any tool that indexes the table by position must skip the guarded
ones, or every opcode after the first guard is off by one or two.

That is not hypothetical: an earlier version of this module read the raw source
list and reported 153, which made a disassembly disagree with the engine's own
opcode trace and pointed the investigation at the wrong words.
"""

import io
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT_CPP = os.path.join(ROOT, "Hybrid-RSDK-Main", "RSDKV4-Decompilation",
                           "RSDKv4", "Script.cpp")

# RSDKv4 defines neither RETRO_REV00 nor the original-revision code paths.
RETRO_REV00 = False


def _walk():
    src = io.open(SCRIPT_CPP, encoding="utf-8", errors="replace").read()
    lines = src.split("\n")
    start = None
    for i, line in enumerate(lines):
        if re.match(r"\s*const FunctionInfo functions\[\]", line):
            start = i
            break
    if start is None:
        raise ValueError("could not find functions[] in %s" % SCRIPT_CPP)

    order = []
    stack = []
    for line in lines[start + 1:]:
        s = line.strip()
        if s.startswith("};"):
            break
        if s.startswith("#if"):
            # A guard naming REV00 is inactive; anything else is treated as
            # active, which matches how RSDKv4 is configured for this project.
            stack.append(not RETRO_REV00 if "REV00" in s else True)
            continue
        if s.startswith("#else"):
            if stack:
                stack[-1] = not stack[-1]
            continue
        if s.startswith("#endif"):
            if stack:
                stack.pop()
            continue
        if not all(stack):
            continue
        m = re.search(r'FunctionInfo\("([^"]+)"\s*,\s*(-?\d+)\)', s)
        if m:
            order.append((m.group(1), int(m.group(2))))
    return order


ORDER = _walk()
NAMES = [n for n, _s in ORDER]
SIZES = [s for _n, s in ORDER]
BY_NAME = {n: i for i, (n, _s) in enumerate(ORDER)}


def name_of(opcode):
    return NAMES[opcode] if 0 <= opcode < len(NAMES) else "?%d" % opcode


def size_of(opcode):
    return SIZES[opcode] if 0 <= opcode < len(SIZES) else -1


if __name__ == "__main__":
    print("active opcodes: %d" % len(ORDER))
    raw = len(re.findall(r'FunctionInfo\("',
                         io.open(SCRIPT_CPP, encoding="utf-8",
                                 errors="replace").read()))
    print("FunctionInfo entries in source: %d (2 are inside #if !RETRO_REV00)"
          % raw)
    for want in ("End", "WLower", "GetTableValue", "SetTableValue", "Inc", "loop"):
        if want in BY_NAME:
            print("  %-16s = %d" % (want, BY_NAME[want]))