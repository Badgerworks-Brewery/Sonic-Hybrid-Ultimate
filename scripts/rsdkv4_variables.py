#!/usr/bin/env python3
"""RSDKv4's script variable table, in compiled order.

Same guard handling as rsdkv4_opcodes.py: the source declares 253 names but two sit
inside `#if !RETRO_REV00`, so the compiled table has fewer. Indexing by the raw
source list shifts every variable past the first guard.
"""

import io
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT_CPP = os.path.join(ROOT, "Hybrid-RSDK-Main", "RSDKV4-Decompilation",
                           "RSDKv4", "Script.cpp")

RETRO_REV00 = False


def _walk():
    src = io.open(SCRIPT_CPP, encoding="utf-8", errors="replace").read()
    lines = src.split("\n")
    start = None
    for i, line in enumerate(lines):
        if re.match(r"\s*const char variableNames\[\]\[", line):
            start = i
            break
    if start is None:
        raise ValueError("could not find variableNames[] in %s" % SCRIPT_CPP)

    names = []
    stack = []
    for line in lines[start + 1:]:
        s = line.strip()
        if s.startswith("};"):
            break
        if s.startswith("#if"):
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
        m = re.match(r'^"([^"]+)",\s*(//.*)?$', s)
        if m:
            names.append(m.group(1))
    return names


NAMES = _walk()
BY_NAME = {n: i for i, n in enumerate(NAMES)}


def name_of(index):
    return NAMES[index] if 0 <= index < len(NAMES) else "var%d" % index


if __name__ == "__main__":
    print("active variables: %d" % len(NAMES))
    raw = len(re.findall(r'^\s*"[^"]+",\s*(//.*)?$',
                         io.open(SCRIPT_CPP, encoding="utf-8",
                                 errors="replace").read(), re.M))
    print("quoted entries in source: %d" % raw)
    for i in (0, 8, 9, 19, 20, 22):
        if i < len(NAMES):
            print("  %3d %s" % (i, NAMES[i]))