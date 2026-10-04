"""Self-rearming keepalive supervisor - always on, detached from any single agent turn.

THE PROBLEM THIS SOLVES

The harness has no timer. The only thing that re-invokes the agent is a tracked background
*command completing*. So a keepalive has to be a command that exits, which means the agent
has to relaunch it - and if the agent is mid-turn when it exits, or goes quiet, or the
session is resumed cold, the chain breaks and a human has to prompt again. That has been
happening repeatedly, which is why this exists.

THE FIX

Two processes with different jobs:

  supervisor (this file)   Runs for hours. Relaunches the waker every cycle, rewrites a
                           heartbeat file, and never exits on its own. Launched detached,
                           so it survives the agent's turn structure entirely.
  waker (keepalive.py)     The thing that actually completes and wakes the agent. Exits
                           every cycle, on purpose.

The supervisor does not need the harness to keep running. That is the whole point: if the
agent goes quiet for any reason, the supervisor is still there, still rewriting the
heartbeat, and still spawning wakers - so the moment anything wakes the agent the chain is
already intact rather than needing to be rebuilt from scratch.

USAGE

    python scripts/keepalive_supervisor.py              # foreground, for testing
    python scripts/keepalive_supervisor.py --minutes 480

    Detached, from PowerShell (this is the normal invocation):

    Start-Process -WindowStyle Hidden -FilePath python `
        -ArgumentList "scripts/keepalive_supervisor.py"

    Check it is alive:

    Get-Content .keepalive-heartbeat

See also: scripts/keepalive.py - the waker this supervises.
"""
import argparse
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
WAKER = os.path.join(HERE, "keepalive.py")

# Written next to this script so it is easy to inspect from outside the session.
HEARTBEAT = os.path.join(HERE, "..", ".keepalive-heartbeat")
LOGFILE = os.path.join(HERE, "..", ".keepalive.log")

# The waker must complete often enough to be useful and rarely enough to cost nothing.
# 55s rather than 60s so cycle boundaries never sit exactly on a minute boundary, which
# would let successive cycles drift into lockstep with anything else on a timer.
CYCLE_SECONDS = 55


def log(message):
    stamp = time.strftime("%Y-%m-%d %H:%M:%S")
    line = "%s  %s\n" % (stamp, message)
    try:
        with open(LOGFILE, "a", encoding="utf-8") as fh:
            fh.write(line)
    except OSError:
        pass
    print(line, end="", flush=True)


def heartbeat(cycle, state):
    """Record liveness where an outside observer can see it.

    Written to a temp file and renamed, so a reader never sees a half-written heartbeat.
    """
    payload = "%s\ncycle=%d\nstate=%s\npid=%d\n" % (
        time.strftime("%Y-%m-%d %H:%M:%S"), cycle, state, os.getpid())
    tmp = HEARTBEAT + ".tmp"
    try:
        with open(tmp, "w", encoding="utf-8") as fh:
            fh.write(payload)
        os.replace(tmp, HEARTBEAT)
    except OSError as exc:
        log("WARNING: could not write heartbeat: %s" % exc)


def run_cycle(cycle, watch):
    """Run one waker to completion, then return its exit code.

    The waker is what wakes the agent, so it must actually be allowed to exit - that
    completion is the signal. Given a generous timeout so a stuck child cannot wedge the
    supervisor, and a timeout is reported as a failed cycle rather than silently ignored,
    because a supervisor that stops re-arming is the exact failure this exists to prevent.
    """
    cmd = [sys.executable, WAKER, "--seconds", str(CYCLE_SECONDS)]
    if watch:
        cmd += ["--watch", watch]
    try:
        done = subprocess.run(cmd, capture_output=True, text=True,
                              timeout=CYCLE_SECONDS + 30)
        return done.returncode
    except subprocess.TimeoutExpired:
        log("WARNING: waker for cycle %d timed out; continuing" % cycle)
        return None


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--minutes", type=float, default=480.0,
                    help="how long to stay alive (default 480 = 8 hours)")
    ap.add_argument("--watch", default=None,
                    help="path the waker should also poll for early exit")
    args = ap.parse_args()

    if not os.path.isfile(WAKER):
        log("FAIL: waker not found at %s" % WAKER)
        return 2

    deadline = time.time() + args.minutes * 60.0
    cycle = 0
    log("supervisor up, pid %d, cycle %ds, alive for %d minutes"
        % (os.getpid(), CYCLE_SECONDS, int(args.minutes)))

    while time.time() < deadline:
        cycle += 1
        heartbeat(cycle, "cycling")
        code = run_cycle(cycle, args.watch)
        if code == 10:
            log("cycle %d: waker exited 10 - watch target appeared" % cycle)
        elif code == 0:
            log("cycle %d: waker completed" % cycle)
        else:
            log("cycle %d: waker exit %s" % (cycle, code))
        heartbeat(cycle, "armed")

    log("supervisor exiting after %d cycles" % cycle)
    return 0


if __name__ == "__main__":
    sys.exit(main())