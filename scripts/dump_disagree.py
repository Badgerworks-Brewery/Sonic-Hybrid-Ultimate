"""Print the actual words at the two sites where the walker and the engine disagree.

Reading the format has not explained this. Both sides' arithmetic checks out:

    walker  VAR + NONE = 3, VAR + ARRAY/ENTNOPLUS1/ENTNOMINUS1 = 5, INTCONST = 2
    engine  Script.cpp:3639-3668 consumes exactly those words

yet at MZS1.bin word 121077 the engine took a five-word branch on operand 0 and the
walker took the three-word branch, and at 126301 the engine consumed 7 words, which is not
expressible as a sum of two VAR widths at all (3+3, 3+5, 5+5, 5+3 give 6, 8 or 10).

Both facts say the same thing: stop reasoning and look. So this prints the words, the tag
bytes, and what each side would make of them, next to the width each one claims.

Usage:  python scripts/dump_disagree.py [stage-container.bin ...]
"""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rsdkv4_bytecode_merger import parse  # noqa: E402
from rsdkv4_opcodes import ORDER          # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BYTECODE = os.path.join(ROOT, "Hybrid-RSDK-Main", "sonic-hybrid", "Data", "Bytecode")

# The word index the oracle prints is the engine's index into its own global scriptCode
# array, not an offset into the container it names. Worth being explicit about, because
# getting it wrong is loud rather than silent in this case: MZS1.bin holds about 30,000
# words, so indexing it with 121,077 raises IndexError instead of quietly returning the
# wrong bytes. A regular stage's words start after the merged globals.
_GLOBAL = os.path.join(BYTECODE, "GlobalCode.bin")
GLOBAL_BASE = len(parse(_GLOBAL).code) if os.path.exists(_GLOBAL) else 0

# (container, container-local word, tags the engine reported, words the engine consumed,
#  words the walker computed)
CASES = [
    ("MZS1.bin", 121077, (1, 2), 7, 5),
    ("MZS1.bin", 126301, (1, 1), 7, 8),
]

# SCRIPTVAR_VAR = 1, INTCONST = 2, STRCONST = 3; VARARR_NONE = 0, ARRAY = 1,
# ENTNOPLUS1 = 2, ENTNOMINUS1 = 3 (Script.cpp:635).
TAG = {1: "VAR", 2: "INTCONST", 3: "STRCONST"}
SEL = {0: "NONE", 1: "ARRAY", 2: "ENTNOPLUS1", 3: "ENTNOMINUS1"}


def var_width(selector):
    """What a VAR costs, per Script.cpp:3646-3668."""
    return 3 if selector == 0 else 5


def main():
    names = sys.argv[1:] or None
    cache = {}
    for container, word, tags, engine_words, walker_words in CASES:
        if names and container not in names:
            continue
        path = os.path.join(BYTECODE, container)
        if container not in cache:
            if not os.path.exists(path):
                print("no such container: %s" % path)
                return 2
            cache[container] = parse(path).code
        code = cache[container]

        print("%s global word %d (container word %d of %d)"
              % (container, word, word - GLOBAL_BASE, len(code)))
        word = word - GLOBAL_BASE
        opcode = code[word]
        opname = ORDER[opcode][0] if 0 <= opcode < len(ORDER) else "?%d" % opcode
        print("  opcode word        %3d  %s (declared %d operands)"
              % (opcode, opname, ORDER[opcode][1] if 0 <= opcode < len(ORDER) else -1))
        print("  engine reported    tags %s, consumed %d words"
              % (list(tags), engine_words))
        print("  walker computed    %d words" % walker_words)
        print()

        # Walk the operands the way the engine does and show every byte it would read.
        pos = word + 1
        running = 0
        for i, tag in enumerate(tags):
            print("  operand %d: tag byte at %d is %d (%s)"
                  % (i, pos, code[pos], TAG.get(tag, "?")))
            if tag == 1:
                sel_pos = pos + 1
                selector = code[sel_pos]
                print("             selector at %d is %d (%s)"
                      % (sel_pos, selector, SEL.get(selector, "UNKNOWN")))
                if selector in (1, 2, 3):
                    flag = code[sel_pos + 1]
                    index = code[sel_pos + 2]
                    print("             flag at %d is %d, index at %d is %d"
                          % (sel_pos + 1, flag, sel_pos + 2, index))
                    print("             -> %s branch, %d words"
                          % (SEL[selector], var_width(selector)))
                    running += var_width(selector)
                else:
                    print("             -> no array branch, 3 words")
                    running += 3
                pos = sel_pos + (3 if selector in (1, 2, 3) else 1)
            elif tag == 2:
                print("             constant at %d is %d -> 2 words"
                      % (pos + 1, code[pos + 1]))
                running += 2
                pos += 2
            else:
                break
            print()

        print("  sum of operands    %d words; engine says %d, walker says %d"
              % (running, engine_words, walker_words))
        if running != engine_words:
            print("  => the engine consumed something the rules above do not "
                  "account for")
        print("  words after the instruction: %s"
              % " ".join(str(code[word + 1 + k]) for k in range(14)))
        print()

    return 0


if __name__ == "__main__":
    sys.exit(main())