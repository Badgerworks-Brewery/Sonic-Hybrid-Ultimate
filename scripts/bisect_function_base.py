"""Compare a traced stage run with and without the per-object CallFunction base.

One build, two runs, one number each. The question this answers is narrow: Sonic 1 is
the only game with a non-zero functionBase, ProcessStartupObjects dies at about global
type 55, and distinct-site coverage fell from ~831 to 190 when the base was introduced.
If disabling the base restores coverage, the base is the cause. If it does not, the base
is exonerated and the fault is elsewhere.

The output it compares is deliberately not a frame count. A frame count is what made a
stage look working while a third of its objects were dead.
"""
import io
import os
import re
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rsdk_settings  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WD = os.path.join(ROOT, "Hybrid-RSDK-Main", "sonic-hybrid")
EXE = os.path.join(ROOT, "build", "bin", "Release", "rsdkv4.exe")
LOG = os.path.join(WD, "log.txt")


def run(scene, seconds, extra_env=None):
    rsdk_settings.write_settings(WD, 1, scene)
    if os.path.exists(LOG):
        os.remove(LOG)
    env = dict(os.environ)
    env["RSDK_TRACE_ALL"] = "1"
    if extra_env:
        env.update(extra_env)
    proc = subprocess.Popen([EXE], cwd=WD, env=env,
                            stdout=subprocess.DEVNULL,
                            stderr=subprocess.DEVNULL)
    time.sleep(seconds)
    if proc.poll() is None:
        proc.kill()
    proc.wait()


def measure(label):
    """The numbers that matter: how far the startup loop got, and what it reached."""
    started = []
    entries = set()
    words = []
    for line in io.open(LOG, encoding="latin-1"):
        m = re.match(r"STARTING type (\d+) ", line)
        if m:
            started.append(int(m.group(1)))
        m = re.match(r"ENTER script entry @(\d+)", line)
        if m:
            entries.add(int(m.group(1)))
        m = re.match(r"ORACLE: \S+ @(-?\d+)", line)
        if m:
            words.append(int(m.group(1)))

    stage_words = [w for w in words if w >= 115998]
    print("  %s" % label)
    print("    startup loop reached %d types, highest %s"
          % (len(started), max(started) if started else "-"))
    print("    distinct script entries: %d%s"
          % (len(entries),
             ", highest %d" % max(entries) if entries else ""))
    print("    instructions traced: %d, of which in stage code: %d"
          % (len(words), len(stage_words)))
    print("    traced word range: %s"
          % ("%d..%d" % (min(words), max(words)) if words else "-"))
    return {
        "startup_high": max(started) if started else -1,
        "entries": len(entries),
        "stage_words": len(set(stage_words)),
    }


def main():
    scene = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    seconds = int(sys.argv[2]) if len(sys.argv) > 2 else 12

    if not os.path.exists(EXE):
        print("engine not built")
        return 2

    print("scene %d, %ds per run\n" % (scene, seconds))

    run(scene, seconds)
    with_base = measure("WITH per-object functionBase (current)")

    run(scene, seconds, {"RSDK_NO_FUNCTION_BASE": "1"})
    without = measure("WITHOUT it (stock behaviour, RSDK_NO_FUNCTION_BASE=1)")

    print()
    verdict = []
    if without["startup_high"] > with_base["startup_high"]:
        verdict.append("startup loop reaches further")
    if without["entries"] > with_base["entries"]:
        verdict.append("more distinct script entries")
    if without["stage_words"] > with_base["stage_words"]:
        verdict.append("stage code actually executes")
    if verdict:
        print("VERDICT: the per-object functionBase is implicated - " +
              ", ".join(verdict))
        return 1
    print("VERDICT: no difference. The functionBase is exonerated.")
    return 0


if __name__ == "__main__":
    sys.exit(main())