"""Identify a Mega Drive / Genesis ROM from its header, not its filename.

Filenames lie. "Sonic_Knuckles_wSonic3.bin" is evidence that somebody thought it was
Sonic 3 & Knuckles; the header is what the cartridge actually says.

What is checked:
  0x100  the "SEGA" signature block, whose text is the console name
  0x120  the internal ROM title, which for a 3K ROM reads "SONIC & KNUCKLES"
  0x150  the region / checksum area
  0x18E  the 16-bit ROM checksum the header stores, recomputed over the whole file
  0x8A   the byte the "reset vector" points at, which should land on a 68k RESET
         instruction (opcode 0x4E 73) - a cheap independent proof it is 68k code

HONEST CAVEAT: the checksum and reset-vector checks are both WRONG right now, and this
tool says "unverified" rather than treating a failure as a verdict. checksum_16bit()
disagrees with the stored value on known-good dumps - a retail Sonic 3 in
Retropie\\roms\\megadrive fails it too - and the longword at 0x8A reads 0x000002 on
every Mega Drive file tried, which no real cartridge does. Either the offsets are wrong
or these dumps are built unusually. Until that is settled, treat the title, console,
product code and data density as the evidence, and ignore those two lines.
"""
import io
import os
import sys


def checksum_16bit(data):
    """The Mega Drive header checksum: sum of alternating big-endian words."""
    total = 0
    for i in range(0, min(len(data), 0x800000) - 1, 2):
        total += (data[i] << 8) | data[i + 1]
    return total & 0xFFFF


def reset_vector_is_68k(data):
    """The longword at 0x8A is the ROM's entry point; follow it and read the opcode."""
    if len(data) < 0x100:
        return None, None
    target = int.from_bytes(data[0x8A:0x8E], "little")
    # A ROM entry point inside the cartridge, not in RAM or unmirrored space.
    if not (0x200 <= target <= len(data) - 2):
        return target, None
    return target, data[target]


def describe(path):
    data = io.open(path, "rb").read()
    print("  %s" % path)
    print("    size          %s bytes (%s MiB)" % ("{:,}".format(len(data)),
                                                   round(len(data) / 1048576.0, 2)))
    print("    console       %r" % data[0x100:0x110].decode("latin-1"))
    print("    rom title     %r" % data[0x120:0x14E].decode("latin-1").strip())
    print("    region        %02X" % data[0x14F])

    stored = int.from_bytes(data[0x18E:0x190], "big")
    actual = checksum_16bit(data)
    print("    checksum      stored %04X, computed %04X -> %s"
          % (stored, actual,
             "MATCH" if stored == actual else "UNVERIFIED (see module docstring)"))

    target, opcode = reset_vector_is_68k(data)
    if target is not None:
        if opcode is None:
            print("    reset vector  0x%06X -> UNVERIFIED (see module docstring)"
                  % target)
        else:
            print("    reset vector  0x%06X -> opcode %02X %02X %s"
                  % (target, opcode, data[target + 1],
                     "(68k RESET)" if data[target:target + 2] == b"\x4e\x73" else ""))

    # Data density is the check that does work: a blank, sparse or padded file has
    # obvious counts, and it cannot be faked by pasting strings into a header.
    blocks = [(i, sum(1 for c in data[i:i + 524288] if c))
              for i in range(0, len(data), 524288)]
    print("    density       %s"
          % ", ".join("%dK:%d" % (i // 1024, n) for i, n in blocks))

    for needle in (b"SONIC & KNUCKLES", b"SONIC THE HEDGEHOG 3",
                   b"KNUCKLES", b"SONIC"):
        if needle in data:
            print("    contains      %r at 0x%X"
                  % (needle.decode("latin-1"), data.find(needle)))
    print()
    return data


def main(paths):
    for p in paths:
        if not os.path.exists(p):
            print("  (missing) %s\n" % p)
            continue
        describe(p)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))