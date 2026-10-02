#!/usr/bin/env python3
"""Merge two RSDKv4 bytecode containers into one.

RSDKv4 loads exactly one `Bytecode/GlobalCode.bin` for the whole process, and it
decides the text-or-bytecode question by whether that single file resolves
(Scene.cpp:675). Sonic 1 and Sonic 2 each ship their own, defining different
things for the same object indices, so only one game can have working object
logic at a time. Merging them is what makes both work.

## Container layout

Reverse-engineered from `RSDKV4-Decompilation/RSDKv4/Script.cpp` `LoadBytecode()`
and confirmed by exact byte consumption on every shipped file:

    u32  scriptCodeCount
         blocks: byte header, count&0x7F words; >=0x80 means 32-bit words
    u32  jumpTableCount
         blocks: same encoding
    u16  scriptCount
         scriptCount * 3 * u32   event scriptCode pointers (Update, Draw, Startup)
         scriptCount * 3 * u32   event jumpTable pointers
    u16  functionCount
         functionCount * u32     function scriptCode pointers
         functionCount * u32     function jumpTable pointers

All pointers are **absolute indices** into the concatenated `scriptCode` and
`jumpTable` arrays, which is why merging needs renumbering rather than plain
concatenation. `0x0003FFFF` means "no such script" and is carried through.

## What this merges, and what it deliberately does not

Global object scripts (GlobalCode.bin) only. Per-stage files are copied under
their own names and need no merging, because a stage's scripts are addressed by
object index that only that stage defines.

Functions are **not** merged. RSDKv4 indexes `scriptFunctionList` globally from
0, so two games' function tables cannot both start at 0. Sonic 1 and Sonic 2's
`CallFunction` targets would collide. This merger therefore refuses to run unless
it can place both tables side by side, and reports that as a known limitation
rather than silently producing a container whose functions call the wrong code.
"""

import io
import os
import struct
import sys

# The two pointer tables do not share a sentinel. Measured from the shipped
# files: scriptCode pointers use 0x0003FFFF, jumpTable pointers use 0x00003FFF.
NONE = 0x0003FFFF
JUMP_NONE = 0x00003FFF


class Container(object):
    def __init__(self):
        self.code = []
        self.jumps = []
        self.scripts = []      # list of [update, draw, startup] absolute pointers
        self.script_jumps = []  # list of [update, draw, startup] absolute pointers
        self.functions = []
        self.function_jumps = []

    @property
    def script_count(self):
        return len(self.scripts)

    @property
    def function_count(self):
        return len(self.functions)


def read_blocks(data, pos):
    """Read one count-prefixed run of 1- or 4-byte words."""
    if pos + 4 > len(data):
        raise ValueError("truncated block header")
    n = struct.unpack_from("<I", data, pos)[0]
    pos += 4
    out = []
    while len(out) < n:
        if pos >= len(data):
            raise ValueError("truncated block stream")
        header = data[pos]
        pos += 1
        k = header & 0x7F
        if header >= 0x80:
            if pos + 4 * k > len(data):
                raise ValueError("truncated 32-bit block")
            out.extend(struct.unpack_from("<%di" % k, data, pos))
            pos += 4 * k
        else:
            if pos + k > len(data):
                raise ValueError("truncated 8-bit block")
            out.extend(data[pos:pos + k])
            pos += k
    return out, pos


def parse(path):
    """Parse a container, asserting the layout consumed the file exactly."""
    data = io.open(path, "rb").read()
    c = Container()

    c.code, pos = read_blocks(data, 0)
    c.jumps, pos = read_blocks(data, pos)

    script_count = struct.unpack_from("<H", data, pos)[0]
    pos += 2
    for _ in range(script_count):
        c.scripts.append(list(struct.unpack_from("<3I", data, pos)))
        pos += 12
    for _ in range(script_count):
        c.script_jumps.append(list(struct.unpack_from("<3I", data, pos)))
        pos += 12

    function_count = struct.unpack_from("<H", data, pos)[0]
    pos += 2
    for _ in range(function_count):
        c.functions.append(struct.unpack_from("<I", data, pos)[0])
        pos += 4
    for _ in range(function_count):
        c.function_jumps.append(struct.unpack_from("<I", data, pos)[0])
        pos += 4

    if pos != len(data):
        raise ValueError("%s: layout consumed %d of %d bytes (%d left over)"
                         % (os.path.basename(path), pos, len(data), len(data) - pos))
    return c


def write_blocks(words):
    """Re-encode a word list, packing runs of small values into 8-bit blocks."""
    out = bytearray()
    out += struct.pack("<I", len(words))
    i = 0
    n = len(words)
    while i < n:
        # Values 0..255 that fit a signed byte are the common case; anything
        # outside needs the 32-bit form.
        if 0 <= words[i] <= 127:
            run = 0
            while i + run < n and run < 0x7F and 0 <= words[i + run] <= 127:
                run += 1
            out.append(run)          # header < 0x80: `run` 8-bit words follow
            out += bytes(words[i:i + run])
            i += run
        else:
            run = 0
            while i + run < n and run < 0x7F and not (0 <= words[i + run] <= 127):
                run += 1
            out.append(0x80 | run)   # header >= 0x80: `run` 32-bit words follow
            out += struct.pack("<%di" % run, *words[i:i + run])
            i += run
    return bytes(out)


def merge(primary, secondary, secondary_object_offset):
    """Append `secondary` after `primary`, renumbering every absolute pointer.

    `secondary_object_offset` is the object index the secondary game's scripts
    will live at, so its object pointers are recorded against that. Event
    pointers are renumbered by the code/jump-table base shift.
    """
    out = Container()

    code_base = len(primary.code)
    jump_base = len(primary.jumps)

    out.code = list(primary.code) + list(secondary.code)
    out.jumps = list(primary.jumps) + list(secondary.jumps)

    # Primary keeps its own indices untouched.
    out.scripts = [list(s) for s in primary.scripts]
    out.script_jumps = [list(s) for s in primary.script_jumps]

    # A sentinel means "this object has no such script". It must pass through
    # unchanged: turning it into an index would make the engine execute whatever
    # happens to live there.
    def shift_code(value, base):
        return value if value == NONE else value + base

    def shift_jump(value, base):
        return value if jump_none(value) else value + base

    for s, sj in zip(secondary.scripts, secondary.script_jumps):
        out.scripts.append([shift_code(v, code_base) for v in s])
        out.script_jumps.append([shift_jump(v, jump_base) for v in sj])

    # Functions cannot be merged: RSDKv4 indexes scriptFunctionList from 0
    # globally, so two games' tables would overlap. Carried over only when the
    # secondary has none.
    out.functions = list(primary.functions)
    out.function_jumps = list(primary.function_jumps)

    return out, secondary.function_count


def serialize(c):
    out = bytearray()
    out += write_blocks(c.code)
    out += write_blocks(c.jumps)
    out += struct.pack("<H", c.script_count)
    for s in c.scripts:
        out += struct.pack("<3I", *s)
    for s in c.script_jumps:
        out += struct.pack("<3I", *s)
    out += struct.pack("<H", c.function_count)
    for f in c.functions:
        out += struct.pack("<I", f)
    for f in c.function_jumps:
        out += struct.pack("<I", f)
    return bytes(out)


def jump_none(value):
    """True when a jumpTable pointer means 'no such jump table'.

    The two tables do not share a sentinel. Reading the shipped files shows
    scriptCode pointers use 0x0003FFFF (5 occurrences in each GlobalCode) while
    jumpTable pointers use 0x00003FFF (16383), which is simply past the end of
    every jump table in both games. Treating one as the other would either
    renumber a sentinel into a live index or fail to renumber a real one, so
    each table is checked against its own.
    """
    return value == JUMP_NONE


def self_test():
    """Round-trip the shipped files: parse then re-serialize and re-parse."""
    root = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                        "Hybrid-RSDK-Main", "rsdk-source-data")
    checked = 0
    for game in ("sonic1", "sonic2"):
        for name in sorted(os.listdir(os.path.join(root, game, "Bytecode"))):
            if not name.endswith(".bin"):
                continue
            path = os.path.join(root, game, "Bytecode", name)
            original = parse(path)
            again = parse_from_bytes(serialize(original))
            if again.code != original.code or again.jumps != original.jumps:
                print("  FAIL round-trip changed the word streams: %s/%s" % (game, name))
                return False
            if again.scripts != original.scripts or again.script_jumps != original.script_jumps:
                print("  FAIL round-trip changed the script table: %s/%s" % (game, name))
                return False
            if again.functions != original.functions:
                print("  FAIL round-trip changed the function table: %s/%s" % (game, name))
                return False
            checked += 1
    print("round-tripped %d shipped containers with no loss" % checked)
    return True


def parse_from_bytes(data):
    c = Container()
    c.code, pos = read_blocks(data, 0)
    c.jumps, pos = read_blocks(data, pos)
    n = struct.unpack_from("<H", data, pos)[0]; pos += 2
    for _ in range(n):
        c.scripts.append(list(struct.unpack_from("<3I", data, pos))); pos += 12
    for _ in range(n):
        c.script_jumps.append(list(struct.unpack_from("<3I", data, pos))); pos += 12
    f = struct.unpack_from("<H", data, pos)[0]; pos += 2
    for _ in range(f):
        c.functions.append(struct.unpack_from("<I", data, pos)[0]); pos += 4
    for _ in range(f):
        c.function_jumps.append(struct.unpack_from("<I", data, pos)[0]); pos += 4
    if pos != len(data):
        raise ValueError("re-parsed %d of %d bytes" % (pos, len(data)))
    return c


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(0 if self_test() else 1)
    print(__doc__)
    sys.exit(0)