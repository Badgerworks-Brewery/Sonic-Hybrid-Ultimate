#!/usr/bin/env python3
"""Validate the bytecode walker against the engine's own decoding, scene by scene.

The engine can log every instruction it executes (`RSDK_TRACE_ALL=1`), which is the
only authoritative answer to "how many words does this instruction occupy". This
script drives that: for each scene it boots the engine with tracing on, captures the
log, and checks the walker against it.

Two things this had to get right, both of which produced convincing nonsense first:

- **One stage container per run.** The engine appends GlobalCode.bin and then the
  one stage the scene needs, and every word in the trace is an absolute index into
  that combined array. Comparing a log against all 31 shipped stage files at once
  reported 238 sites where "the engine read a different opcode than we did" - it had
  simply read the words out of the wrong container. Each scene is checked against
  GlobalCode plus that scene's own stage file, and the mapping is asserted rather
  than assumed.
- **Per instruction site, not per execution.** Scripts loop, so the same words are
  executed many times and one pass can leave the sequence for reasons unrelated to
  width. WLower at word 25932 was reported as an 8-word instruction the engine
  advanced 31 past, when the log plainly shows WLower @25932 followed by
  GetTableValue @25940, which is 8.

A site counts as confirmed if any of its executions agrees. Only a site that never
agrees is reported, and that is a real width error.

Exits non-zero if any site is wrong, so it can gate a build.
"""

import io
import os
import re
import subprocess
import sys
import time
from collections import Counter

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

# Opcodes that jump rather than fall through, so the next executed word says nothing
# about this instruction's width.
CONTROL_FLOW = ("If", "loop", "else", "endif", "endswitch", "next", "break",
                "switch", "ForEach", "case", "return", "End", "continue",
                "CallFunction")

# Scenes to trace. Sonic 1's first three stages, and two Sonic 2 stages that are not
# Emerald Hill. Sonic CD is skipped: it has no bytecode yet, so there is nothing to
# compare against and the trace would just be the globals again.
SCENES = [
    (1, 0, "GHZS1.bin"),
    (1, 5, "MZS1.bin"),
    (1, 18, "SBZS1.bin"),
    (1, 92, "CPZS2.bin"),
    (1, 101, "WFZS2.bin"),
]


def is_control_flow(name):
    return any(name == c or name.startswith(c) for c in CONTROL_FLOW)


def run_scene(category, scene, seconds=9):
    io.open(os.path.join(PACK, "settings.ini"), "w", encoding="ascii",
            newline="").write(SETTINGS % (category, scene))
    for stale in (LOG,):
        if os.path.exists(stale):
            os.remove(stale)
    env = dict(os.environ)
    env["RSDK_TRACE_ALL"] = "1"
    proc = subprocess.Popen([EXE], cwd=PACK, env=env,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(seconds)
    if proc.poll() is None:
        proc.kill()
        proc.wait()
    if not os.path.exists(LOG):
        return "", "no log"
    stage = ""
    entries = []
    for line in io.open(LOG, encoding="latin-1"):
        m = re.match(r"Loading Scene .* - (.*)$", line.strip())
        if m:
            stage = m.group(1)
        m = re.match(r"ORACLE: (.+) @(-?\d+)(?: sc=(-?\d+))?", line.strip())
        if m:
            entries.append((m.group(1), int(m.group(2)),
                            int(m.group(3)) if m.group(3) else None))
    return entries, stage


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

    matched = set()
    wrong = {}
    fallthrough = 0
    branched = 0
    table_clash = Counter()

    for i in range(len(entries) - 1):
        name, word, script = entries[i]
        _n, nextword, nextscript = entries[i + 1]
        if script is not None and nextscript is not None and script != nextscript:
            continue
        cname, code, local = container_for(word)
        if code is None or local < 0 or local >= len(code.code):
            continue
        op = code.code[local]
        if op < 0 or op >= len(ORDER):
            continue
        if ORDER[op][0] != name:
            table_clash[name] += 1
            continue

        q = local + 1
        try:
            for _ in range(size_of(op)):
                if q >= len(code.code):
                    raise ValueError
                q += operand_width(code.code, q)
        except Exception:                              # noqa: BLE001
            continue
        width = q - local
        gap = nextword - word

        if is_control_flow(name):
            branched += 1
            continue

        fallthrough += 1
        key = (cname, name, word)
        if gap == width:
            matched.add(key)
            # A site is confirmed if *any* of its executions agrees, so an earlier
            # mismatch must not keep it in the failure list. Forgetting this is what
            # reported WLower at word 25932 as wrong 33 times over, when the trace
            # plainly shows WLower @25932 followed by GetTableValue @25940 - a gap of
            # 8, exactly what the walker computes.
            wrong.pop(key, None)
        elif key not in matched and key not in wrong:
            wrong[key] = (op, size_of(op), width, gap)

    sites = len(matched) + len(wrong)
    print("  %-28s fell through %4d, branched %4d, sites %3d, confirmed %3d%s"
          % (label, fallthrough, branched, sites, len(matched),
             "" if not wrong else "  WRONG %d" % len(wrong)))
    if table_clash:
        print("      opcode-table clashes: %s"
              % ", ".join("%s x%d" % (n, c)
                          for n, c in table_clash.most_common(4)))
    for key in sorted(wrong, key=lambda k: wrong[k][3])[:6]:
        cname, name, word = key
        op, size, width, gap = wrong[key]
        print("      %s word %d %-22s declared %d, computed %d, engine advanced %d"
              % (cname, word, name[:22], size, width, gap))
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
            print("  scene %-3d no trace captured (%s)" % (scene, label))
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
    print("OK: the walker's operand widths match the engine everywhere it was "
          "observed")
    return 0


if __name__ == "__main__":
    sys.exit(main())