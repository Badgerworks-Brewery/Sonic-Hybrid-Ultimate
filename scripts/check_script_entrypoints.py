#!/usr/bin/env python3
"""Check that every stage in the hybrid has object logic that will actually run.

RSDKv4 runs an object through exactly three entry points, set only by:

  * text parsing, on `eventObjectUpdate` / `eventObjectDraw` / `eventObjectStartup`
    (Script.cpp:2848-2864)
  * loaded bytecode (Script.cpp:3191-3209)

On startup all three point at a sentinel (Script.cpp:3318-3325) and Object.cpp
guards every call with `scriptCode[...scriptCodePtr] > 0`, so an object with
neither has no behaviour and the engine reports nothing.

Which path is taken is decided per stage load by whether
`Bytecode/GlobalCode.bin` resolves (Scene.cpp:675). That is one global file, so
it is all-or-nothing:

  * absent  -> every stage loads text scripts
  * present -> every stage loads `Bytecode/<stage folder>.bin`

So the question is not "do the text scripts parse" but "does every stage resolve
to script bytecode". A stage with no bytecode file while the bytecode path is
active gets no object logic at all - the same silent failure.
"""

import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "Hybrid-RSDK-Main", "sonic-hybrid", "Data")
BYTECODE = os.path.join(DATA, "Bytecode")
STAGES = os.path.join(DATA, "Stages")
MARKERS = ("eventobjectupdate", "eventobjectdraw", "eventobjectstartup")


def text_script_entrypoints():
    """Count text scripts that declare an RSDKv4 entry point."""
    scripts = os.path.join(ROOT, "Hybrid-RSDK-Main", "sonic-hybrid", "Scripts")
    total = with_entry = 0
    for dirpath, _dirs, files in os.walk(scripts):
        for name in files:
            if not name.lower().endswith(".txt"):
                continue
            total += 1
            path = os.path.join(dirpath, name)
            try:
                with open(path, "r", encoding="utf-8", errors="replace") as fh:
                    text = fh.read().lower()
            except OSError:
                continue
            if any(m in text for m in MARKERS):
                with_entry += 1
    return total, with_entry


def main():
    bytecode_active = os.path.exists(os.path.join(BYTECODE, "GlobalCode.bin"))

    total_txt, with_entry = text_script_entrypoints()
    print("text scripts: %d, declaring an entry point: %d" % (total_txt, with_entry))
    print("bytecode path: %s" % ("active" if bytecode_active else "inactive"))

    stages = []
    if os.path.isdir(STAGES):
        stages = sorted(n for n in os.listdir(STAGES)
                        if os.path.isdir(os.path.join(STAGES, n)))

    if not bytecode_active:
        # Text path. Every stage depends on the text scripts having entry points.
        print()
        print("FAIL: no Data/Bytecode/GlobalCode.bin, so every stage loads text")
        print("      scripts, and %d of %d declare an entry point." % (with_entry, total_txt))
        return 1

    print()
    if with_entry == 0:
        print("note: the text scripts are inert - they are not loaded while the")
        print("      bytecode path is active, so their missing entry points do not")
        print("      matter right now.")

    missing = []
    for stage in stages:
        if not os.path.exists(os.path.join(BYTECODE, stage + ".bin")):
            missing.append(stage)

    have = len(stages) - len(missing)
    print("stages: %d, with bytecode: %d, without: %d" % (len(stages), have, len(missing)))

    if not missing:
        print("OK: every stage resolves to script bytecode")
        return 0

    print()
    print("FAIL: %d stage(s) have no bytecode while the bytecode path is active," % len(missing))
    print("      so their objects spawn with no Main, Draw or Startup. These stages")
    print("      load their background and nothing else. See docs/rsdkv3-decompiler.md,")
    print("      'Why no object logic runs in ANY of the three games'.")
    print()
    for stage in missing:
        print("      " + stage)
    return 1


if __name__ == "__main__":
    sys.exit(main())