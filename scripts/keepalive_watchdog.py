# Keep-alive watchdog: revives the supervisor if it dies.
#
# WHY A SECOND PROCESS
#
# The supervisor runs forever and no longer stops on a lifetime cap, but "forever" is still a
# claim about code, not about reality. It can be killed by a machine restart, a session sign-out,
# a crash, a task-manager decision, or a bug in its own error handling. Observed once already: the
# supervisor hit its 8-hour cap, exited cleanly, and nothing restarted it, so the chain stayed
# dead until a human typed something.
#
# This process has no such dependency. It is the smallest thing that can possibly work - check
# whether the supervisor's PID exists, start one if not - and it has no failure mode of its own
# beyond being killed, at which point it is restarted by whatever launches the next session.
#
# WHY A PID FILE
#
# Checking "is a python process running with keepalive_supervisor in its command line" would also
# match this watchdog's own command line if it mentioned the name, and would need WMI on every
# poll. The supervisor writes its own PID to .keepalive.pid; this reads that, and falls back to a
# process scan only if the file is missing.
#
# It deliberately does NOT try to wake the agent. Nothing outside the harness can do that - only
# the completion of a tracked background command re-invokes the agent, and this process has no way
# to create one. What it guarantees is that the infrastructure is intact and the heartbeat is
# fresh, so a human can tell "the agent is busy" from "the chain is dead".
#
# USAGE (PowerShell, detached - this is the normal invocation):
#
#   Start-Process -WindowStyle Hidden -FilePath python `
#       -ArgumentList "scripts/keepalive_watchdog.py"
#
# Check both are alive:
#
#   Get-Content .keepalive-heartbeat     # freshness proves the supervisor is cycling
#   Get-Content .keepalive-watchdog.log  # restarts are recorded here
#
# Note: start the watchdog AFTER the supervisor the first time, so there is a PID to track.

import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
SUPERVISOR = os.path.join(HERE, "keepalive_supervisor.py")
HEARTBEAT = os.path.join(HERE, "..", ".keepalive-heartbeat")
PIDFILE = os.path.join(HERE, "..", ".keepalive.pid")
LOGFILE = os.path.join(HERE, "..", ".keepalive-watchdog.log")

# How often to check. Faster than the supervisor's cycle, so a death is noticed promptly but not
# so fast that this becomes a busy loop.
POLL_SECONDS = 30

# A heartbeat older than this means the supervisor is present but wedged - alive as a process,
# not actually cycling. Reviving it is still better than leaving it, because a wedged supervisor
# writing nothing is indistinguishable from a dead one to anyone reading the heartbeat.
STALE_SECONDS = 180


def log(message):
    stamp = time.strftime("%Y-%m-%d %H:%M:%S")
    line = "%s  watchdog: %s\n" % (stamp, message)
    try:
        with open(LOGFILE, "a", encoding="utf-8") as fh:
            fh.write(line)
            fh.flush()
    except OSError:
        pass
    print(line, end="", flush=True)


def read_pid():
    try:
        with open(PIDFILE, "r", encoding="utf-8") as fh:
            return int(fh.read().strip())
    except (OSError, ValueError):
        return None


def pid_alive(pid):
    if pid is None:
        return False
    try:
        out = subprocess.run(["tasklist", "/FI", "PID eq %d" % pid, "/NH"],
                             capture_output=True, text=True, timeout=20).stdout
    except (subprocess.TimeoutExpired, OSError):
        return False
    return str(pid) in out


def heartbeat_age():
    """Seconds since the heartbeat was last written, or None if it cannot be read."""
    try:
        stamp = os.path.getmtime(HEARTBEAT)
    except OSError:
        return None
    return time.time() - stamp


def find_supervisor():
    """Scan for a running supervisor, for when the PID file is missing."""
    try:
        r = subprocess.run(
            ["wmic", "process", "where", "name='python.exe'", "get", "ProcessId,CommandLine"],
            capture_output=True, text=True, timeout=30)
    except (subprocess.TimeoutExpired, OSError):
        return None
    for line in (r.stdout or "").splitlines():
        if "keepalive_supervisor" in line:
            parts = line.split()
            for tok in reversed(parts):
                if tok.isdigit():
                    return int(tok)
    return None


def start_supervisor():
    """Launch a supervisor detached, so it survives this process and this session."""
    try:
        p = subprocess.Popen(
            [sys.executable, SUPERVISOR],
            cwd=os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0),
        )
    except OSError as exc:
        log("could not start supervisor: %s" % exc)
        return False
    try:
        with open(PIDFILE, "w", encoding="utf-8") as fh:
            fh.write(str(p.pid))
    except OSError:
        pass
    log("started supervisor, pid %d" % p.pid)
    return True


def main():
    if not os.path.isfile(SUPERVISOR):
        log("FAIL: supervisor not found at %s" % SUPERVISOR)
        return 2

    log("watchdog up, pid %d, poll %ds, stale-after %ds" % (os.getpid(), POLL_SECONDS, STALE_SECONDS))

    pid = read_pid()
    if pid is None:
        pid = find_supervisor()
    if pid is not None and pid_alive(pid):
        log("supervisor already running, pid %d - monitoring" % pid)
        try:
            with open(PIDFILE, "w", encoding="utf-8") as fh:
                fh.write(str(pid))
        except OSError:
            pass
    else:
        log("no supervisor running; starting one")
        start_supervisor()

    restarts = 0
    while True:
        time.sleep(POLL_SECONDS)

        pid = read_pid()
        if pid is None:
            pid = find_supervisor()

        if not pid_alive(pid):
            restarts += 1
            log("supervisor is gone (pid %s) - restart #%d" % (pid, restarts))
            start_supervisor()
            continue

        age = heartbeat_age()
        if age is not None and age > STALE_SECONDS:
            # Present but not cycling. Killing it lets the next poll start a clean one, which is
            # the only way to recover from a hang rather than merely noticing one.
            restarts += 1
            log("supervisor pid %d is stale (heartbeat %ds old) - killing and restarting, #%d"
                % (pid, int(age), restarts))
            try:
                subprocess.run(["taskkill", "/F", "/PID", str(pid)],
                               capture_output=True, timeout=20)
            except (subprocess.TimeoutExpired, OSError):
                pass
            time.sleep(3)
            start_supervisor()


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(0)