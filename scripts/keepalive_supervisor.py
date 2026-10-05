"""Keep-alive supervisor: runs forever, re-arms itself, and cannot be talked out of retrying.

WHY THIS SHAPE

Nothing in the harness fires on a timer. The only thing that re-invokes the agent is a tracked
background *command completing*. So a keep-alive has to be a command that exits, and the agent has
to relaunch it - which is a chain, and chains break.

Two ways this version's predecessor broke, both observed:

  1. It had a lifetime cap (`--minutes 480`). After 8 hours it exited cleanly, by design, and the
     session went quiet until a human typed something. Observed: last heartbeat 2026-10-05
     10:34:52, process gone by 10:48, nothing relaunched it. A keep-alive that stops keeping
     alive on a timer is worse than none, because the heartbeat file keeps looking plausible for
     a while and then quietly stops updating.
  2. A waker that crashed was logged and skipped. One failure in a cycle meant no wake for that
     cycle, and nothing noticed.

So: no lifetime cap by default, and a failed cycle is retried rather than passed over.

WHAT THIS STILL CANNOT DO

It cannot re-invoke the agent. Only the harness can, and only on command completion. The
supervisor's job is to make sure that whenever the harness *does* wake the agent, the chain is
already intact - and to leave visible evidence of liveness so a human can tell the difference
between "agent is thinking" and "chain is dead".

THEREFORE

  - This runs detached, forever, restarted by keepalive_watchdog.ps1 if it ever dies.
  - The agent additionally launches a short tracked background command at the START of every turn.
    That command's completion is what actually wakes the agent. This file is belt-and-braces.

USAGE

    python scripts/keepalive_supervisor.py                 # foreground, runs forever
    python scripts/keepalive_supervisor.py --seconds 5      # short cycle, for testing
    python scripts/keepalive_supervisor.py --minutes 60     # optional cap, off by default

    Detached (the normal invocation):
    Start-Process -WindowStyle Hidden -FilePath python `
        -ArgumentList "scripts/keepalive_supervisor.py"

    Check it is alive:
    Get-Content .keepalive-heartbeat
"""
import argparse
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
WAKER = os.path.join(HERE, "keepalive.py")
HEARTBEAT = os.path.join(HERE, "..", ".keepalive-heartbeat")
LOGFILE = os.path.join(HERE, "..", ".keepalive.log")

# 55s rather than 60s so cycle boundaries never land exactly on a minute, which would let
# successive cycles drift into lockstep with anything else on a timer.
CYCLE_SECONDS = 55

# A cycle that fails is retried this many times before the supervisor gives up on the cycle and
# moves to the next one. Giving up entirely is what made a transient failure fatal before.
RETRIES_PER_CYCLE = 3


def log(message):
    stamp = time.strftime("%Y-%m-%d %H:%M:%S")
    line = "%s  %s\n" % (stamp, message)
    try:
        with open(LOGFILE, "a", encoding="utf-8") as fh:
            fh.write(line)
            fh.flush()
    except OSError:
        pass
    print(line, end="", flush=True)


def heartbeat(cycle, state, extra=""):
    """Record liveness where an outside observer can see it.

    Written to a temp file and renamed, so a reader never sees a half-written heartbeat - which
    matters because a truncated heartbeat looks like a stale one.
    """
    payload = "%s\ncycle=%d\nstate=%s\npid=%d\n%s" % (
        time.strftime("%Y-%m-%d %H:%M:%S"), cycle, state, os.getpid(), extra)
    tmp = HEARTBEAT + ".tmp"
    try:
        with open(tmp, "w", encoding="utf-8") as fh:
            fh.write(payload)
        os.replace(tmp, HEARTBEAT)
    except OSError as exc:
        log("WARNING: could not write heartbeat: %s" % exc)


def run_waker(cycle, seconds):
    """Run one waker to completion. Returns (exit_code, note).

    The waker is the thing that wakes the agent, so it must be allowed to exit - that completion
    is the signal. It gets a generous timeout so a wedged child cannot stall the supervisor, and a
    timeout is reported as a failure rather than ignored.
    """
    cmd = [sys.executable, WAKER, "--seconds", str(seconds)]
    try:
        done = subprocess.run(cmd, capture_output=True, text=True, timeout=seconds + 30)
        return done.returncode, ""
    except subprocess.TimeoutExpired:
        return None, "timed out"
    except OSError as exc:
        return None, "spawn failed: %s" % exc


def main():
    ap = argparse.ArgumentParser(description="keep-alive supervisor")
    ap.add_argument("--seconds", type=int, default=CYCLE_SECONDS,
                    help="cycle length (default %d)" % CYCLE_SECONDS)
    ap.add_argument("--minutes", type=float, default=0.0,
                    help="optional lifetime cap in minutes; 0 (the default) means run forever. "
                         "A cap is what made the previous version stop keeping alive.")
    args = ap.parse_args()

    if not os.path.isfile(WAKER):
        log("FAIL: waker not found at %s" % WAKER)
        return 2

    seconds = max(1, int(args.seconds))
    deadline = (time.time() + args.minutes * 60.0) if args.minutes > 0 else None
    cycle = 0
    log("supervisor up, pid %d, cycle %ds, lifetime %s"
        % (os.getpid(), seconds,
           ("%d min" % int(args.minutes)) if deadline else "unlimited"))

    while deadline is None or time.time() < deadline:
        cycle += 1
        heartbeat(cycle, "cycling")

        # Retry rather than skip. One failed waker used to mean a silent gap.
        code, note = None, "no attempt"
        for attempt in range(1, RETRIES_PER_CYCLE + 1):
            code, note = run_waker(cycle, seconds)
            if code in (0, 10):
                break
            log("cycle %d attempt %d failed (%s); retrying" % (cycle, attempt, note))
            heartbeat(cycle, "retrying", "attempt=%d\n" % attempt)
            time.sleep(2)

        if code == 10:
            log("cycle %d: waker exited 10 - watch target appeared" % cycle)
        elif code == 0:
            log("cycle %d: waker completed" % cycle)
        else:
            # Out of retries. Log loudly and continue - a supervisor that exits here is how the
            # chain dies, so the only safe response to a bad cycle is to keep cycling.
            log("cycle %d: FAILED after %d attempts (%s); continuing anyway"
                % (cycle, RETRIES_PER_CYCLE, note))
        heartbeat(cycle, "armed", "last_exit=%s\n" % code)

    log("supervisor exiting after %d cycles (lifetime cap reached)" % cycle)
    return 0


if __name__ == "__main__":
    sys.exit(main())