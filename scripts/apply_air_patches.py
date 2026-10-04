#!/usr/bin/env python3
"""Apply this project's patches to the vendored Sonic 3 A.I.R. submodule.

`vendor/sonic3air` is a git submodule pinned to a commit, so an edit inside it cannot be
committed to this repository - git records only the pinned SHA. But integrating A.I.R.
requires editing it: `EngineMain::shutdown()` tears down process-global state on the way
out of every game, which makes the engine one-shot per process, and the Hybrid has to be
able to leave Sonic 3 and come back.

The usual answer is the one used here: keep the submodule pristine and pinned, and store
the modifications as patch files in this repository, applied to the worktree at build
time. That keeps 296 MB of third-party source out of this repository's history, keeps the
pinned SHA meaningful, and makes the modifications reviewable as diffs rather than as
vagueness about what was changed locally.

Patches live in `patches/sonic3air/` and are applied in filename order. Each is a plain
unified diff, so `git apply` does the work and `git am` is not needed - there is no
upstream history to preserve, only a diff to reproduce.

Idempotent by construction: a patch that is already applied is detected and skipped, so
this is safe to run before every build and safe to run twice.

Usage:
    apply_air_patches.py                    # apply anything not yet applied, in order
    apply_air_patches.py --check            # report status, change nothing
    apply_air_patches.py --reverse          # undo every applied patch
    apply_air_patches.py --reverse 0001     # undo just that patch
    apply_air_patches.py 0002 0003          # restrict to those patches, in that order

Naming patches positionally exists because "--reverse reverts everything" is actively
misleading during bisection: the obvious next step after a test fails is to revert only the
patch under suspicion, and silently reverting unrelated ones produces a tree that is not
the experiment anyone meant to run. Restricting by name is what makes reverting 0001 for a
falsification run a one-line operation instead of a hand-rolled `git apply`.
"""
import os
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SUBMODULE = os.path.join(REPO, "vendor", "sonic3air")
PATCH_DIR = os.path.join(REPO, "patches", "sonic3air")


def git(*args):
    return subprocess.run(
        ["git", "-C", SUBMODULE] + list(args),
        capture_output=True, text=True,
    )


def patch_files():
    if not os.path.isdir(PATCH_DIR):
        return []
    return sorted(
        os.path.join(PATCH_DIR, n)
        for n in os.listdir(PATCH_DIR)
        if n.endswith(".patch")
    )


def already_applied(path):
    """True when the patch is present in the worktree.

    `git apply --reverse --check` succeeds exactly when the patch's additions are already
    in the file, which is the test for "already applied". It fails both for "not applied"
    and for "does not apply cleanly", so a separate forward check disambiguates.
    """
    rev = git("apply", "--reverse", "--check", path)
    if rev.returncode == 0:
        return True
    fwd = git("apply", "--check", path)
    return fwd.returncode != 0 and "already exists" not in fwd.stderr


def main():
    args = sys.argv[1:]
    reverse = "--reverse" in args
    check_only = "--check" in args

    # Bare arguments are patch name fragments, e.g. "0001" or "0003-restart". Matching is by
    # substring so a short prefix is enough, and an unmatched name is an error rather than a
    # silent no-op - a typo'd restriction that quietly applies everything would be worse than
    # no restriction at all.
    wanted = [a for a in args if not a.startswith("--")]
    for name in wanted:
        if not any(name in os.path.basename(p) for p in patch_files()):
            print("FAIL: no patch matches %r" % name)
            print("      available: %s"
                  % ", ".join(os.path.basename(p) for p in patch_files()))
            return 2

    if not os.path.isdir(SUBMODULE):
        print("FAIL: submodule not initialised at %s" % SUBMODULE)
        print("      run: git submodule update --init --recursive")
        return 2

    pinned = git("rev-parse", "HEAD")
    if pinned.returncode != 0:
        print("FAIL: %s is not a git repository" % SUBMODULE)
        return 2
    pinned_sha = pinned.stdout.strip()

    files = patch_files()
    if wanted:
        files = [p for p in files if any(n in os.path.basename(p) for n in wanted)]
    files.sort()

    print("submodule : %s" % os.path.relpath(SUBMODULE, REPO).replace("\\", "/"))
    print("pinned at : %s" % pinned_sha)
    if not files:
        print("patches   : none selected")
        return 0
    print("patches   : %d selected" % len(files))
    print("")

    failures = 0
    for path in files:
        name = os.path.basename(path)
        applied = already_applied(path)

        if check_only:
            state = "applied" if applied else "NOT applied"
            print("  %-46s %s" % (name, state))
            continue

        if applied == (not reverse):
            # Nothing to do: either already applied and we are applying, or not applied
            # and we are reversing.
            print("  %-46s %s" % (name, "skipped (already applied)" if applied
                                  else "skipped (not applied)"))
            continue

        verb = ["--reverse"] if reverse else []
        res = git("apply", *verb, path)
        if res.returncode != 0:
            print("  %-46s FAILED" % name)
            for line in (res.stderr or res.stdout).strip().splitlines()[:6]:
                print("      %s" % line)
            failures += 1
        else:
            print("  %-46s %s" % (name, "reverted" if reverse else "applied"))

    if check_only:
        return 1 if failures else 0
    if failures:
        print("")
        print("%d patch(es) failed. `git apply` reported the reason above." % failures)
        print("Two causes seen in practice, worth checking in this order:")
        print("  - the submodule has moved off the SHA these patches were written against;")
        print("    check `git -C vendor/sonic3air rev-parse HEAD` against the pinned SHA above")
        print("  - the worktree was edited outside this script, so the patch no longer matches;")
        print("    check `git -C vendor/sonic3air status --short` for unexpected modifications")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())