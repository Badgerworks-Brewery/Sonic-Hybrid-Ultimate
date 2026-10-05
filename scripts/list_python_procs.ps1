# Enumerate python processes with their command lines, as JSON on stdout.
#
# Exists as a separate file because it is invoked as a subprocess from Python, and the quoting
# needed to pass a Get-CimInstance -Filter through cmd/powershell from a Python string literal is
# where the previous attempt broke (WinError 2 twice, then a positional-parameter error).
#
# wmic was the first choice and is the cheaper call, but it is REMOVED from recent Windows 11 -
# `Get-Command wmic` returns nothing. The watchdog silently degraded to "no processes found", which
# is precisely the failure that let 206 supervisors accumulate: it concluded, every cycle, that
# the supervisor had died.
#
# So: try wmic, and if it is absent, fall back to Get-CimInstance here. Both are checked at call
# time rather than cached, because a machine may gain or lose wmic.

param(
    [string]$Match = ""
)

$ErrorActionPreference = 'Stop'
$procs = Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
    Select-Object ProcessId, CommandLine, CreationDate

if ($Match) {
    $procs = $procs | Where-Object { $_.CommandLine -like "*$Match*" }
}

# CreationDate sorts usefully and is what lets the caller keep the oldest instance.
$out = foreach ($p in $procs) {
    [pscustomobject]@{
        pid      = $p.ProcessId
        cmdline  = $p.CommandLine
        started  = if ($p.CreationDate) { $p.CreationDate.ToString("o") } else { "" }
    }
}

ConvertTo-Json -InputObject @($out) -Compress -Depth 3