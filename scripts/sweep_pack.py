#!/usr/bin/env python3
"""Sweep many plausible path layouts against a pack in one pass.

An .rsdk pack stores MD5 keys, not filenames, so the only way to find out what is inside
is to guess names and ask. Guessing them one at a time is how you conclude "the stages
aren't in there" when really you guessed the wrong folder layout - so this does the
guessing systematically and prints the whole grid.

Usage: sweep_pack.py <pack.rsdk>
"""
import hashlib
import struct
import sys

SIGNATURE = b"RSDKvB"


def engine_key(archive_path):
    digest = hashlib.md5(archive_path.lower().encode()).digest()
    return b"".join(digest[i:i + 4][::-1] for i in range(0, 16, 4))


def read_keys(path):
    with open(path, "rb") as fh:
        head = fh.read(8)
        if head[:6] != SIGNATURE:
            return None, None, "bad signature %r (not an RSDKvB pack)" % head[:6]
        count = struct.unpack_from("<H", head, 6)[0]
        body = fh.read(count * 24)
    keys = {body[i * 24:i * 24 + 16] for i in range(count)}
    return count, keys, None


# Anchors: paths we are confident about, to establish the prefix convention.
ANCHORS = [
    "data/game/gameconfig.bin",
    "data/sprites/global/display.gif",
    "data/animations/sonic.ani",
    "data/scripts/global/stagesetup.txt",
]

# Layout axes worth sweeping.
ROOTS = ["data/", "Data/", "", "data/game/", "assets/"]
STAGE_DIRS = ["stages/", "levels/", "scenes/", ""]
STAGE_NAMES = ["ghzs1", "ghz", "greenhill", "green_hill", "ghz"]
STAGE_FILES = ["stageconfig.bin", "backgrounds.bin", "objects.bin", "tiles.bin"]
BYTECODE_DIRS = ["bytecode/", "Bytecode/", "scripts/bytecode/", "game/bytecode/"]
BYTECODE_NAMES = ["ghzs1", "ghz", "zoneghz", "globalcode", "global", "specialzone"]


def main():
    pack = sys.argv[1]
    count, keys, err = read_keys(pack)
    if err:
        print("pack: %s" % pack)
        print("FAIL: %s" % err)
        return 2
    print("pack: %s  (fileCount %d)" % (pack, count))

    hits = []

    def probe(p):
        ok = engine_key(p) in keys
        if ok:
            hits.append(p)
        return ok

    print("\n-- anchors (calibration: which prefixes does this pack use?) --")
    for a in ANCHORS:
        print("  %-4s %s" % ("HIT" if probe(a) else "miss", a))
    if not hits:
        print("\nNo anchor hit. Cannot calibrate; refusing to conclude anything about")
        print("stages or bytecode, because an uncalibrated probe proves nothing.")
        return 1

    print("\n-- stage paths --")
    n = 0
    for r in ROOTS:
        for d in STAGE_DIRS:
            for s in STAGE_NAMES:
                for f in STAGE_FILES:
                    n += 1
                    p = r + d + s + "/" + f
                    if probe(p):
                        print("  HIT  %s" % p)
    print("  tried %d stage paths" % n)

    print("\n-- bytecode paths --")
    n = 0
    for r in ROOTS:
        for d in BYTECODE_DIRS:
            for b in BYTECODE_NAMES:
                n += 1
                p = r + d + b + ".bin"
                if probe(p):
                    print("  HIT  %s" % p)
    print("  tried %d bytecode paths" % n)

    print("\ntotal hits: %d of %d probed" % (len(hits), len(ANCHORS) + n))
    return 0


if __name__ == "__main__":
    sys.exit(main())