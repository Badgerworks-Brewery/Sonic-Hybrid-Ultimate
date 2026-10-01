#!/usr/bin/env python3
"""Check that every shipped object script declares an RSDKv4 entry point.

RSDKv4 runs an object through exactly three entry points. On startup they all
point at a sentinel (Script.cpp:3318-3325), and only two things replace it:

  * text parsing, when the script contains `eventObjectUpdate`,
    `eventObjectDraw` or `eventObjectStartup`  (Script.cpp:2848-2864)
  * loading bytecode                            (Script.cpp:3191-3209)

Object.cpp guards each call with `scriptCode[...scriptCodePtr] > 0`, so a script
with none of those markers leaves the object with no behaviour at all - it will
spawn, draw nothing and do nothing, and the engine will not complain.

The `function <Name> ... end function` dialect used by the tracked Sonic 1 and
Sonic 2 scripts is not an entry point. Those functions are only reachable through
CallFunction, so a stage built from them loads its background and nothing else.

This check exists because that is the exact symptom the project shipped with, and
nothing in the build reported it: the scripts parse cleanly, the pack resolves,
and every stage still "loads".
"""

import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT_DIRS = [os.path.join(ROOT, "Hybrid-RSDK-Main", "sonic-hybrid", "Scripts")]

MARKERS = ("eventObjectUpdate", "eventObjectDraw", "eventObjectStartup")


def main():
    files = []
    for base in SCRIPT_DIRS:
        for dirpath, _dirnames, filenames in os.walk(base):
            for name in filenames:
                if name.lower().endswith(".txt"):
                    files.append(os.path.join(dirpath, name))

    if not files:
        sys.stderr.write("no script files found; check the path\n")
        return 1

    with_entry = []
    without = []

    for path in files:
        try:
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                text = fh.read()
        except OSError as exc:
            sys.stderr.write("could not read %s: %s\n" % (path, exc))
            return 1

        low = text.lower()
        if any(m in low for m in MARKERS):
            with_entry.append(path)
        else:
            without.append(path)

    rel = lambda p: os.path.relpath(p, ROOT)
    print("object scripts: %d" % len(files))
    print("  with an RSDKv4 entry point : %d" % len(with_entry))
    print("  without one                : %d" % len(without))

    if not without:
        print("OK: every script declares eventObjectUpdate/Draw/Startup")
        return 0

    print()
    print("FAIL: %d script(s) declare no entry point, so the engine never runs" % len(without))
    print("      them. Objects spawn with no Main, Draw or Startup. See")
    print("      docs/rsdkv3-decompiler.md, 'Why no object logic runs in ANY of")
    print("      the three games'.")
    print()
    for path in sorted(without)[:10]:
        print("      " + rel(path))
    if len(without) > 10:
        print("      ... and %d more" % (len(without) - 10))
    return 1


if __name__ == "__main__":
    sys.exit(main())