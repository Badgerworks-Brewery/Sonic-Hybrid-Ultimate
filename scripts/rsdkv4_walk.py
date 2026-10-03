#!/usr/bin/env python3
"""Walk RSDKv4 bytecode linearly, exactly as ProcessScript does.

This exists because the engine decodes operands with variable width, and a walker
that assumes a fixed width desynchronises within a few instructions. Three earlier
attempts in this repo failed for that reason and were deleted rather than shipped.

STATE: this walker agrees with the engine on 647 of the 794 script ranges across
every container Sonic 1 and Sonic 2 ship - 81%. It is **not** accurate enough to
rewrite operands, and nothing depends on it. Do not treat it as authoritative.

That 81% is only meaningful because of how the ranges are found. Two earlier
versions of this file reported flattering numbers for the wrong reason:

- One filtered script pointers with `v < len(container.code)`, which is wrong for
  every per-stage container, because their pointers are absolute into the engine's
  combined array. It reported 569 ranges and every one came from a GlobalCode or
  presentation file - no stage bytecode was ever walked. Its 74% was a measurement
  of the easy half.
- The fix for that shifted pointers against each container's own lowest pointer,
  which moves every index by the 262-word prologue the compiler emits first, so it
  read the wrong words throughout and reported 16%.

`script_ranges()` now takes the container's real placement base, which
`placement_base()` derives from the sibling GlobalCode.bin. Measured that way, the
walker covers 794 ranges and agrees on 647.

Operand encoding, read from Script.cpp:

    SCRIPTVAR_VAR = 1        (Script.cpp:623)
        tag, array selector, then the variable index. The selector decides
        how many more words follow (Script.cpp:3460-3485):

            VARARR_NONE = 0        no more words
            VARARR_ARRAY = 1       flag word, then an index word
            VARARR_ENTNOPLUS1 = 2  same shape
            VARARR_ENTNOMINUS1 = 3 same shape

        so a variable operand is 3 words (VARARR_NONE) or 5 words. The array case
        is *always* three further words - selector, flag, index - whatever the
        flag's value; making the count depend on the flag is wrong in both
        directions, and an earlier version of this file did exactly that.

    SCRIPTVAR_INTCONST = 2   tag, then one word (Script.cpp:4253)

    SCRIPTVAR_STRCONST = 3   tag, a length word, then the characters packed
        four to a word, most significant byte first (Script.cpp:4256-4281). The
        length is the plain character count - the writer stores
        `StrLength(funcName) - 2` (Script.cpp:1943).

        The width is `3 + length // 4`, not `2 + ceil(length / 4)`: the reader
        increments once more after the character loop (Script.cpp:4280), so a
        16-character string costs 7 words rather than the 6 the writer appears to
        emit. An earlier version used the writer's count and desynchronised on the
        very next opcode.

A warning about "fixing" the engine from this walker's numbers
---------------------------------------------------------------
Searching for an operand width that reduces the desync count finds convincing
improvements that are wrong. Measured over Sonic 2's GlobalCode.bin ranges, from
208 desynchronising:

    GetVersionNumber  declared 2   "try 3" -> 173 desync   (looks great)
    Abs               declared 1   "try 3" -> 148 desync   (looks better)

Both handlers use exactly the declared number of operands - GetVersionNumber does
`menu->entryHighlight[menu->rowCount] = operands[1]`, Abs does
`operands[0] = abs(operands[0])` - so the table is correct and the apparent gain is
coincidence: a wrong width sometimes resynchronises a stream by landing on a word
that happens to look like an opcode.

DrawText, by contrast, really was wrong: its handler uses three operands and the
table said seven, so the operand fetch ate four words belonging to the next
instruction. That was confirmed by reading the handler, not by the count.

Read the handler. Never trust the desync count.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rsdkv4_opcodes import ORDER, name_of, size_of  # noqa: E402

SCRIPTVAR_VAR = 1
SCRIPTVAR_INTCONST = 2
SCRIPTVAR_STRCONST = 3

VARARR_NONE = 0
ARRAY_KINDS = (1, 2, 3)

OPCODE_COUNT = len(ORDER)          # 151; two source entries sit behind #if
FUNC_CALLFUNCTION = None           # filled in by _find_callfunction()


def _find_callfunction():
    """CallFunction's opcode number, read from the opcode table by name."""
    for i, (name, _size) in enumerate(ORDER):
        if name.lower().replace(" ", "") == "callfunction":
            return i
    raise ValueError("CallFunction not found in the opcode table")


FUNC_CALLFUNCTION = _find_callfunction()


class Desync(Exception):
    """Raised when a walk cannot stay aligned with the engine's decoding."""


def operand_width(code, pc):
    """Words consumed by the operand whose tag is at `code[pc]`."""
    tag = code[pc]
    if tag == SCRIPTVAR_VAR:
        width = 2                                   # tag + array selector
        selector = code[pc + 1]
        if selector in ARRAY_KINDS:
            # Always three words: the selector was already counted, then
            # `if (code[ptr++] == 1) arrayVal = arrayPosition[code[ptr++]]; else
            # arrayVal = code[ptr++]` (Script.cpp:3464-3482). The flag word is
            # consumed either way and exactly one index word follows it, so an
            # array operand is 5 words total and never 4. A version of this file
            # that made the count depend on the flag's value desynchronised.
            width += 2
        return width + 1                            # the variable index
    if tag == SCRIPTVAR_INTCONST:
        return 2
    if tag == SCRIPTVAR_STRCONST:
        length = code[pc + 1]
        if length < 0:
            raise Desync("string length %d at word %d" % (length, pc))
        # 3 + length // 4, not 2 + ceil(length / 4). The reader loops over the
        # characters packing four to a word and *then* increments once more
        # (Script.cpp:4280), so a 16-character string costs 7 words rather than
        # the 6 the writer appears to emit. Getting this wrong desynchronises
        # the very next opcode, which is what an earlier version of this file did.
        return 3 + length // 4
    raise Desync("unknown operand tag %d at word %d" % (tag, pc))


def operand_constant(code, pc):
    """(word_offset_of_value, value) for an operand that is a plain constant.

    Only int constants are meaningful here; a CallFunction's index is the first
    operand of that instruction and is always encoded as an int constant.
    """
    return pc + 1, code[pc + 1]


def instruction_width(code, pc):
    """Total words occupied by the instruction starting at `pc`, or None."""
    op = code[pc]
    if op < 0 or op >= OPCODE_COUNT:
        return None
    q = pc + 1
    for _ in range(size_of(op)):
        q += operand_width(code, q)
    return q - pc


def walk(code, start, end, on_callfunction=None):
    """Walk [start, end) yielding (pc, opcode).

    `on_callfunction` is called as fn(pc, operand_index, function_index) for each
    CallFunction, where operand_index is the word offset of the index itself. It
    is the hook used to renumber function references after a merge.

    Raises Desync if an opcode or operand tag is not one the engine could read,
    or if a width runs past `end`.
    """
    pc = start
    while pc < end:
        op = code[pc]
        if op < 0 or op >= OPCODE_COUNT:
            raise Desync("opcode %d at word %d" % (op, pc))
        count = size_of(op)
        q = pc + 1
        for i in range(count):
            width = operand_width(code, q)
            if op == FUNC_CALLFUNCTION and i == 0 and code[q] == SCRIPTVAR_INTCONST:
                if on_callfunction:
                    on_callfunction(pc, q + 1, code[q + 1])
            q += width
        if q > end:
            raise Desync("instruction at word %d runs past the end (%d > %d)"
                         % (pc, q, end))
        yield pc, op
        pc = q


def script_ranges(container, base=0):
    """[(start, end, index)] over this container's own code.

    The pointers a container stores are *absolute* indices into the engine's
    combined scriptCode array, which is built by appending each file as it loads.
    So a per-stage container's pointers start well past its own word count: Sonic
    1's Zone01.bin holds 30381 words yet its first script points at 53139, because
    it is compiled to sit after GlobalCode.bin's 52319 words.

    That makes "is this pointer inside my own code" the wrong test, and applying it
    to `len(container.code)` silently discarded every per-stage container. An
    earlier version of this file reported 569 script ranges, all of them from a
    GlobalCode or presentation file, so no stage bytecode had ever been walked. It
    then "fixed" the numbers by shifting pointers against the container's own
    lowest pointer, which quietly moved every index by the 262-word prologue the
    compiler emits first - reading the wrong words and reporting 16% agreement.

    So `base` is the container's real placement in the combined array, and it has
    to be supplied: 0 for GlobalCode.bin and for presentation files, which load
    before the globals, and the game's own GlobalCode word count for everything
    else. main() derives it from the sibling GlobalCode.bin.

    Scripts share code (several events can point at the same block), so ranges come
    from distinct start words, not script identity.
    """
    absolute = sorted({v for s in container.scripts for v in s
                       if v != 0x3FFFF})
    if not absolute:
        return []
    local = [v - base for v in absolute]
    if local[0] < 0 or local[0] >= len(container.code):
        # Legitimate: some containers carry code none of their own scripts point
        # at. Sonic 1's Continue.bin holds 1669 words but all three of its scripts
        # live in GlobalCode.bin, so it has no local ranges to walk at all.
        return []
    out = []
    for i, s in enumerate(local):
        e = local[i + 1] if i + 1 < len(local) else len(container.code)
        if e > len(container.code):
            e = len(container.code)
        out.append((s, e, i))
    return out


def validate(container, label):
    """Walk every script and function; returns a list of problem strings."""
    problems = []

    covered = set()
    for start, end, _ in script_ranges(container):
        try:
            for pc, _op in walk(container.code, start, end):
                covered.add(pc)
        except Desync as exc:
            problems.append("%s: script range [%d,%d): %s" % (label, start, end, exc))
            continue

    for i, ptr in enumerate(container.functions):
        if ptr == 0x3FFFF:
            continue
        try:
            for _pc, _op in walk(container.code, ptr, len(container.code)):
                pass
        except Desync as exc:
            problems.append("%s: function %d at %d: %s" % (label, i, ptr, exc))

    return problems


PRESENTATION = {"Title", "LSelect", "Credits", "Special"}


def placement_base(root, name):
    """Where a container's own code sits in the engine's combined scriptCode.

    RSDKv4 appends each bytecode file to one shared scriptCode array as it loads,
    so every pointer in the file is absolute. Two files load before the globals -
    GlobalCode.bin itself, and the presentation stages, because those are read while
    the config is being parsed - and everything else loads after them. That split
    is measured from the shipped files rather than assumed: Sonic 1's Zone01 has
    1514 jump words of its own yet its lowest jump index is 3388, which is 3366 plus
    its own count, and Title.bin's lowest is 28, inside its own 423.

    So the base is the sibling GlobalCode's word count for a regular stage, and zero
    for everything that loads first.
    """
    stem = name[:-4] if name.lower().endswith(".bin") else name
    if stem == "GlobalCode" or stem in PRESENTATION:
        return 0

    from rsdkv4_bytecode_merger import parse
    global_path = os.path.join(root, "GlobalCode.bin")
    if not os.path.exists(global_path):
        return 0
    return len(parse(global_path).code)


def main():
    from rsdkv4_bytecode_merger import parse

    roots = sys.argv[1:] or [
        os.path.join("Hybrid-RSDK-Main", "rsdk-source-data", g, "Bytecode")
        for g in ("sonic1", "sonic2", "soniccd")
    ]
    dirs = []
    for root in roots:
        if not os.path.isdir(root):
            print("skipping %s: not a directory" % root)
            continue
        if any(f.endswith(".bin") for f in os.listdir(root)):
            dirs.append(root)                        # already a bytecode folder
        else:
            dirs.extend(os.path.join(root, d) for d in sorted(os.listdir(root))
                        if os.path.isdir(os.path.join(root, d)))

    total = 0
    bad = 0
    for d in sorted(dirs):
        for name in sorted(os.listdir(d)):
            if not name.endswith(".bin"):
                continue
            path = os.path.join(d, name)
            try:
                c = parse(path)
            except Exception as exc:                # noqa: BLE001
                print("  %-28s parse failed: %s" % (os.path.basename(path), exc))
                continue
            ranges = script_ranges(c, placement_base(root, name))
            for start, end, _ in ranges:
                total += 1
                try:
                    for _pc, _op in walk(c.code, start, end):
                        pass
                except Desync:
                    bad += 1

    print("\n%d script range(s) walked, %d desynchronised" % (total, bad))
    if total:
        print("agreement: %d of %d (%.0f%%)"
              % (total - bad, total, 100.0 * (total - bad) / total))
    if bad:
        print()
        print("This walker is NOT accurate enough to rewrite operands. That is a")
        print("known, recorded state, not a regression to be chased - see the")
        print("module docstring, which also explains why narrowing a width to")
        print("reduce this number is how you end up breaking the engine.")
    return 0


if __name__ == "__main__":
    sys.exit(main())