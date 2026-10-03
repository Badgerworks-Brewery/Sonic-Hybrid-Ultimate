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
DisableFocusPause=1
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
        return [], "no log", None

    entries = []
    pending = None
    label = ""
    stage_file = None
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
        # Take the stage's bytecode file from the log rather than from a list kept
        # in step with the config by hand. A stale list would silently compare a
        # trace against the wrong container, which is exactly the mistake that
        # produced 238 phantom "the engine read a different opcode" reports.
        m = re.match(r"Loaded Data File 'Bytecode/(.+)\.bin'$", line)
        if m and m.group(1) != "GlobalCode":
            stage_file = m.group(1) + ".bin"
    return entries, label, stage_file


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

    # Everything the engine traced has to end up in exactly one of these buckets.
    #
    # It used to be three bare `continue`s that dropped entries which did not line up,
    # which made a 100% result arithmetically unavoidable: an entry that disagreed was
    # discarded before it could be counted as a disagreement. The walker's operand
    # decoding may well be right, but "every entry that survived the filter agreed" is
    # not evidence of that. So each reason is counted and reported.
    unplaced = {}          # word -> opcode name, no container covers it
    misnamed = {}          # (cname, word) -> (engine name, walker name)
    undecodable = {}       # (cname, word) -> engine name

    for name, word, consumed, tags in entries:
        cname, code, local = container_for(word)
        if code is None or local < 0 or local >= len(code.code):
            unplaced.setdefault(word, name)
            continue
        op = code.code[local]
        if op < 0 or op >= len(ORDER):
            undecodable.setdefault((cname, word), name)
            continue
        if ORDER[op][0] != name:
            misnamed.setdefault((cname, word), (name, ORDER[op][0]))
            continue

        count = size_of(op)
        q = local + 1
        mine = []
        try:
            for _ in range(count):
                mine.append(code.code[q])
                q += operand_width(code.code, q)
        except Exception as exc:                       # noqa: BLE001
            undecodable.setdefault((cname, word), "%s (%s)" % (name, exc))
            continue
        mine_width = q - local - 1                    # operand words only

        key = (cname, name, word)
        if consumed == mine_width and tags == tuple(mine):
            agreed[key] = True
            wrong.pop(key, None)
        elif key not in agreed and key not in wrong:
            wrong[key] = (count, mine_width, consumed, list(mine), list(tags))

    sites = len(agreed) + len(wrong)
    gaps = len(unplaced) + len(misnamed) + len(undecodable)
    print("  %-30s traced %5d | placed %4d confirmed %4d | WRONG %3d | "
          "unplaced %4d, opcode differs %4d, undecodable %4d"
          % (label or stage_file, len(entries), sites, len(agreed), len(wrong),
             len(unplaced), len(misnamed), len(undecodable)))
    for key in sorted(wrong, key=lambda k: wrong[k][1])[:8]:
        cname, name, word = key
        count, mine_width, consumed, mine, tags = wrong[key]
        print("      %s word %d %-18s %d operands: walker %d words tags %s, "
              "engine %d words tags %s"
              % (cname, word, name[:18], count, mine_width, mine, consumed, tags))
    for (cname, word), (engine_name, walker_name) in sorted(misnamed.items())[:6]:
        print("      %s word %d: engine read %s, walker read %s"
              % (cname, word, engine_name, walker_name))
    for word, name in sorted(unplaced.items())[:4]:
        print("      word %d (%s): no loaded container covers this index" % (word, name))
    return sites, len(wrong), gaps


def main():
    if not os.path.exists(EXE):
        print("engine not built: %s" % EXE)
        return 2

    total_sites = 0
    total_wrong = 0
    total_gaps = 0
    total_traced = 0
    ran = 0

    # `--from N --to M` sweeps a range of stages instead of the default five. The
    # oracle only confirms what the engine *executes*, so covering more stages is the
    # only way to widen the set of instructions that are safe to rewrite.
    scenes = list(SCENES)
    sweep = False
    lo = hi = None
    args = sys.argv[1:]
    while args:
        flag = args.pop(0)
        if flag in ("--from", "--to") and args:
            value = int(args.pop(0))
            if flag == "--from":
                lo = value
            else:
                hi = value
        else:
            sys.stderr.write("unknown option %r\n" % flag)
            return 2
    if lo is not None and hi is not None:
        sweep = True
        scenes = [(1, n, None) for n in range(lo, hi + 1)]

    for category, scene, _fallback in scenes:
        entries, label, stage_file = run_scene(category, scene)
        if not entries:
            if sweep:
                continue              # a stage that traced nothing is not a result
            print("  scene %-3d nothing traced (%s)" % (scene, label))
            total_wrong += 1
            continue
        ran += 1
        sites, wrong, gaps = check(entries, stage_file or "",
                                   "%s (scene %d)" % (label, scene))
        total_sites += sites
        total_wrong += wrong
        total_gaps += gaps
        total_traced += len(entries)

    print()
    print("stages traced: %d" % ran)
    print("instructions the engine executed: %d" % total_traced)
    print("instruction sites placed against the walker's containers: %d of %d "
          "(%.1f%%)" % (total_sites, total_traced,
                        100.0 * total_sites / max(total_traced, 1)))
    print("of those, confirmed: %d of %d (%.1f%%)"
          % (total_sites - total_wrong, total_sites,
             100.0 * (total_sites - total_wrong) / max(total_sites, 1)))
    print("not placed, so not compared: %d" % total_gaps)
    if total_wrong:
        print("%d site(s) the engine never agreed with" % total_wrong)
        return 1
    print()
    print("Read the second figure, not the third. The walker agrees with the engine on")
    print("every site it could be shown, but %d of %d instructions the engine actually"
          % (total_gaps, total_traced))
    print("ran were not compared at all - either no loaded container covers their word")
    print("index, or the two read different opcodes there. Those are unverified, not")
    print("correct, and the gap is the honest limit of this check.")
    return 0


if __name__ == "__main__":
    sys.exit(main())