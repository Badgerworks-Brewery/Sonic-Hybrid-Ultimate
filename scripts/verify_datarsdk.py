#!/usr/bin/env python3
"""Verify an RSDKv4 data pack the same way the engine reads it.

Independent of the C# packer: parses the archive from raw bytes and re-derives
the lookup key the engine would use (MD5 of the lowercased archive path, with
each 32-bit word stored little-endian).

Usage: verify_datarsdk.py <Data.rsdk> [<output-dir>]

Exits non-zero with a description of the first problem found.
"""
import hashlib
import os
import struct
import sys

SIGNATURE = b"RSDKvB"

# Paths the engine asks for by name during a normal boot into Sonic 1's Green Hill
# Zone Act 1. The stage folder carries its game in the name - GHZS1, not ZoneGHZ -
# because both games' zones have to be told apart, and this list still pointed at the
# old name. That went unnoticed while stale ZoneGHZ copies happened to be lying in the
# output folder from before the rename; it surfaced only when the folder was rebuilt
# from scratch, which is a fair argument for keeping the generated tree disposable.
REQUIRED = [
    "data/game/gameconfig.bin",
    "data/stages/ghzs1/backgrounds.bin",
    "data/stages/ghzs1/stageconfig.bin",
    "data/scripts/ghz/ghzsetup.txt",
    "data/scripts/global/stagesetup.txt",
    "data/scripts/players/playerobject.txt",
    "data/sprites/global/display.gif",
    "data/animations/sonic.ani",
]


def engine_key(archive_path):
    """Mirror of the engine's lookup: MD5 of the lowercased path, LE words."""
    digest = hashlib.md5(archive_path.lower().encode()).digest()
    return b"".join(
        digest[i:i + 4][::-1] for i in range(0, 16, 4)
    )


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    path = sys.argv[1]
    out_dir = sys.argv[2] if len(sys.argv) > 2 else None

    with open(path, "rb") as fh:
        data = fh.read()

    if data[:6] != SIGNATURE:
        print(f"FAIL: bad signature {data[:6]!r}, expected {SIGNATURE!r}")
        return 1

    count = struct.unpack_from("<H", data, 6)[0]
    header_end = 8 + count * 24
    print(f"signature : {SIGNATURE.decode()}")
    print(f"fileCount : {count}")
    print(f"header end: {header_end}")

    index = {}
    data_end = header_end
    for i in range(count):
        off = 8 + i * 24
        key = data[off:off + 16]
        file_off, size = struct.unpack_from("<II", data, off + 16)
        if key in index:
            print(f"FAIL: duplicate hash entry at index {i} -> {key.hex()}")
            return 1
        index[key] = (file_off, size)
        data_end = max(data_end, file_off + size)

    if data_end > len(data):
        print(f"FAIL: entry points past end of file ({data_end} > {len(data)})")
        return 1
    if data_end < len(data):
        print(f"FAIL: {len(data) - data_end} trailing byte(s) after last entry")
        return 1

    print(f"data end  : {data_end} (file size {len(data)})")

    missing = [p for p in REQUIRED if engine_key(p) not in index]
    if missing:
        print(f"FAIL: {len(missing)} path(s) the engine needs would not resolve:")
        for p in missing:
            print(f"    - {p}")
        return 1
    print(f"required  : all {len(REQUIRED)} engine lookup paths resolve")

    # Byte-for-byte comparison against the on-disk sources, when available.
    if out_dir:
        sources = [(os.path.join(out_dir, "Data"), "data"),
                   (os.path.join(out_dir, "Scripts"), "data/scripts")]
        checked = corrupted = 0
        for directory, prefix in sources:
            if not os.path.isdir(directory):
                continue
            for root, _, files in os.walk(directory):
                for name in files:
                    full = os.path.join(root, name)
                    rel = os.path.relpath(full, directory).replace("\\", "/")
                    archive_path = (prefix + "/" + rel if prefix else rel).lower()
                    entry = index.get(engine_key(archive_path))
                    if not entry:
                        continue
                    off, size = entry
                    with open(full, "rb") as fh:
                        blob = fh.read()
                    if len(blob) != size or data[off:off + size] != blob:
                        print(f"FAIL: payload mismatch for {archive_path}")
                        corrupted += 1
                    else:
                        checked += 1
        print(f"payloads  : {checked} byte-exact, {corrupted} corrupt")
        if corrupted:
            return 1

    print("OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())