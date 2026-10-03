"""Act layout types must not be renumbered twice.

This is a regression test for a bug that survived every check that existed at the
time, including one that reported "exact" on all 65 files.

What happened: `shift_act_object_types` in scripts/emit_merged_bytecode.py added each
game's offset to every object type byte in the Act layouts, and it did so in place on
the output copy of the file. But the packer had *already* renumbered those types -
RsdkGenericImporter.cs resolves each one against the source game's object names and
writes the destination index. So the layouts were shifted once by the packer and again
by the script. Because the script edited the output in place, every later build shifted
an already-shifted file again.

Sonic 1's Green Hill Act 1 says types 0..72. After one build they said 40..111, which
is correct. After two, 79..150 - past the 114 types that exist, so 18 of the stage's
objects were entities whose type matched no script and therefore never ran. Nothing
noticed: the file still parsed to exactly its length, the stage still loaded, and the
frame counter still reached 600.

So the check is deliberately blunt and cheap - the highest type any packed layout
names must not exceed the highest type the stages can actually register - and it runs
as part of the build rather than only in the test suite, because the suite is not what
gates a build.
"""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from emit_merged_bytecode import OUT as BYTECODE, read_act_type_bytes  # noqa: E402
from rsdkv4_bytecode_merger import parse  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STAGES = os.path.join(ROOT, "Hybrid-RSDK-Main", "sonic-hybrid", "Data", "Stages")
STOCK = os.path.join(ROOT, "Hybrid-RSDK-Main", "rsdk-source-data")

# Green Hill Act 1, which is the stage the engine actually boots in the probe.
CASES = [
    ("sonic1", "Zone01", "GHZS1"),
]

# The merged table's globals: Sonic 2's 39 followed by Sonic 1's 38. The packer reads
# these itself, so they are re-derived here rather than trusted.
def merged_globals():
    from emit_merged_bytecode import object_names
    return len(object_names("sonic2")) + len(object_names("sonic1"))


def fail(msg):
    print("FAIL: %s" % msg)
    return 1


def main():
    if not os.path.isdir(STAGES):
        return fail("no packed stage folder at %s" % STAGES)

    globals_count = merged_globals()
    print("merged global object types: %d" % globals_count)

    failures = 0
    for game, src_folder, dst_folder in CASES:
        packed = os.path.join(STAGES, dst_folder, "Act1.bin")
        stock = os.path.join(STOCK, game, "Data", "Stages", src_folder, "Act1.bin")
        if not os.path.exists(packed):
            failures += fail("packed layout missing: %s" % packed)
            continue
        if not os.path.exists(stock):
            failures += fail("stock layout missing: %s" % stock)
            continue

        stock_types = read_act_type_bytes(io.open(stock, "rb").read())
        packed_types = read_act_type_bytes(io.open(packed, "rb").read())
        if stock_types is None or packed_types is None:
            failures += fail("a layout does not parse to its own length")
            continue

        # A shift adds a constant to every type, so the two files' ranges move
        # together. What must not happen is the range leaving the table.
        print("  %s -> %s" % (src_folder, dst_folder))
        print("    stock  types %d..%d over %d objects"
              % (min(stock_types), max(stock_types), len(stock_types)))
        print("    packed types %d..%d over %d objects"
              % (min(packed_types), max(packed_types), len(packed_types)))

        if len(stock_types) != len(packed_types):
            failures += fail("object count changed: %d -> %d"
                             % (len(stock_types), len(packed_types)))

        highest = max(packed_types)

        # Every stage registers the merged globals and then its own objects, and its
        # own bytecode container records exactly how many. That count - not the layout's
        # entity count, which is 305 for Green Hill and far too loose to catch
        # anything - is what makes the bound tight enough to be worth having: a stock
        # type of 72 shifted twice becomes 150, which is past it.
        container = os.path.join(BYTECODE, dst_folder + ".bin")
        if not os.path.exists(container):
            failures += fail("no bytecode container for %s, so its object count is "
                             "unknown and the bound would be a guess" % dst_folder)
            continue
        stage_objects = parse(container).script_count

        allowed = globals_count + stage_objects
        if highest > allowed:
            failures += fail(
                "packed layout names type %d, past the %d types this stage "
                "registers (%d globals + %d stage objects) - the types have been "
                "shifted a second time"
                % (highest, allowed, globals_count, stage_objects))
        else:
            print("    highest type %d is within the %d types this stage registers "
                  "(%d globals + %d stage objects)"
                  % (highest, allowed, globals_count, stage_objects))

    print()
    if failures:
        print("%d Act layout problem(s)" % failures)
        return 1
    print("OK: Act layouts carry destination indices exactly once")
    return 0


if __name__ == "__main__":
    sys.exit(main())