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
import json
import os
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SUBMODULE = os.path.join(REPO, "vendor", "sonic3air")
PATCH_DIR = os.path.join(REPO, "patches", "sonic3air")
MANIFEST = os.path.join(PATCH_DIR, "manifest.json")


def load_manifest():
    """Per-patch content assertions, or {} if the manifest is absent or unreadable.

    Checking content is the only thing that catches a patch which is *wrong but applies
    cleanly*. Two were: one truncated to half its files, one carrying a previous patch's
    change as well. Both applied without complaint. The second is the worse case, because
    `git apply --reverse --check` then succeeds and this script reports "already applied",
    so a no-op reads as success.
    """
    if not os.path.isfile(MANIFEST):
        return {}
    try:
        with open(MANIFEST, "r", encoding="utf-8") as fh:
            data = json.load(fh)
    except (ValueError, OSError) as exc:
        print("WARNING: could not read %s (%s); content checks skipped"
              % (os.path.basename(MANIFEST), exc))
        return {}
    return {k: v for k, v in data.items() if not k.startswith("_")}


def check_content(path, spec):
    """Return a list of human-readable problems with this patch file's content."""
    problems = []
    name = os.path.basename(path)
    if not isinstance(spec, dict):
        return problems
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        body = fh.read()
    if not body.strip():
        problems.append("%s is EMPTY - it would apply cleanly and do nothing" % name)
    for marker in spec.get("must_contain", []):
        if marker not in body:
            problems.append("%s is missing required content %r" % (name, marker))
    for marker in spec.get("must_not_contain", []):
        if marker in body:
            problems.append(
                "%s contains %r, which belongs to another patch - this patch is "
                "duplicated and would silently no-op" % (name, marker))
    return problems


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
    """Tri-state: True applied, False not applied, None "cannot tell - do not guess".

    `git apply --reverse --check` succeeding is the only trustworthy evidence that a patch is
    already present. If it fails, `git apply --check` decides: succeeding means not applied,
    and failing means neither - which is a conflict, not a fact about the worktree.

    The earlier version of this function treated a failing forward check as "already applied".
    That is backwards, and it cost two patches silently: 0008 and 0011 both reported
    `skipped (already applied)` while their changes were absent, because 0007 and 0008 touch
    the same file and 0008's context no longer matched once 0007 was in place. A patch that
    cannot be applied must fail loudly. Reporting it as applied is the one outcome that hides
    the problem, and it is precisely the failure this whole patch mechanism exists to prevent.
    """
    rev = git("apply", "--reverse", "--check", path)
    if rev.returncode == 0:
        return True
    fwd = git("apply", "--check", path)
    if fwd.returncode == 0:
        return False
    return None


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
    conflicts = 0
    for path in files:
        name = os.path.basename(path)
        applied = already_applied(path)

        if applied is None:
            # Neither direction applies. Say so and fail, rather than guessing - see the
            # docstring on already_applied() for why this branch exists at all.
            print("  %-46s CONFLICT" % name)
            print("      applies neither forwards nor in reverse: the worktree does not match")
            print("      this patch and is not the state it expects. Usually another patch")
            print("      touching the same file is applied or missing.")
            conflicts += 1
            continue

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
        return 1 if (failures or conflicts) else 0
    if conflicts:
        print("")
        print("%d patch(es) CONFLICT: neither direction applies. These were previously being"
              % conflicts)
        print("reported as 'skipped (already applied)', which is how two patches went missing")
        print("from the worktree while every run looked green. Resolve the overlap - usually")
        print("regenerate the later patch with the earlier ones applied as a staged baseline -")
        print("rather than assuming they are present.")
        return 1
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