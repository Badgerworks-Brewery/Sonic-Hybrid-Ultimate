#!/usr/bin/env python3
"""Extract the Sonic CD (RSDKv3) opcode tables and measure coverage against RSDKv4.

Sonic CD's bytecode exists in RSDKv3 form - 88 containers under
`rsdk-source-data/soniccd/Data/Scripts/ByteCode/`, the 70 `RS*.bin` lining up one-to-one with
the 70 stage folders. Converting them to RSDKv4 needs a mapping, and the two engines do not
express their opcodes the same way at all:

  - RSDKv3 declares a plain sequential C enum (`enum ScrFunction`, `enum ScriptVar`) in
    `RSDKV3/RSDKv3/Script.cpp`. A `.bin` stores *numbers*, and those numbers mean whatever
    the declaration order says. Reordering the enum would silently change the format.

  - RSDKv4 builds a name-keyed table at startup - `FunctionInfo("Equal", 2)` and so on - so
    its opcodes are identified by name, not by a fixed number.

So a converter cannot do a numeric shift. It needs an explicit v3-enum-order -> v4-name
table, and the only way to be sure that table is right is to derive both halves from the
engines' own source and report what does not line up. That is what this does.

Usage: cd_opcode_map.py [--v3 <path>] [--v4 <path>] [--list-unmapped]
"""
import argparse
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_V3 = os.path.join(REPO, "Hybrid-RSDK-Main", "RSDKV3", "RSDKv3", "Script.cpp")
DEFAULT_V4 = os.path.join(REPO, "Hybrid-RSDK-Main", "RSDKV4-Decompilation",
                          "RSDKv4", "Script.cpp")


def read(path):
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        return fh.read()


def strip_comments(text):
    """Remove block and line comments.

    Necessary because RSDKv3's enums are wrapped in #if blocks - `VAR_ENGINEHAPTICSENABLED`
    sits inside `#if RETRO_USE_HAPTICS`, so a naive scan would index it and shift every
    subsequent value. Whether a given build has haptics on changes the numbering, which is
    exactly the fragility this script exists to make visible rather than to paper over.
    """
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    text = re.sub(r"//[^\n]*", "", text)
    return text


def parse_c_enum(text, enum_name):
    """Return the ordered member names of a C enum, or [] if absent.

    Only unconditional members are returned. Preprocessor-guarded ones are collected
    separately so they can be reported rather than silently folded in.
    """
    m = re.search(r"enum\s+" + re.escape(enum_name) + r"\s*\{(.*?)\n\};", text, flags=re.S)
    if not m:
        return [], []
    body = m.group(1)
    guarded = set(re.findall(r"#if[^\n]*\n(.*?)(?=#endif)", body, flags=re.S))
    guarded_names = set()
    for g in guarded:
        guarded_names.update(re.findall(r"\b([A-Z][A-Z0-9_]*)\b", g))
    names = []
    for raw in body.split(","):
        tok = raw.strip()
        if not tok or tok.startswith("#"):
            continue
        name = re.match(r"([A-Za-z_][A-Za-z0-9_]*)", tok)
        if not name:
            continue
        nm = name.group(1)
        if nm in guarded_names:
            continue
        if nm.isupper() or "_" in nm:
            names.append(nm)
    return names, sorted(guarded_names)


def parse_v4_function_names(text):
    """Pull the names out of RSDKv4's FunctionInfo("Name", count) table."""
    return re.findall(r'FunctionInfo\(\s*"([^"]+)"', text)


def normalise(v3_name):
    """FUNC_EQUAL -> Equal, VAR_TIMETIMER -> TimeTimer.

    v3 uses SCREAMING_SNAKE with a category prefix; v4 uses PascalCase. The prefix and case
    are the only systematic differences, so stripping them is a starting point, not the
    mapping - every entry still has to be confirmed against the real name.
    """
    n = v3_name
    for pfx in ("FUNC_", "VAR_"):
        if n.startswith(pfx):
            n = n[len(pfx):]
            break
    return "".join(part.capitalize() for part in n.split("_") if part)


def parse_v4_variable_names(text):
    """Pull the names out of RSDKv4's `variableNames[][0x20]` string table.

    This is a separate table from `FunctionInfo` - comparing variables against the function
    table reported 0/228 mapped, which reads as "RSDKv4 has no variables" rather than "the
    checker looked in the wrong table". Same lesson as the header-vs-whole-file hash, and
    the reason this function exists separately.

    Note the table is inside `#if RETRO_USE_COMPILER`. That guard is not stripped here on
    purpose: whether the table is present at all depends on the build, and silently
    including it regardless would hide that dependency.
    """
    m = re.search(r"const\s+char\s+variableNames\s*\[\]\s*\[[^\]]*\]\s*=\s*\{(.*?)\n\};",
                  text, flags=re.S)
    if not m:
        return [], False
    body = m.group(1)
    names = re.findall(r'"([^"]+)"', body)
    guarded = "RETRO_USE_COMPILER" in text[:m.start()].rsplit("#if", 1)[-1]
    return names, guarded


def split_scope(v4_name):
    """RSDKv4 variable names carry scope; RSDKv3's do not.

    v4 uses "object.gravity", "global.x" style dotted names. v3's enum is flat
    (VAR_OBJECTYPOS). So a variable mapping cannot just rename - it also has to decide which
    scope each operand belongs to, which the bytecode does not record for us.
    """
    if "." in v4_name:
        scope, _, leaf = v4_name.partition(".")
        return scope, leaf
    return "", v4_name


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--v3", default=DEFAULT_V3)
    ap.add_argument("--v4", default=DEFAULT_V4)
    ap.add_argument("--list-unmapped", action="store_true")
    args = ap.parse_args()

    for p in (args.v3, args.v4):
        if not os.path.isfile(p):
            print("FAIL: no such file: %s" % p)
            return 2

    v3_text = strip_comments(read(args.v3))
    v4_text = strip_comments(read(args.v4))

    funcs, func_guarded = parse_c_enum(v3_text, "ScrFunction")
    # NB: the variable enum is `ScrVariable`, not `ScriptVar`. Guessing the name from the
    # `VAR_` prefix it contains produced an empty list and a 0/0 coverage line that read as
    # "no variables" rather than "parser looked in the wrong place" - the same trap as
    # measuring the wrong scope and reporting it as a fact about the data.
    vars_, var_guarded = parse_c_enum(v3_text, "ScrVariable")
    v4_funcs = parse_v4_function_names(v4_text)
    v4_vars, v4_vars_guarded = parse_v4_variable_names(v4_text)

    print("RSDKv3 (Sonic CD source format)")
    print("  enum ScrFunction : %d unconditional members" % len(funcs))
    print("  enum ScrVariable : %d unconditional members" % len(vars_))
    if func_guarded:
        print("  #if-guarded in ScrFunction, EXCLUDED from numbering: %s"
              % ", ".join(func_guarded))
    if var_guarded:
        print("  #if-guarded in ScrVariable, EXCLUDED from numbering: %s"
              % ", ".join(var_guarded))
    print()
    print("RSDKv4 (target format)")
    print("  FunctionInfo table   : %d named entries" % len(v4_funcs))
    print("  variableNames table  : %d named entries%s"
          % (len(v4_vars),
             "   (inside #if RETRO_USE_COMPILER)" if v4_vars_guarded else ""))
    scoped = sum(1 for v in v4_vars if "." in v)
    if scoped:
        scopes = sorted({split_scope(v)[0] for v in v4_vars if "." in v})
        print("    of which scope-qualified: %d  (scopes: %s)"
              % (scoped, ", ".join(scopes)))
    print()

    v4_func_lower = {}
    for n in v4_funcs:
        # Deduplicate: RSDKv4's table legitimately repeats a name for overloads
        # (SetPaletteFade and LoadTextFile each appear twice), and reporting those as
        # "ambiguous" would invent a conflict that does not exist.
        v4_func_lower.setdefault(n.lower(), set()).add(n)

    def coverage(names, label, table):
        mapped, unmapped = [], []
        for n in names:
            cand = normalise(n)
            hits = table.get(cand.lower())
            if not hits:
                unmapped.append((n, cand))
            else:
                mapped.append((n, cand, sorted(hits)[0]))
        total = len(names)
        print("%s: %d/%d map to a v4 name (%.1f%%)"
              % (label, len(mapped), total, 100.0 * len(mapped) / total if total else 0.0))
        print("  unmapped: %d" % len(unmapped))
        if args.list_unmapped:
            for n, cand in unmapped:
                print("    %-34s -> %s" % (n, cand))
        print()
        return len(mapped), 0, len(unmapped)

    fm, fa, fu = coverage(funcs, "ScrFunction", v4_func_lower)

    # Variables are compared on the leaf name with v4's scope qualifier stripped, purely to
    # show how much of the gap is naming versus structure. It is a lower bound: even a
    # leaf-name hit still has to be assigned a scope, which this does not attempt.
    v4_var_lower = {}
    for n in v4_vars:
        leaf = split_scope(n)[1]
        v4_var_lower.setdefault(leaf.lower(), set()).add(n)
    vm, va, vu = coverage(vars_, "ScrVariable (leaf match, scope ignored)", v4_var_lower)

    print("A caveat that decides how this table may be built.")
    print("RSDKv3's numbering comes from enum declaration order, and some members sit inside")
    print("#if blocks. A build with RETRO_USE_HAPTICS enabled inserts VAR_ENGINEHAPTICSENABLED")
    print("and shifts every later value. Which numbering the CD .bin files actually use is")
    print("therefore an empirical question about the bytes, not something to assume from the")
    print("header - so the converter must confirm the opcode width and first few values against")
    print("a real container before trusting this table.")
    return 0


if __name__ == "__main__":
    sys.exit(main())