import io, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rsdkv4_bytecode_merger import parse
from rsdkv4_opcodes import ORDER, name_of, size_of

path = sys.argv[1] if len(sys.argv) > 1 else \
    r"Hybrid-RSDK-Main/sonic-hybrid/Data/Bytecode/GlobalCode.bin"
si = int(sys.argv[2]) if len(sys.argv) > 2 else 3
ev = int(sys.argv[3]) if len(sys.argv) > 3 else 0

c = parse(path)
code = c.code
start = c.scripts[si][ev]
jt = c.script_jumps[si][ev]
print("%s  script %d event %d: code %d, jumps %s"
      % (os.path.basename(path), si, ev, start,
         "none" if jt == 0x3FFF else jt))
print()

pc = start
for _ in range(40):
    if pc >= len(code):
        print("  %6d  <past end>" % pc); break
    op = code[pc]
    if op < 0 or op >= len(ORDER):
        print("  %6d  BAD opcode %d" % (pc, op)); break
    sz = size_of(op)
    print("  %6d  %-24s %s" % (pc, name_of(op), list(code[pc + 1:pc + 1 + sz])))
    pc += 1 + sz