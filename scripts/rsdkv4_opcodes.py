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

# RSDKv4 defines RSDK_REVISION as 3 in RetroEngine.hpp (line 225), and derives
# every RETRO_REVxx from it:
#
#     RETRO_REV00 (RSDK_REVISION == 0)
#     RETRO_REV01 (RSDK_REVISION >= 1)
#     RETRO_REV02 (RSDK_REVISION >= 2)
#     RETRO_REV03 (RSDK_REVISION >= 3)
#
# At 3 that makes REV00 false and REV01/02/03 true, so `#if RETRO_REV00` blocks are
# excluded and `#if RETRO_REV01`, `#if !RETRO_REV02` and `#if RETRO_REV03` are all
# included. An earlier version of this file treated every guard it did not recognise
# as active, which kept `LoadFontFile` and `DrawText` in the table even though both
# sit inside `#if !RETRO_REV02` and are never compiled. That shifted every opcode
# after LoadFontFile by one.
RSDK_REVISION = 3


def _macro_value(name):
    if name == "RSDK_REVISION":
        return RSDK_REVISION
    for rev in ("00", "01", "02", "03"):
        if name == "RETRO_REV" + rev:
            n = int(rev)
            return (RSDK_REVISION == n) if n == 0 else (RSDK_REVISION >= n)
    # Anything else is a feature flag this project configures in.
    return True


def _guard_active(directive):
    """Whether the `#if` on this line is true for this project's configuration.

    The `!` matters and an earlier version of this file ignored it, so
    `#if RETRO_REV00` and `#if !RETRO_REV00` were treated identically. That put a
    7-operand `SetPaletteFade` in the table instead of the 6-operand one.
    """
    parts = directive.split()
    condition = parts[1] if len(parts) > 1 else ""
    negated = condition.lstrip().startswith("!")
    name = condition.lstrip("!~ ").split("(")[0].strip()
    value = _macro_value(name)
    return (not value) if negated else value


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
            stack.append(_guard_active(s))
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