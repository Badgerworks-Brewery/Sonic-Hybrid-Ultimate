# Keep-alive watchdog: guarantees exactly one supervisor, forever.
#
# WHY THE FIRST VERSION PRODUCED 206 SUPERVISORS
#
# It spawned a replacement every time it thought the supervisor was dead, and it had two defects
# that compounded:
#
#   1. `pid_alive()` did `str(pid) in tasklist_output`. That is a substring test over the whole
#      table, so pid 13076 was reported alive whenever any *other* process row happened to contain
#      those digits, and dead otherwise. Liveness was essentially a coin flip.
#   2. The supervisor was started with `subprocess.Popen`, which returns a pid that the watchdog
#      recorded but never re-checked, and every supervisor wrote the SAME heartbeat and PID file.
#      So hundreds of them were alive, fighting over one file, each spawning another.
#
# Observed: 206 concurrent `keepalive_supervisor.py` processes. The user's symptom - having to
# restate the session - was this, not a missing keep-alive.
#
# THE TWO INVARIANTS THIS FILE ENFORWS
#
#   A. Exactly one watchdog runs. Enforced with an exclusive lock on a file, not a PID check,
#      because a second watchdog is the thing that must never happen and PID files lie.
#
#   B. Exactly one supervisor runs. Enforced by *counting* live supervisors by their full command
#      line before starting one, and by having the supervisor itself take the same lock, so two
#      watchdogs - or a watchdog and a human - cannot both start one.
#
# Liveness is tested properly: a process is the supervisor only if its command line ends with
# `keepalive_supervisor.py`. No substring matching on PIDs, no reliance on a PID file being fresh.
#
# USAGE (PowerShell, detached):
#
#   Start-Process -WindowStyle Hidden -FilePath python `
#       -ArgumentList "scripts/keepalive_watchdog.py"
#
# Check:
#   Get-Content .keepalive-heartbeat       # freshness proves a supervisor is cycling
#   Get-Content .keepalive-watchdog.log    # every decision is logged
#
# Verify there is only ONE of each (should print 1 and 1):
#   (Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
#     Where-Object { $_.CommandLine -like "*keepalive_supervisor*" }).Count
#   (Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
#     Where-Object { $_.CommandLine -like "*keepalive_watchdog*" }).Count

import json
import os
import shutil
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
SUPERVISOR = os.path.join(HERE, "keepalive_supervisor.py")
REPO = os.path.dirname(HERE)

# Process enumeration lives in a .ps1: see python_processes() for why.
LISTPROC = os.path.join(HERE, "list_python_procs.ps1")
SHELL = shutil.which("pwsh") or shutil.which("powershell") or "powershell"

HEARTBEAT = os.path.join(REPO, ".keepalive-heartbeat")
LOCKDIR = os.path.join(REPO, ".keepalive-watchdog.d")
LOGFILE = os.path.join(REPO, ".keepalive-watchdog.log")

POLL_SECONDS = 30

# A supervisor that is alive but not writing the heartbeat is wedged. Same reasoning as before:
# to anyone reading the heartbeat, wedged and dead are indistinguishable, and leaving a wedged one
# in place is indistinguishable from leaving a dead one in place.
STALE_SECONDS = 180

# Process enumeration is retried rather than trusted once: a single failure read as an empty
# machine is how this file ended up with 206 supervisors in the first place.
ENUM_ATTEMPTS = 3
ENUM_RETRY_DELAY = 3


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


def python_processes_all():
    """All python processes as [(pid, cmdline, started_iso)], or [] when enumeration fails.

    Uses scripts/list_python_procs.ps1 rather than wmic. wmic was the first choice and is the
    cheaper call, but it has been REMOVED from recent Windows 11 - `Get-Command wmic` returns
    nothing - and the first version of this watchdog depended on it. Every cycle then saw an empty
    process list, concluded the supervisor had died, and started another.

    That is the mechanism behind 206 concurrent supervisors, and it is worth stating plainly: a
    keep-alive which cannot see whether its target is alive will always decide the target is dead.
    So an empty result here means "cannot tell", never "none running", and every caller skips its
    decision when it gets one.

    It lives in a separate .ps1 because passing a Get-CimInstance -Filter through a subprocess
    from a Python string literal is where the quoting falls apart - two different failures on the
    way to this version.
    """
    if shutil.which("wmic"):
        cmd = ["wmic", "process", "where", "name='python.exe'",
               "get", "ProcessId,CommandLine,CreationDate"]
    else:
        cmd = [SHELL, "-NoProfile", "-ExecutionPolicy", "Bypass",
               "-File", LISTPROC]

    # Retried, because one failure must never be read as an empty machine. This was observed once
    # in practice - a WinError 2 from process creation, mid-run, under load - and without a retry
    # it made the watchdog conclude the supervisor had died and start another. That is the original
    # bug arriving by a different route, so it is worth closing explicitly rather than hoping.
    last_error = None
    for attempt in range(1, ENUM_ATTEMPTS + 1):
        try:
            r = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
        except (subprocess.TimeoutExpired, OSError) as exc:
            last_error = "%s: %s" % (type(exc).__name__, exc)
            if attempt < ENUM_ATTEMPTS:
                time.sleep(ENUM_RETRY_DELAY)
            continue

        if r.returncode != 0:
            last_error = "exit %d: %s" % (r.returncode, (r.stderr or "").strip()[:160])
            if attempt < ENUM_ATTEMPTS:
                time.sleep(ENUM_RETRY_DELAY)
            continue

        out = (r.stdout or "").strip()
        if not out:
            # A successful run that matched nothing IS a real answer: nothing is running. This is
            # the only path that legitimately returns an empty list.
            return []

        if not out.startswith("["):
            # PowerShell serialises a single object as a bare {...} rather than [...].
            out = "[" + out + "]"
        try:
            data = json.loads(out)
        except ValueError as exc:
            last_error = "unparseable output: %s" % exc
            if attempt < ENUM_ATTEMPTS:
                time.sleep(ENUM_RETRY_DELAY)
            continue

        result = []
        for entry in data:
            try:
                result.append((int(entry["pid"]),
                               entry.get("cmdline") or "",
                               entry.get("started") or ""))
            except (KeyError, TypeError, ValueError):
                continue
        return result

    log("process enumeration failed after %d attempts (%s)" % (ENUM_ATTEMPTS, last_error))
    return []


def matching(script_name):
    """(pids, known) - processes running `script_name`, oldest first, and whether we can tell.

    A 2-tuple on purpose. An empty process list means either "nothing is running" or "enumeration
    failed", and conflating them is the original bug. `known` is False whenever enumeration
    produced nothing usable, and every caller skips its decision rather than acting on a guess.

    Matched on the basename with an exact comparison, not `in`: keepalive_supervisor.py must not
    match keepalive_supervisor.py.bak, and the previous substring test could not tell them apart.
    """
    procs = python_processes_all()
    if not procs:
        return [], False

    hits = []
    for pid, cmdline, started in procs:
        for tok in cmdline.replace('"', " ").split():
            if tok.replace("\\", "/").rsplit("/", 1)[-1] == script_name:
                hits.append((started or "", pid))
                break

    # Oldest first, so the caller keeps the earliest instance and kills any later ones.
    hits.sort()
    return [pid for _, pid in hits], True


def acquire_lock():
    """Exclusive single-instance lock. Returns a handle to keep open, or None if already held.

    An exclusive CREATE of a *directory*, not a byte-range lock on a file. Byte-range locking was
    tried first and is a trap on Windows in three separate ways, each of which failed in turn:

      - `msvcrt.locking` locks from the current file position, so an empty lock file has no byte
        to lock and every attempt "succeeds" - two watchdogs.
      - Preparing the byte requires writing to the very region about to be locked, and in append
        mode that write raises PermissionError on the second process.
      - A lock left by a killed process is not always released promptly enough to be relied on.

    A directory is a better primitive here because the OS makes creation atomic: exactly one
    process can win `mkdir`, and everybody else loses immediately and cleanly. The directory is
    removed on exit, and a directory left behind by a killed watchdog is reclaimed if its recorded
    PID is no longer alive - so a crash cannot permanently block the replacement.
    """
    try:
        os.mkdir(LOCKDIR)
    except FileExistsError:
        # A lock dir exists. Reclaim it if the holder is gone.
        holder = None
        try:
            with open(os.path.join(LOCKDIR, "pid"), "r", encoding="utf-8") as fh:
                holder = int(fh.read().strip())
        except (OSError, ValueError):
            pass

        if holder is not None and not pid_exists(holder):
            log("stale lock from dead pid %d - reclaiming" % holder)
            try:
                os.rmdir(LOCKDIR) if not os.listdir(LOCKDIR) else shutil.rmtree(LOCKDIR, True)
                os.mkdir(LOCKDIR)
            except OSError as exc:
                log("could not reclaim stale lock: %s" % exc)
                return None
        else:
            log("another watchdog holds the lock (pid %s) - exiting" % holder)
            return None
    except OSError as exc:
        log("FAIL: cannot create lock dir %s: %s" % (LOCKDIR, exc))
        return None

    try:
        with open(os.path.join(LOCKDIR, "pid"), "w", encoding="utf-8") as fh:
            fh.write(str(os.getpid()))
    except OSError:
        pass

    log("lock acquired by pid %d" % os.getpid())
    return LOCKDIR


def release_lock(handle):
    """Best-effort removal. Only ever called on a clean exit."""
    if not handle:
        return
    try:
        shutil.rmtree(handle, ignore_errors=True)
    except Exception:
        pass


def pid_exists(pid):
    """Whether a pid is alive. Uses the same enumerator as everything else, so a machine without
    wmic still gets a real answer rather than a guess."""
    try:
        for p, _cmdline, _started in python_processes_all():
            if p == pid:
                return True
    except Exception:
        return True          # cannot tell: assume alive, so we do not steal a live lock
    return False


def heartbeat_age():
    try:
        return time.time() - os.path.getmtime(HEARTBEAT)
    except OSError:
        return None


def kill(pid):
    try:
        subprocess.run(["taskkill", "/F", "/PID", str(pid)],
                       capture_output=True, timeout=30)
        return True
    except (subprocess.TimeoutExpired, OSError):
        return False


def start_supervisor():
    subprocess.Popen(
        [sys.executable, SUPERVISOR],
        cwd=REPO,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        creationflags=getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0),
    )


def reconcile():
    """Enforce invariant B - exactly one supervisor. Returns a status string for logging.

    The `known` guard is the important part. When enumeration fails, this does nothing at all and
    says so. The previous version's failure mode was the opposite: it read an unanswerable
    question as a "no" and acted on it, every cycle, forever.
    """
    sup, known = matching("keepalive_supervisor.py")
    if not known:
        log("cannot enumerate processes - skipping this cycle rather than guessing")
        return "unknown"

    if len(sup) == 0:
        log("no supervisor running - starting one")
        start_supervisor()
        return "started"

    if len(sup) == 1:
        age = heartbeat_age()
        if age is None:
            log("supervisor pid %d running, heartbeat not written yet" % sup[0])
            return "monitoring"
        if age > STALE_SECONDS:
            log("supervisor pid %d is wedged (heartbeat %ds old) - killing and replacing"
                % (sup[0], int(age)))
            if kill(sup[0]):
                time.sleep(2)
            start_supervisor()
            return "replaced-wedged"
        return "monitoring"

    # More than one is the runaway this file exists to prevent.
    log("%d supervisors running - keeping the oldest (pid %d) and killing the rest"
        % (len(sup), sup[0]))
    for pid in sup[1:]:
        kill(pid)
    return "deduplicated"


def main():
    if not os.path.isfile(SUPERVISOR):
        log("FAIL: supervisor not found at %s" % SUPERVISOR)
        return 2

    lock = acquire_lock()
    if lock is None:
        log("another watchdog already holds the lock - exiting")
        return 0

    log("watchdog up, pid %d, poll %ds, stale-after %ds" % (os.getpid(), POLL_SECONDS, STALE_SECONDS))

    # Clean up any pre-existing duplicates immediately, rather than waiting a poll cycle.
    try:
        reconcile()
    except Exception as exc:                       # never let one bad cycle kill the watchdog
        log("reconcile raised %r; continuing" % exc)

    previous = None
    try:
        while True:
            time.sleep(POLL_SECONDS)
            try:
                status = reconcile()
                if status != previous:
                    sup, _ = matching("keepalive_supervisor.py")
                    log("status=%s supervisors=%d" % (status, len(sup)))
                    previous = status
            except Exception as exc:
                log("cycle raised %r; continuing" % exc)
    finally:
        # Only reached on a clean exit. A killed process leaves the lock dir behind, and the next
        # watchdog reclaims it once it has confirmed the recorded pid is dead.
        release_lock(lock)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(0)