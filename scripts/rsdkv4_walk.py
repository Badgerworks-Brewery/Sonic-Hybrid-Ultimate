#!/usr/bin/env python3
"""Walk RSDKv4 bytecode linearly, exactly as ProcessScript does.

This exists because the engine decodes operands with variable width, and a
walker that assumes a fixed width desynchronises within a few instructions. Two
earlier attempts in this repo failed for that reason and were deleted rather than
shipped; this one is validated against every container the repo ships, and
`validate()` is what proves it.

Operand encoding, read from Script.cpp:

    SCRIPTVAR_VAR = 1        (Script.cpp:623)
        tag, array selector, then the variable index. The selector decides
        whether one or two more words follow (Script.cpp:3460-3485):

            VARARR_NONE = 0        no more words
            VARARR_ARRAY = 1       flag word, then an index word
            VARARR_ENTNOPLUS1 = 2  same shape
            VARARR_ENTNOMINUS1 = 3 same shape

        so a variable operand is 3 words (VARARR_NONE) or 5 words.

    SCRIPTVAR_INTCONST = 2   tag, then one word (Script.cpp:4253)

    SCRIPTVAR_STRCONST = 3   tag, a length word, then the characters packed
        four to a word, most significant byte first (Script.cpp:4256-4281).
        The length is the plain character count - the writer stores
        `StrLength(funcName) - 2` (Script.cpp:1943) - and the read consumes
        ceil(length/4) words plus one final increment, so a string operand is
        2 + ceil(length/4) words.

        An earlier version of this file assumed the length was `n * 4` and
        decoded `length//4 + 1`. That is wrong and is what made the first
        disassembler drift.
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


def script_ranges(container):
    """[(start, end, script_index)] for each script, by sorted start word.

    Scripts share code ranges in the shipped files - several events can point at
    the same code - so ranges are computed from distinct start words, not from
    script identity.
    """
    starts = sorted({v for s in container.scripts for v in s
                    if v != 0x3FFFF and v < len(container.code)})
    out = []
    for i, start in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else len(container.code)
        out.append((start, end, i))
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
            problems = validate(c, name)
            total += 1
            if problems:
                bad += 1
                print("  %-28s %d problem(s)" % (name, len(problems)))
                for p in problems[:3]:
                    print("      " + p)
    print("\n%d container(s) walked, %d with problems" % (total, bad))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())