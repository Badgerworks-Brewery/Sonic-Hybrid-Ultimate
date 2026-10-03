"""Keep-alive: wake the agent on a timeout, or the instant you ask for something.

WHY THIS EXISTS

Nothing in the harness fires on a timer. The only thing that re-invokes the agent is a
background *command completing*. So a keep-alive has to be a command that exits, and the
agent has to relaunch it each time it is resumed. There is no loop underneath this.

Consequence worth being blunt about: if that chain ever breaks - the agent stops, the
session is closed, a command dies - nothing will restart it, and the session still needs
a human prompt. This buys resilience against the agent going quiet between steps. It is
not a daemon.

WHY NOT EVERY MINUTE

An empty wake costs a full turn. Waking every 60 seconds with nothing queued burns
context to produce silence. So the default is a long timeout, and the early exit is for
the case that actually matters: you asking something mid-wait.

USAGE

    python scripts/keepalive.py                          # wake in 60s
    python scripts/keepalive.py --watch ask               # ...or when ask appears

EXIT CODES

    0   the timeout elapsed - nothing to do
    10  the watched path appeared - look at it
    11  bad arguments

WATCHING A PATH

Polls, because this must not busy-wait a CPU the agent also wants. `--watch` takes a
file or directory; the agent can `touch` it to be woken immediately. Point it at a drop
box and you can interrupt a long unattended run without killing the session.
"""
import argparse
import os
import sys
import time

# How often to check the watch target. Short enough to feel instant to a person
# dropping a file in, long enough to cost nothing.
POLL_SECONDS = 2.0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seconds", type=float, default=60.0,
                        help="maximum time to wait before waking (default 60)")
    parser.add_argument("--watch", default=None,
                        help="exit early, with code 10, when this path appears")
    args = parser.parse_args()

    if args.seconds <= 0:
        sys.stderr.write("--seconds must be positive\n")
        return 11

    deadline = time.time() + args.seconds
    beat = time.time()

    while True:
        now = time.time()
        if now >= deadline:
            print("keepalive: %d seconds elapsed, nothing pending"
                  % int(args.seconds))
            return 0
        if args.watch and os.path.exists(args.watch):
            print("keepalive: %s appeared after %d seconds"
                  % (args.watch, int(now - (deadline - args.seconds))))
            return 10
        if now >= beat:
            beat = now + 60.0
            print("keepalive: still waiting, %d seconds left"
                  % int(deadline - now), flush=True)
        time.sleep(POLL_SECONDS)


if __name__ == "__main__":
    sys.exit(main())