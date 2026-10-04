# Patches to vendored Sonic 3 A.I.R.

`vendor/sonic3air` is a git submodule. Git records only the pinned commit SHA for it, so
**an edit made directly inside that directory cannot be committed to this repository.** It
would exist only on whichever machine made it, and would be silently absent everywhere
else — including on any fresh clone, where the build would quietly produce an unpatched
engine.

This directory exists so that modifications to A.I.R. are committable, reviewable, and
reproducible.

## The rules

1. **The submodule stays pinned and pristine.** Its worktree is a build artefact as far as
   this repository is concerned. Do not commit changes inside `vendor/sonic3air`; there is
   no mechanism for it and the attempt will fail or be lost.
2. **Every modification lives here as a `.patch` file** — a plain unified diff, applied in
   filename order. `0001-`, `0002-`, and so on, so the sequence is obvious.
3. **Patches are applied at build time**, by `scripts/apply_air_patches.py`. That script is
   idempotent: it detects patches already present and skips them, so it is safe before
   every build and safe to run twice.
4. **Each patch states what it changes and why**, in the commit message that introduced it
   and in a comment at the top of the diff where that is practical. A diff with no stated
   reason is not reviewable six months from now.
5. **A patch that stops applying is a stop signal, not something to force.** It almost
   always means the submodule moved off the SHA the patch was written against. Re-read the
   upstream code, decide whether the change is still wanted, and rewrite the patch — do not
   reach for `--reject` or hand-merge blindly.

## Why not just vendor the source instead

The alternative is dropping the submodule and committing A.I.R.'s ~296 MB and ~11,500
files into this repository's history. That would make edits trivially committable, at the
cost of permanently embedding a large third-party project in this history, making the
provenance of the engine ambiguous, and making every upstream sync a manual merge.

Patches keep the provenance obvious: the SHA says exactly which upstream state this is, and
the diff says exactly what we changed about it. That is worth a small amount of extra
machinery. If that trade is later judged wrong, converting to vendored source is a
mechanical change — flatten the submodule and commit it — and it does not affect the
patches' contents.

## Current patches

| patch | what it does |
|---|---|
| `0001-restartable-engine.patch` | Splits `EngineMain::shutdown()` into per-session teardown and a new `EngineMain::shutdownProcess()`, so the engine can be left and re-entered in one process. See `docs/STATUS.md` for why this is required and what it does not fix. |

## Checking state

```sh
python scripts/apply_air_patches.py --check     # report, change nothing
python scripts/apply_air_patches.py             # apply what's missing
python scripts/apply_air_patches.py --reverse   # undo all
```