#!/usr/bin/env python3
"""Ask an RSDKv4 pack whether it contains specific paths, without extracting it.

An .rsdk pack stores MD5 keys, not filenames, so you cannot list its contents. You can
only ask "do you have this exact path?". That is enough to answer the questions worth
asking - "does Sonic CD.rsdk carry the CD bytecode we are missing?" - and it answers
them without unpacking 78 MB into the working tree.

Usage: probe_pack.py <pack.rsdk> [--prefix data/bytecode/] <path> [path ...]
       probe_pack.py <pack.rsdk> --zones RABCD EF      (generate CD-style zone names)

Exit 0 if every probed path was found, 1 if any was missing, 2 on a bad pack.
"""
import hashlib
import struct
import sys

SIGNATURE = b"RSDKvB"


def engine_key(archive_path):
    """Mirror of the engine's lookup: MD5 of the lowercased path, LE words."""
    digest = hashlib.md5(archive_path.lower().encode()).digest()
    return b"".join(digest[i:i + 4][::-1] for i in range(0, 16, 4))


def read_keys(path):
    with open(path, "rb") as fh:
        head = fh.read(8)
        if head[:6] != SIGNATURE:
            raise ValueError("bad signature %r" % head[:6])
        count = struct.unpack_from("<H", head, 6)[0]
        body = fh.read(count * 24)
    keys = set()
    for i in range(count):
        off = i * 24
        keys.add(body[off:off + 16])
    return count, keys


def cd_zone_names(letters, suffix="S"):
    """Generate <Zone><Act><Act><Stage>.bin names, the shape CD stages use.

    Zone is one letter, then two act letters (A1 -> AA, A2 -> AB, B1 -> BA, C1 -> CA),
    then a trailing discriminator shared by the 1/2 games.
    """
    out = []
    for z in letters:
        for acts in ("AA", "AB", "BA", "CA"):
            out.append("Zone%s%s%s.bin" % (z, acts, suffix))
    return out


def main():
    args = [a for a in sys.argv[1:]]
    if not args:
        print(__doc__)
        return 2

    pack = args.pop(0)
    prefix = "data/bytecode/"
    paths = []
    while args:
        a = args.pop(0)
        if a == "--prefix":
            prefix = args.pop(0)
        elif a == "--zones":
            letters = args.pop(0).replace(" ", "")
            paths.extend(prefix + n for n in cd_zone_names(letters))
        else:
            paths.append(a)

    try:
        count, keys = read_keys(pack)
    except (IOError, OSError, ValueError) as exc:
        print("FAIL: %s" % exc)
        return 2

    print("pack     : %s" % pack)
    print("fileCount: %d" % count)
    print("probing  : %d paths" % len(paths))

    missing = []
    for p in paths:
        if engine_key(p) not in keys:
            missing.append(p)

    for p in missing[:40]:
        print("  MISSING  %s" % p)
    if len(missing) > 40:
        print("  ... and %d more missing" % (len(missing) - 40))
    print("found %d of %d" % (len(paths) - len(missing), len(paths)))
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())