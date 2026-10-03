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

It must also carry DisableFocusPause=1. With no window to take focus on this
machine, the engine sees hasFocus=0 and pauses the stage, and a run then stops dead
a few frames in. That looks exactly like a broken stage - it reported 20 frames and
308 live entities, with nothing wrong with the stage at all.
"""

import io
import os
import re
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rsdk_settings  # noqa: E402

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

# Any stage in the merged regular list can be probed by index, which is how a stage
# is selected when it is not one of the defaults above. The index is the engine's
# own StartingScene value, so `python scripts/probe_stages.py --scene 92` boots
# Chemical Plant Zone Act 2.
#
# This exists because a stage was requested by name that appears nowhere in the
# shipped data: "EST" was searched for as a standalone token across all 1631 config
# and stage files of Sonic 1, Sonic 2 and Sonic CD and does not occur once, and the
# engine's stage list has no world field at all (Scene.hpp's SceneInfo is
# name/folder/id). So rather than hard-code a guess, selection is a parameter.
EXTRA_SCENES = []


RUN_SECONDS = 14


def probe(idx):
    """Boot one stage and return what the log shows."""
    settings = os.path.join(WD, "settings.ini")
    log = os.path.join(WD, "log.txt")
    # Only the log is cleared. settings.ini is *merged*, not rewritten: it is a tracked
    # file, and the template this used to write from dropped every section it did not
    # know about - including DataFile=Data.rsdk, which is how the engine finds the pack.
    # The tests still passed afterwards, because the engine falls back to the pack
    # beside it, and the damage only surfaced as a confusing diff in the next commit.
    if os.path.exists(log):
        os.remove(log)

    rsdk_settings.write_settings(WD, 1, idx)

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

    probes = list(PROBES) + list(EXTRA_SCENES)

    # `--scene N` probes one stage by its index in the merged regular list, which is
    # the engine's own StartingScene value. That is how a stage is selected when it
    # is not in the default set, rather than editing this file each time.
    args = sys.argv[1:]
    while args:
        flag = args.pop(0)
        if flag in ("--scene", "-s") and args:
            try:
                idx = int(args.pop(0))
            except ValueError:
                sys.stderr.write("--scene wants a number\n")
                return 2
            probes = [(idx, "scene %d" % idx)]
        elif flag in ("--help", "-h"):
            print(__doc__)
            print("usage: probe_stages.py [--scene N]")
            return 0
        else:
            sys.stderr.write("unknown option %r\n" % flag)
            return 2

    print("%4s  %-38s %-7s %-10s %-7s %s" %
          ("idx", "stage", "loaded", "bytecode", "objects", "frames"))
    print("-" * 92)

    rows = []
    for idx, label in probes:
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