#!/usr/bin/env python3
"""Boot a sample of stages from all three games and report what actually ran.

This exists because "the stage loaded" and "the stage works" are different claims.
RSDKv4 will happily load a stage's background and tiles and report success while
every object in it sits inert, because an object only runs if its script bytecode
resolved. That is exactly the failure this project shipped with for months.

So for each stage this reports:

  loaded    the engine reached "Loading Scene" for it
  bytecode  the stage's Bytecode/<folder>.bin resolved - the thing that decides
            whether objects have any behaviour at all
  objects   how many object types were registered
  frames    how many frames the engine actually ran, which distinguishes "booted
            and is playing" from "stalled"

A stage that loads but has no bytecode is NOT working, and is called out as such.

Note on settings.ini: it must carry [Window] RefreshRate. Without it the frame
loop in RetroEngine::Run() spins without doing any work - no log output, a
pinned core, and nothing that looks like a script bug. This script always writes
a complete file rather than deleting it between runs.
"""

import io
import os
import re
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WD = os.path.join(ROOT, "Hybrid-RSDK-Main", "sonic-hybrid")
EXE = os.path.join(ROOT, "build", "bin", "Release", "rsdkv4.exe")

# Stage indices in the merged StagesRegular list:
#   0  GREEN HILL ZONE 1        (Sonic 1)
#   18 FINAL ZONE               (Sonic 1, last)
#   19 PALMTREE PANIC 1 PRESENT (Sonic CD, first)
#   49 QUARTZ QUADRANT          (Sonic CD)
#   88 METALLIC MADNESS 3 BAD   (Sonic CD, last)
# StartingCategory must be 1 (STAGELIST_REGULAR): 0 is the presentation list and
# InitFirstStage treats 0 as "unset".
#
# Sonic 2 (Emerald Hill, Death Egg) is deliberately excluded. It is the one game
# whose stages are known to work, so including it only dilutes the signal about
# the two that are broken. Sonic 3 cannot be probed at all: it has no stages in
# the merged config and no game data - see the note printed by this script.
PROBES = [
    (0,   "Sonic 1  Green Hill Act 1"),
    (5,   "Sonic 1  Marble Zone Act 1"),
    (18,  "Sonic 1  Final Zone"),
    (19,  "Sonic CD  Palmtree Panic A1 Present"),
    (49,  "Sonic CD  Quartz Quadrant A2 Past"),
    (88,  "Sonic CD  Metallic Madness A3 Bad Future"),
]

SETTINGS = """[Window]
RefreshRate=60
WindowScale=1
ScreenWidth=640
DimLimit=300

[Audio]
BGMVolume=1.000
SFXVolume=1.000

[Dev]
EngineDebugMode=true
TxtScripts=false
StartingCategory={cat}
StartingScene={scene}
StartingSaveFile=255
DataFile=Data.rsdk

[Game]
Language=0
SkipStartMenu=true
"""

RUN_SECONDS = 14


def probe(idx):
    """Boot one stage and return what the log shows."""
    settings = os.path.join(WD, "settings.ini")
    log = os.path.join(WD, "log.txt")
    for path in (settings, log):
        if os.path.exists(path):
            os.remove(path)

    io.open(settings, "w", encoding="ascii").write(SETTINGS.format(cat=1, scene=idx))

    proc = subprocess.Popen([EXE], cwd=WD,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(RUN_SECONDS)
    if proc.poll() is None:
        proc.kill()
    proc.wait()
    time.sleep(0.3)

    if not os.path.exists(log):
        return {"loaded": False, "bytecode": 0, "objects": 0, "frames": 0, "scene": ""}

    text = io.open(log, encoding="utf-8", errors="replace").read()

    frames = 0
    m = re.findall(r"ProcessObjects frame (\d+)", text)
    if m:
        frames = max(int(x) for x in m)

    missing = re.findall(r"Couldn't load file '([^']+)'", text)

    # The decisive signal is whether *this stage's own* bytecode resolved. A
    # missing "Couldn't load file 'Bytecode/<folder>.bin'" says so directly, and
    # it is more reliable than counting successes: GlobalCode.bin is loaded for
    # every stage, so a naive count looks healthy even when the stage's scripts
    # are absent.
    stage_bc_missing = [p for p in missing
                        if p.startswith("Bytecode/")
                        and not p.endswith("GlobalCode.bin")]

    return {
        "loaded": "Loading Scene" in text,
        "bytecode_ok": not stage_bc_missing,
        "objects": len(re.findall(r"^Set Object", text, re.M)),
        "frames": frames,
        "scene": next((l.strip() for l in text.splitlines() if "Loading Scene" in l), ""),
        "missing": missing,
        "stage_bc_missing": stage_bc_missing,
    }


def main():
    if not os.path.exists(EXE):
        sys.stderr.write("engine not built: %s\n" % EXE)
        return 2

    print("%4s  %-38s %-7s %-10s %-7s %s" %
          ("idx", "stage", "loaded", "bytecode", "objects", "frames"))
    print("-" * 92)

    rows = []
    for idx, label in PROBES:
        r = probe(idx)
        print("%4d  %-38s %-7s %-10s %-7d %s" %
              (idx, label,
               "YES" if r["loaded"] else "NO",
               "YES" if r["bytecode_ok"] else "MISSING",
               r["objects"], r["frames"]))
        rows.append((idx, label, r))

    print()
    print("per-stage verdict")
    print("-" * 92)

    worked = []
    for idx, label, r in rows:
        if not r["loaded"]:
            verdict = "FAILED TO LOAD"
        elif not r["bytecode_ok"]:
            verdict = "loads, but its bytecode is MISSING -> objects are inert"
        elif r["frames"] < 30:
            verdict = "has bytecode, but only ran %d frames" % r["frames"]
        else:
            verdict = "OK - loads, has bytecode, ran %d frames" % r["frames"]
            worked.append(label)
        print("  %-40s %s" % (label, verdict))
        for miss in r["stage_bc_missing"][:2]:
            print("      could not load: %s" % miss)

    print()
    print("stages with working object logic: %d of %d" % (len(worked), len(rows)))
    for label in worked:
        print("  + " + label)

    # Labels are "Sonic 1"/"Sonic CD"/"Sonic 2", so split on the first token only.
    games = {}
    for label in worked:
        game = label.split("  ")[0].strip()
        games[game] = games.get(game, 0) + 1

    probed_games = []
    for _, label in PROBES:
        game = label.split("  ")[0].strip()
        if game not in probed_games:
            probed_games.append(game)

    print()
    for game in probed_games:
        count = games.get(game, 0)
        total = sum(1 for _, l in PROBES if l.split("  ")[0].strip() == game)
        if count:
            print("  %-8s %d of %d probed stages have object logic" % (game, count, total))
        else:
            print("  %-8s NO probed stage has object logic" % game)

    print()
    print("Sonic 3: NOT PROBED - no game data and no stages in the merged config.")
    print("  rsdk-source-data holds sonic1, sonic2 and soniccd only. The only S3")
    print("  artefact is a 4 MB rsdk-source-data/sonic3.bin, which is untracked,")
    print("  contains no AIR signatures or Data/ paths, and is not the ROM S3AIR")
    print("  needs - SONIC3_AIR_SETUP.md requires a user-supplied Sonic 3 & Knuckles")
    print("  ROM plus a sonic3air.exe. Neither is present, so there is nothing to boot.")

    incomplete = [g for g in probed_games if g not in games]
    if incomplete:
        print()
        print("games with NO working stage among those probed: " + ", ".join(incomplete))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())