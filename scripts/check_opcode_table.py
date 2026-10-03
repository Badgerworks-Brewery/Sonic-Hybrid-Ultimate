import io
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rsdkv4_opcodes import ORDER

LOG = os.path.join("Hybrid-RSDK-Main", "sonic-hybrid", "log.txt")
if not os.path.exists(LOG):
    raise SystemExit("no log.txt - run the engine first")

engine = {}
for line in io.open(LOG, encoding="latin-1"):
    m = re.match(r"OPTABLE (\d+) (\S+) (-?\d+)", line.strip())
    if m:
        engine[int(m.group(1))] = (m.group(2), int(m.group(3)))

print("engine opcode table: %d entries" % len(engine))
print("derived table:        %d entries" % len(ORDER))
print()

bad = 0
for i in range(max(len(engine), len(ORDER))):
    e = engine.get(i)
    d = ORDER[i] if i < len(ORDER) else None
    if e != d:
        bad += 1
        if bad <= 14:
            print("  %3d  engine=%-26s derived=%s"
                  % (i, e, d))

if bad:
    print()
    print("%d difference(s)" % bad)
    sys.exit(1)

print("OK: the derived table matches the engine's exactly, index for index")