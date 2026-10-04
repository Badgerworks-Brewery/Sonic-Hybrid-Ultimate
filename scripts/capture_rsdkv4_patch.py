#!/usr/bin/env python3
"""Capture the RSDKV4-Decompilation submodule's local edits as a patch.

WHY THIS EXISTS NOW AND NOT EARLIER

`vendor/sonic3air` is patched through `patches/sonic3air/`, applied at build time. The sibling
RSDKv4 submodule had been edited in place the whole time and never captured: 8 files, 689
insertions, 29 deletions - the typeBase/functionBase object-renumbering work, the name-lookup
change, and a batch of HEARTBEAT instrumentation. None of it was in this repository, so a fresh
clone would have built a *different binary* than the one every measurement in `docs/STATUS.md`
was taken from.

Two things hid it. The submodule pointer matched the index, so `git status` in the parent repo
showed only the lowercase `m` that means "modified content" rather than a new commit - which is
correct, and reads like nothing to do. And `git diff --stat` printed its warning about LF being
replaced by CRLF, so a caller filtering on "did I get output" could read the warning line as
the answer.

The mechanism is the same one AIR uses, for the same reason: a submodule records only a SHA, so
an in-worktree edit cannot be committed to the parent repository.

The HEARTBEAT prints are kept rather than stripped. Several are the only way to tell a stalled
frame loop from a slow one, and they are what `scripts/probe_stages.py` relies on. They are
compile-gated behind `HYBRID_HEARTBEAT_LOG` so the shipped binary stays quiet.

Usage:
    python scripts/capture_rsdkv4_patch.py           # write the patch from current state
    python scripts/capture_rsdkv4_patch.py --check   # verify the patch still matches
"""
import io
import os
import shutil
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SUB = os.path.join(REPO, "Hybrid-RSDK-Main", "RSDKV4-Decompilation")
OUT_DIR = os.path.join(REPO, "patches", "rsdkv4")
NAME = "0001-object-renumbering-and-heartbeats.patch"
PATH = os.path.join(OUT_DIR, NAME)

# Diagnostics on stderr so they never contaminate stdout, which is what callers parse.
def note(msg):
    sys.stderr.write("  note: %s\n" % msg)


def git(*args):
    return subprocess.run(["git", "-C", SUB] + list(args), capture_output=True, text=True)


def dirty_files():
    """Modified and untracked paths, both of which must end up in the patch.

    Untracked files are the case that matters. `git diff` describes changes to *tracked* files
    only, so a new file this worktree needs - RSDKv4/HeartbeatLog.h was exactly that - is
    silently absent from the patch while four other files `#include` it. The patch then applies
    cleanly to a fresh clone and fails to compile. Listing modified files alone is not enough,
    and it fails quietly, which is the worst way to fail.
    """
    res = git("status", "--short")
    modified = []
    untracked = []
    for line in res.stdout.splitlines():
        if not line.strip():
            continue
        # Porcelain v1: XY then path. `??` is untracked; anything with M in XY is an edit.
        code, path = line[:2], line[3:].strip()
        if path.startswith("->"):
            continue
        if code == "??":
            untracked.append(path)
        elif "M" in code:
            modified.append(path)
    return sorted({p.replace("\\", "/") for p in modified}), \
           sorted({p.replace("\\", "/") for p in untracked})


def diff_for(modified, untracked):
    """`git diff` for edits plus an intent-to-add trick so new files appear as creations.

    `git add -N` records intent without staging content, which makes `git diff` emit the file as
    a proper `new file mode` diff instead of omitting it. The index is left holding only intent
    entries, and `git reset` afterwards restores it exactly - a submodule pointer must keep
    matching its index entry, and this must not become a reason for it to stop.
    """
    if untracked:
        subprocess.run(["git", "-C", SUB, "add", "-N", "--"] + untracked,
                       capture_output=True, text=True)
    try:
        res = git("diff", "--", *(modified + untracked))
        if res.stderr.strip():
            note(res.stderr.strip())
        return res
    finally:
        if untracked:
            subprocess.run(["git", "-C", SUB, "reset", "-q", "--"] + untracked,
                           capture_output=True, text=True)


def main():
    check_only = "--check" in sys.argv[1:]

    if not os.path.isdir(os.path.join(SUB, ".git")) and not os.path.isfile(
            os.path.join(SUB, ".git")):
        print("FAIL: %s is not a git checkout" % SUB)
        return 2

    modified, untracked = dirty_files()
    files = modified + untracked
    print("submodule  : Hybrid-RSDK-Main/RSDKV4-Decompilation")
    print("pinned at  : %s" % git("rev-parse", "HEAD").stdout.strip())
    print("modified   : %d" % len(modified))
    for f in modified:
        print("  M %s" % f)
    print("untracked  : %d" % len(untracked))
    for f in untracked:
        print("  A %s" % f)

    if not files:
        print("")
        print("worktree is clean - nothing to capture")
        return 0

    res = diff_for(modified, untracked)
    if res.returncode != 0:
        print("FAIL: git diff exited %d" % res.returncode)
        return 1
    diff = res.stdout
    if not diff.strip():
        print("FAIL: git diff produced no content despite %d dirty files" % len(files))
        return 1

    patch_files = [l.split(" b/")[-1] for l in diff.splitlines() if l.startswith("diff --git")]
    missing = [f for f in files if f not in patch_files]
    if missing:
        print("FAIL: dirty but absent from the diff: %s" % missing)
        return 1

    if check_only:
        print("")
        if not os.path.isfile(PATH):
            print("FAIL: %s does not exist" % os.path.relpath(PATH, REPO))
            return 1
        have = io.open(PATH, encoding="utf-8", newline="").read()
        same = have == diff
        print("captured patch matches the worktree: %s" % ("yes" if same else "NO"))
        if not same:
            print("  captured %d bytes, worktree %d bytes" % (len(have), len(diff)))
            return 1
        # And it must reverse-apply, which is what proves the patch reproduces the tree.
        chk = subprocess.run(["git", "-C", SUB, "apply", "--reverse", "--check", PATH],
                             capture_output=True, text=True)
        print("reverse-applies to the edited worktree: %s"
              % ("yes" if chk.returncode == 0 else "NO"))
        return 0 if chk.returncode == 0 else 1

    os.makedirs(OUT_DIR, exist_ok=True)
    io.open(PATH, "w", encoding="utf-8", newline="\n").write(diff)

    chk = subprocess.run(["git", "-C", SUB, "apply", "--reverse", "--check", PATH],
                         capture_output=True, text=True)
    print("")
    print("wrote %s (%d bytes, %d files)"
          % (os.path.relpath(PATH, REPO).replace("\\", "/"), len(diff), len(patch_files)))
    print("reverse-applies to the edited worktree: %s"
          % ("yes" if chk.returncode == 0 else "NO"))
    if chk.returncode != 0:
        print("FAIL: the patch does not describe the worktree it was taken from")
        return 1

    # The check that matters, and the one this script originally lacked: prove the patch
    # recreates the tree from a pristine checkout, rather than only reversing cleanly on top of
    # the tree it was taken from. Reverse-applying succeeds even when a new file is absent from
    # the patch entirely, which is how an #include of an uncaptured header gets through.
    #
    # Done in a throwaway clone so the real submodule is never disturbed, and so the submodule
    # pointer in this repository is never at risk from a verification step.
    return verify_from_pristine(patch_files, diff)


def _first_difference(got, want):
    """Locate the first differing line, so a mismatch report is actionable not just a flag."""
    gl, wl = got.split(b"\n"), want.split(b"\n")
    for i in range(max(len(gl), len(wl))):
        g = gl[i] if i < len(gl) else b"<end of file>"
        w = wl[i] if i < len(wl) else b"<end of file>"
        if g != w:
            return "line %d:\n        clone %r\n        work  %r" % (i + 1, g[:90], w[:90])
    return "length only"


def verify_from_pristine(patch_files, diff):
    """Apply the captured patch to a clean copy of the submodule and compare the result.

    Compares byte-for-byte against the live worktree for every file the patch touches. This is
    the only check that would have caught the missing HeartbeatLog.h, and it is cheap enough to
    run on every capture.
    """
    tmp = os.path.join(REPO, ".rsdkv4-verify")
    if os.path.isdir(tmp):
        shutil.rmtree(tmp, ignore_errors=True)
    try:
        clone = subprocess.run(
            ["git", "clone", "--quiet", "--no-hardlinks", "--shared", SUB, tmp],
            capture_output=True, text=True)
        if clone.returncode != 0:
            print("  verify: could not clone (%s)" % clone.stderr.strip()[:120])
            return 1

        # The clone lands on the submodule's HEAD, which is the pinned pristine state.
        ap = subprocess.run(["git", "-C", tmp, "apply", "--whitespace=nowarn", PATH],
                            capture_output=True, text=True)
        if ap.returncode != 0:
            print("FAIL: the patch does not apply to a pristine checkout")
            for line in (ap.stderr or ap.stdout).strip().splitlines()[:6]:
                print("      %s" % line)
            return 1
        print("applies to a pristine checkout: yes")

        mismatched, absent = [], []
        for rel in patch_files:
            live = os.path.join(SUB, rel.replace("/", os.sep))
            made = os.path.join(tmp, rel.replace("/", os.sep))
            if not os.path.isfile(made):
                absent.append(rel)
                continue
            # Compare with line endings normalised, not byte-for-byte. git's core.autocrlf
            # rewrites CRLF on checkout and the patch stores LF, so a file this worktree stores
            # with 6,727 CRLF pairs comes out of the clone with LF and differs by 83 bytes on
            # content that is in fact identical. A byte comparison here reports a false failure
            # and, worse, invites "fixing" a patch that is already correct. Line endings are not
            # semantics; the text is.
            want = io.open(live, "rb").read().replace(b"\r\n", b"\n")
            got = io.open(made, "rb").read().replace(b"\r\n", b"\n")
            if want != got:
                where = _first_difference(got, want)
                mismatched.append("%s (first difference at %s)" % (rel, where))
        if absent:
            print("FAIL: patched clone is missing %s" % absent)
            return 1
        if mismatched:
            print("FAIL: patched clone differs from the worktree: %s" % mismatched)
            return 1
        print("patched clone content-identical to the worktree for all %d files: yes"
              % len(patch_files))
        print("  (compared with line endings normalised - see the comment in this function)")
        return 0
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())