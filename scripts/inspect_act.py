"""Read one Act layout the way Scene.cpp:934 reads it, and print what it finds.

Deliberately independent of emit_merged_bytecode.py. Two implementations of the same
format that never share code are the point: a shared misreading cannot hide, and a
disagreement between them is a real finding rather than a tautology.

This exists because Act layouts were being renumbered three times over - once correctly
by the packer, then twice more by a build-time rewriter that edited the game's own
files in place. The rewriter's parser reported "exact" on all 65 files while producing
types past the end of the object table, because the parse was right and the *edit* was
the bug, and no length check can see an edit. Comparing stock against packed shows the
range moving; a count does not.

Usage:
    python scripts/inspect_act.py
"""
import io
import os
import sys

# attrib bit -> width in bytes, in the order Scene.cpp:1024 reads them.
ATTRIB_FIELDS = [
    (0x0001, 4), (0x0002, 1), (0x0004, 4), (0x0008, 4), (0x0010, 1),
    (0x0020, 1), (0x0040, 1), (0x0080, 1), (0x0100, 4), (0x0200, 1),
    (0x0400, 1), (0x0800, 4), (0x1000, 4), (0x2000, 4), (0x4000, 4),
]

CASES = [
    # (label, path)
    ("stock Sonic 1", os.path.join("Hybrid-RSDK-Main", "rsdk-source-data", "sonic1",
                                   "Data", "Stages", "Zone01", "Act1.bin")),
    ("packed GHZS1", os.path.join("Hybrid-RSDK-Main", "sonic-hybrid", "Data",
                                  "Stages", "GHZS1", "Act1.bin")),
]


def read(path):
    """Return a dict describing the layout, or None if it does not parse exactly."""
    if not os.path.exists(path):
        return None
    data = io.open(path, "rb").read()
    pos = 0
    length = data[pos]; pos += 1
    title = data[pos:pos + length]; pos += length
    layers = list(data[pos:pos + 4]); pos += 4
    midpoint = data[pos]; pos += 1
    xsize = data[pos]; pos += 1
    pos += 1                                       # unused
    ysize = data[pos]; pos += 1
    pos += 1                                       # unused
    header_end = pos + 2 * xsize * ysize
    pos = header_end
    if pos + 2 > len(data):
        return None
    count = data[pos] | (data[pos + 1] << 8); pos += 2

    objects = []
    for _ in range(count):
        if pos + 12 > len(data):
            return None
        attribs = data[pos] | (data[pos + 1] << 8); pos += 2
        type_byte = data[pos]; pos += 1
        pos += 1                                   # propertyValue
        pos += 8                                   # xpos, ypos
        for bit, width in ATTRIB_FIELDS:
            if attribs & bit:
                pos += width
        objects.append(type_byte)

    return {
        "size": len(data), "title": title, "layers": layers, "midpoint": midpoint,
        "xsize": xsize, "ysize": ysize, "header_end": header_end,
        "count": count, "end": pos, "objects": objects,
    }


def show(label, path):
    r = read(path)
    if r is None:
        print("  %-16s %s" % (label, "does not parse to its own length"))
        return None
    print("  %-16s %s" % (label, path.replace("\\", "/")))
    print("      %d bytes | title %r | layers %s mid %d"
          % (r["size"], r["title"], r["layers"], r["midpoint"]))
    print("      xsize %d ysize %d | header ends at %d"
          % (r["xsize"], r["ysize"], r["header_end"]))
    print("      object count field: %d | parser ends at %d (%s)"
          % (r["count"], r["end"],
             "exact" if r["end"] == r["size"] else "MISMATCH"))
    if r["objects"]:
        print("      types %d..%d over %d objects"
              % (min(r["objects"]), max(r["objects"]), len(r["objects"])))
        print("      first 16: %s" % r["objects"][:16])
    print()
    return r


def main():
    print("=== Act layouts, read independently of the packer ===\n")
    results = [(label, show(label, path)) for label, path in CASES]
    if len(results) != 2 or any(r is None for _l, r in results):
        return 1

    stock, packed = results[0][1], results[1][1]
    shift = max(packed["objects"]) - max(stock["objects"])
    print("highest type moved by %+d" % shift)
    print("(Sonic 1's globals begin after Sonic 2's 39 in the merged table, so a")
    print(" single correct renumbering is expected to move them by +39 or so. What")
    print(" matters is that it does not keep moving on every build.)")
    return 0


if __name__ == "__main__":
    sys.exit(main())