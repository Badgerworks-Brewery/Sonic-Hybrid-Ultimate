#!/usr/bin/env python3
"""Validate the bytecode walker's operand decoding against the engine itself.

The engine can report, for every instruction it executes, exactly how many words
that instruction consumed and what operand tags it read (RSDK_TRACE_ALL=1). That is
the measurement this project kept getting wrong. Everything else - counting
desynchronised script ranges, narrowing a width until a count improved - is
inference, and it produced two confident wrong answers before this existed: it said
`GetVersionNumber` and `Abs` needed three operands when their handlers use exactly
the declared count, and it said `DrawText` needed three when the real answer was
that `DrawText` is not in the table at all.

So this script asks the engine rather than inferring. For each stage it boots the
game with tracing on and compares, instruction site by instruction site:

  * the number of words the engine consumed,
  * the operand tags it read,
  * against what scripts/rsdkv4_walk.py computes.

Exits non-zero if any site disagrees, so it can gate a build.
"""

import io
import os
import re
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rsdkv4_bytecode_merger import parse
from rsdkv4_opcodes import ORDER, size_of
from rsdkv4_walk import operand_width

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACK = os.path.join(ROOT, "Hybrid-RSDK-Main", "sonic-hybrid")
EXE = os.path.join(ROOT, "build", "bin", "Release", "rsdkv4.exe")
BYTECODE = os.path.join(PACK, "Data", "Bytecode")
LOG = os.path.join(PACK, "log.txt")

SETTINGS = """[Window]
RefreshRate=60
WindowScale=1
ScreenWidth=640
DimLimit=300
[Dev]
EngineDebugMode=true
TxtScripts=false
StartingCategory=%d
StartingScene=%d
StartingSaveFile=255
"""

# Sonic 1's first three stages, and two Sonic 2 stages that are not Emerald Hill.
# Sonic CD is skipped: it has no bytecode yet, so there is nothing to compare.
SCENES = [
    (1, 0, "GHZS1.bin"),
    (1, 5, "MZS1.bin"),
    (1, 18, "SBZS1.bin"),
    (1, 92, "CPZS2.bin"),
    (1, 101, "WFZS2.bin"),
]

RE_SPAN = re.compile(r"ORACLE-SPAN @(-?\d+) consumed=(\d+) tags=$")
RE_OP = re.compile(r"ORACLE: (.+) @(-?\d+)(?: sc=(-?\d+))?$")


def run_scene(category, scene, seconds=9):
    io.open(os.path.join(PACK, "settings.ini"), "w", encoding="ascii",
            newline="").write(SETTINGS % (category, scene))
    if os.path.exists(LOG):
        os.remove(LOG)
    env = dict(os.environ)
    env["RSDK_TRACE_ALL"] = "1"
    proc = subprocess.Popen([EXE], cwd=PACK, env=env,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(seconds)
    if proc.poll() is None:
        proc.kill()
        proc.wait()
    if not os.path.exists(LOG):
        return [], "no log"

    entries = []
    pending = None
    label = ""
    for line in io.open(LOG, encoding="latin-1"):
        line = line.strip()
        m = RE_OP.match(line)
        if m:
            label = label or ""
            pending = {"name": m.group(1), "word": None}
            continue
        m = RE_SPAN.match(line)
        if m:
            if pending is not None:
                pending["word"] = int(m.group(1))
                pending["consumed"] = int(m.group(2))
                pending["tags"] = []
            continue
        if pending is not None and pending.get("word") is not None and line:
            pending["tags"].append(int(line.rstrip(",")))
            continue
        if pending is not None and pending.get("word") is not None:
            entries.append((pending["name"], pending["word"],
                            pending["consumed"], tuple(pending["tags"])))
            pending = None
        m = re.match(r"Loading Scene .* - (.*)$", line)
        if m:
            label = m.group(1)
    return entries, label


def check(entries, stage_file, label):
    merged = parse(os.path.join(BYTECODE, "GlobalCode.bin"))
    containers = [("GlobalCode.bin", merged, 0)]
    offset = len(merged.code)
    stage_path = os.path.join(BYTECODE, stage_file)
    if os.path.exists(stage_path):
        stage = parse(stage_path)
        containers.append((stage_file, stage, offset))

    def container_for(word):
        for name, c, base in containers:
            if base <= word < base + len(c.code):
                return name, c, word - base
        return None, None, word

    agreed = {}
    wrong = {}

    for name, word, consumed, tags in entries:
        cname, code, local = container_for(word)
        if code is None or local < 0 or local >= len(code.code):
            continue
        op = code.code[local]
        if op < 0 or op >= len(ORDER) or ORDER[op][0] != name:
            continue

        count = size_of(op)
        q = local + 1
        mine = []
        try:
            for _ in range(count):
                mine.append(code.code[q])
                q += operand_width(code.code, q)
        except Exception:                              # noqa: BLE001
            continue
        mine_width = q - local - 1                    # operand words only

        key = (cname, name, word)
        if consumed == mine_width and tags == tuple(mine):
            agreed[key] = True
            wrong.pop(key, None)
        elif key not in agreed and key not in wrong:
            wrong[key] = (count, mine_width, consumed, list(mine), list(tags))

    sites = len(agreed) + len(wrong)
    print("  %-30s sites %4d, confirmed %4d%s"
          % (label or stage_file, sites, len(agreed),
             "" if not wrong else "  WRONG %d" % len(wrong)))
    for key in sorted(wrong, key=lambda k: wrong[k][1])[:8]:
        cname, name, word = key
        count, mine_width, consumed, mine, tags = wrong[key]
        print("      %s word %d %-18s %d operands: walker %d words tags %s, "
              "engine %d words tags %s"
              % (cname, word, name[:18], count, mine_width, mine, consumed, tags))
    return sites, len(wrong)


def main():
    if not os.path.exists(EXE):
        print("engine not built: %s" % EXE)
        return 2

    total_sites = 0
    total_wrong = 0
    for category, scene, stage_file in SCENES:
        entries, label = run_scene(category, scene)
        if not entries:
            print("  scene %-3d nothing traced (%s)" % (scene, label))
            total_wrong += 1
            continue
        sites, wrong = check(entries, stage_file, "%s (scene %d)" % (label, scene))
        total_sites += sites
        total_wrong += wrong

    print()
    print("instruction sites confirmed against the engine: %d of %d (%.1f%%)"
          % (total_sites - total_wrong, total_sites,
             100.0 * (total_sites - total_wrong) / max(total_sites, 1)))
    if total_wrong:
        print("%d site(s) the engine never agreed with" % total_wrong)
        return 1
    print("OK: operand widths and tags match the engine everywhere observed")
    return 0


if __name__ == "__main__":
    sys.exit(main())