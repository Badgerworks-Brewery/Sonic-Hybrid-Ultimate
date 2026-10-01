#!/usr/bin/env python3
"""Fail if a Windows batch file has unescaped parentheses inside an if-block.

cmd.exe parses a parenthesised block as a unit. An unescaped "(" or ")" inside an
echo line within such a block terminates or corrupts the block, so its commands
can end up running unconditionally.

That exact bug made run_hybrid.bat print "hybrid data not found" and exit 1 on
every invocation, even when Data.rsdk was sitting next to it.

Usage: check_batch_parens.py <file.bat> [<file2.bat> ...]
"""
import re
import sys

# A paren that is NOT preceded by a caret is structural.
UNESCAPED_PAREN = re.compile(r"(?<!\^)[()]")
BLOCK_OPEN = re.compile(r"^\s*if\s+.*\($", re.IGNORECASE)
CONTINUE = re.compile(r"^\s*[a-z]+\s+.*\($", re.IGNORECASE)


def code_of(line):
    """Batch source with comments and quotes removed, caret escapes preserved."""
    text = line.rstrip()
    if text.lstrip().upper().startswith("REM"):
        return ""
    return text.replace('"', "")


def check(path):
    problems = []
    with open(path, encoding="utf-8", errors="replace") as fh:
        lines = fh.read().splitlines()

    depth = 0
    for number, raw in enumerate(lines, 1):
        code = code_of(raw)
        trimmed = code.strip()

        # Inside a block, an unescaped paren is structural - that is the bug.
        # "^(" and "^)" are deliberate escapes and are fine.
        if depth > 0 and trimmed.lower().startswith("echo"):
            if UNESCAPED_PAREN.search(code):
                problems.append((number, trimmed))

        if depth > 0:
            if trimmed.startswith(")"):
                depth -= 1
        elif BLOCK_OPEN.match(code) or CONTINUE.match(code):
            depth = 1

    return problems


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    failed = False
    for path in sys.argv[1:]:
        problems = check(path)
        if problems:
            failed = True
            print(f"FAIL {path}: unescaped parentheses inside if-block(s)")
            for number, text in problems:
                print(f"     line {number}: {text}")
        else:
            print(f"ok   {path}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())