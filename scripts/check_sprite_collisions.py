#!/usr/bin/env python3
"""Check that no game's spritesheet has been silently overwritten by another's.

`Data/Sprites` was originally copied wholesale in Sonic 1, Sonic CD, Sonic 2
order, so the last writer won. 18 paths exist in more than one game - every
player sheet, plus Global/Items*, LevelSelect/Icons, Ending/*, Title and
Special/Objects - so Sonic 1 was drawing itself with Sonic 2's sprites, which
are laid out differently and simply look wrong.

Object scripts reference sheets by path, so the fix is to give each game its own
copy rather than to decide which sheet "should" win. This test checks that
actually happened: for every sheet present in more than one source game, the
merged output must contain a byte-identical copy under the unsuffixed path *and*
a suffixed one per other game.
"""

import io
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Hybrid-RSDK-Main", "rsdk-source-data")
OUT = os.path.join(ROOT, "Hybrid-RSDK-Main", "sonic-hybrid", "Data", "Sprites")

GAMES = [("sonic1", "S1"), ("soniccd", "CD"), ("sonic2", "S2")]


def sheets(game):
    base = os.path.join(SRC, game, "Data", "Sprites")
    found = {}
    if not os.path.isdir(base):
        return found
    for dirpath, _dirs, files in os.walk(base):
        for name in files:
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, base).replace("\\", "/")
            found[rel] = full
    return found


def main():
    sources = {tag: sheets(game) for game, tag in GAMES}
    sources = {tag: s for tag, s in sources.items() if s}
    if not sources:
        sys.stderr.write("no source sprite data found under %s\n" % SRC)
        return 2
    if not os.path.isdir(OUT):
        sys.stderr.write("merged Sprites directory not found: %s\n" % OUT)
        return 2

    # Every path more than one game provides.
    by_path = {}
    for tag, sheets_for_game in sources.items():
        for rel in sheets_for_game:
            by_path.setdefault(rel, []).append(tag)

    shared = {rel: tags for rel, tags in by_path.items() if len(tags) > 1}
    identical = 0
    clashing = 0
    problems = []

    for rel in sorted(shared):
        tags = sorted(shared[rel])
        digests = {tag: open(sources[tag][rel], "rb").read() for tag in tags}

        # If every game ships identical bytes there is nothing to preserve.
        if len({bytes(v) for v in digests.values()}) == 1:
            identical += 1
            continue

        clashing += 1
        # The generator copies in S1, CD, S2 order and the first game to provide
        # a path keeps it unsuffixed, so compare against that order - not
        # alphabetical, which would credit CD with Sonic 1's sheets.
        ordered = [tag for _game, tag in GAMES if tag in tags]
        first = ordered[0]
        for tag in ordered:
            stem, ext = os.path.splitext(rel)
            name = rel if tag == first else "%s_%s%s" % (stem, tag, ext)
            merged = os.path.join(OUT, name.replace("/", os.sep))
            if not os.path.exists(merged):
                problems.append("missing %s for %s (expected %s)" % (rel, tag, name))
                continue
            if open(merged, "rb").read() != digests[tag]:
                problems.append(
                    "%s for %s does not match the source sheet" % (name, tag))

    print("sprite sheets provided by more than one game: %d" % len(shared))
    print("  byte-identical across games (safe to share): %d" % identical)
    print("  genuinely different (each needs its own copy): %d" % clashing)
    print()
    if problems:
        print("FAIL: %d problem(s)" % len(problems))
        for p in problems[:20]:
            print("      " + p)
        if len(problems) > 20:
            print("      ... and %d more" % (len(problems) - 20))
        return 1

    print("OK: every game's own sheet is present under its own path")
    return 0


if __name__ == "__main__":
    sys.exit(main())