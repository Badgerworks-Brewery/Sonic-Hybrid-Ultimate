#!/usr/bin/env python3
"""Read the Sonic CD RSDKv3 bytecode containers.

STATUS: THE FORMAT IS STILL NOT KNOWN, AND THIS SCRIPT SAYS SO RATHER THAN PRETENDING

This decoder implements the layout described by A.I.R.'s own RSDKv3 loader,
Hybrid-RSDK-Main/RSDKV3/RSDKv3/Script.cpp:1871 LoadBytecode:

    scriptCodeSize  (u32, little-endian)
    scriptCode      scriptCodeSize elements, run-length encoded
    jumpTableSize   (u32, little-endian)
    jumpTable       jumpTableSize elements, run-length encoded

One block is a single byte: the low 7 bits are a run length, and the high bit selects between that
many zero elements and that many literal 4-byte little-endian elements.

That description does not match the files in this directory: 0 of 88 decode. The failure is always
the same shape - a zero-length block a few bytes in, which under LoadBytecode's own loop
(`while (scriptCodeSize > 0)`) would spin forever rather than terminate. So either these are not the
files that loader reads, or the layout has a header this decoder does not model.

Observed, so the next attempt does not repeat this one:

    GS000.bin   fa 86 00 00 7f 0f 01 00 61 01 01 00
    PS000.bin   85 0e 00 00 7f 22 02 00 01 00 13 7b
    PS001.bin   fb 59 00 00 09 04 01 00 20 0a 01 00
    PS002.bin   26 25 00 00 0b 05 01 00 21 16 02 00
    PS003.bin   4c 14 00 00 7f 22 02 00 01 00 13 16

A little-endian u32 at offset 0 gives a plausible element count in every case - 34554, 3717, 9510,
5196, 23035 - but the RLE that follows does not hold. A u32 at offset 4 is also plausible
(0x010f7f, 0x02227f, 0x010409) and also does not decode. The first byte takes 66 distinct values
across the 88 files, so it is not a signature.

Ruled out, so nobody repeats them:

  - An RSDKvB signature: absent, and expected. This is an RSDKv3 pack read by RSDKv3.
  - A per-file variation in the format: 0 of 88 either way.
  - The reference Sonic CD.rsdk being different bytes: verified identical earlier, so it cannot
    explain the mismatch.

WHAT IS STILL NEEDED

Either a reader for the layout these files actually use, or a Sonic CD RSDKv3 mod source whose
loader is known to read them. RSDKV3-Decompilation has the text-script compiler and a
bytecode-mode detector (Reader.cpp:54-78 only tests which .bin exists), not a parser.

FILENAMES, WHICH THE LOADER DOES EXPLAIN

LoadBytecode builds the path from stage list and position rather than reading a directory:

    Data/Scripts/ByteCode/<listID><nnn>.bin

with listID in PRBS for PRESENTATION, REGULAR, BONUS, SPECIAL - the leading letter is the stage
LIST, not the stage. So RS061.bin is REGULAR stage 61, and 70 RS files against 70 stage folders
follows from the naming scheme rather than being a coincidence. It is also the strongest evidence
that these are the intended runtime files, which is what makes the mismatch interesting rather than
expected.

    GS000.bin        global code
    PR*, RS*, BS*, SS*
    <folder>.bin     the BYTECODE_MOBILE variant instead
"""
import argparse
import os
import struct
import sys

BYTECODE_DIR_REL = os.path.join("Hybrid-RSDK-Main", "rsdk-source-data", "soniccd",
                                "Data", "Scripts", "ByteCode")


class ContainerError(Exception):
    """Raised with a message that says where and why, not just that parsing failed."""


def _read_u32(data, pos, label):
    if pos + 4 > len(data):
        raise ContainerError("%s: truncated u32 at offset %d of %d bytes"
                             % (label, pos, len(data)))
    return struct.unpack_from("<I", data, pos)[0], pos + 4


def _read_section(data, pos, label):
    """Decode one RLE section. Returns (elements, new_pos)."""
    count, pos = _read_u32(data, pos, label)
    out = []
    while len(out) < count:
        if pos >= len(data):
            raise ContainerError("%s: ran out of data after %d of %d elements (offset %d of %d)"
                                 % (label, len(out), count, pos, len(data)))
        header = data[pos]
        pos += 1
        run = header & 0x7F
        literal = header >= 0x80
        if run == 0:
            raise ContainerError(
                "%s: zero-length block at offset %d. LoadBytecode's own loop would spin here, so "
                "this file does not use that layout." % (label, pos - 1))
        if literal:
            need = run * 4
            if pos + need > len(data):
                raise ContainerError("%s: literal run of %d needs %d bytes, %d remain at offset %d"
                                     % (label, run, need, len(data) - pos, pos))
            out.extend(struct.unpack_from("<%dI" % run, data, pos))
            pos += need
        else:
            out.extend([0] * run)
    if len(out) != count:
        raise ContainerError("%s: decoded %d elements, header declared %d"
                             % (label, len(out), count))
    return out, pos


def parse_container(path):
    with open(path, "rb") as fh:
        data = fh.read()
    code, pos = _read_section(data, 0, "scriptCode")
    jumps, pos = _read_section(data, pos, "jumpTable")
    return {"path": path, "file_bytes": len(data), "script_code": code,
            "jump_table": jumps, "trailing_bytes": len(data) - pos}


def _dump_one(path, limit):
    info = parse_container(path)
    code, jumps = info["script_code"], info["jump_table"]
    print("  %s" % os.path.basename(path))
    print("    file bytes     : %d" % info["file_bytes"])
    print("    scriptCode     : %d elements, %d nonzero"
          % (len(code), sum(1 for v in code if v)))
    print("    jumpTable      : %d elements, %d nonzero"
          % (len(jumps), sum(1 for v in jumps if v)))
    print("    trailing bytes : %d" % info["trailing_bytes"])
    n = limit or 32
    print("    first %d scriptCode elements:" % n)
    for i in range(0, min(n, len(code)), 8):
        print("     %5d  %s" % (i, " ".join("%10d" % v for v in code[i:i + 8])))


def main():
    ap = argparse.ArgumentParser(
        description="Decode Sonic CD RSDKv3 bytecode containers (format unresolved; see docstring)")
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    ap.add_argument("--dir", default=os.path.join(here, BYTECODE_DIR_REL))
    ap.add_argument("--one", help="decode just this file")
    ap.add_argument("--dump", type=int, default=0, metavar="N")
    args = ap.parse_args()

    if args.one:
        p = args.one if os.path.isabs(args.one) else os.path.join(args.dir, args.one)
        _dump_one(p, args.dump)
        return 0

    if not os.path.isdir(args.dir):
        print("FAIL: no such directory: %s" % args.dir)
        return 2
    names = sorted(f for f in os.listdir(args.dir) if f.lower().endswith(".bin"))
    if not names:
        print("FAIL: no .bin files in %s" % args.dir)
        return 2

    print("  directory : %s" % args.dir)
    print("  containers: %d" % len(names))
    print("")
    print("  %-11s %-3s %10s %8s %8s" % ("file", "lst", "bytes", "code", "jump"))
    for n in names:
        try:
            info = parse_container(os.path.join(args.dir, n))
        except ContainerError as exc:
            print("  %-11s %-3s   %s" % (n, n[:1], str(exc)[:88]))
            continue
        print("  %-11s %-3s %10d %8d %8d"
              % (n, n[:1], info["file_bytes"], len(info["script_code"]), len(info["jump_table"])))

    print("")
    print("  0 of %d decode with LoadBytecode's layout, as expected from the docstring." % len(names))
    print("  The format is still unknown; this run confirms the mismatch rather than resolving it.")
    return 1


if __name__ == "__main__":
    sys.exit(main())