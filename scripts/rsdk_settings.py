"""Write settings.ini for a test run without destroying the rest of it.

Both scripts/probe_stages.py and scripts/oracle_check.py need to point the engine at a
particular scene. They used to write a whole settings.ini from a template, which
silently threw away every section and key they did not know about - including
`DataFile=Data.rsdk`, which is how the engine finds the pack, and the whole [Audio] and
[Game] sections. settings.ini is a tracked file, so that damage lands in the next commit
and nobody notices at the time: the tests still pass, because the engine falls back to
the pack beside it.

So: read the file, change only the keys this run needs, write it back. Unknown keys and
unknown sections are left exactly as they were, which is the only behaviour that is safe
for a file the project also ships.
"""
import io
import os
import re

# Keys a test run sets. Everything else in the file is preserved verbatim.
RUN_KEYS = {
    "RefreshRate": "60",
    "WindowScale": "1",
    "ScreenWidth": "640",
    "DimLimit": "300",
    # With no window to take focus on this machine the engine sees hasFocus=0 and
    # pauses the stage, and a run then stops after 20 frames. That reads exactly like a
    # broken stage and is not one. This is a test-harness setting only - the shipped
    # default deliberately pauses when you alt-tab away.
    "DisableFocusPause": "1",
    "EngineDebugMode": "true",
    "TxtScripts": "false",
    "StartingCategory": None,       # filled in by the caller
    "StartingScene": None,
    "StartingSaveFile": "255",
}

SECTION_FOR = {
    "RefreshRate": "Window", "WindowScale": "Window", "ScreenWidth": "Window",
    "DimLimit": "Window", "DisableFocusPause": "Window",
    "EngineDebugMode": "Dev", "TxtScripts": "Dev", "StartingCategory": "Dev",
    "StartingScene": "Dev", "StartingSaveFile": "Dev",
}


def write_settings(pack_dir, category, scene):
    """Point settings.ini at `category`/`scene`, keeping everything else intact."""
    path = os.path.join(pack_dir, "settings.ini")
    values = dict(RUN_KEYS, StartingCategory=str(category),
                  StartingScene=str(scene))

    sections = []          # list of (name, [(key, value), ...])
    index = {}
    if os.path.exists(path):
        current = None
        for raw in io.open(path, encoding="latin-1").read().splitlines():
            line = raw.strip()
            if not line:
                continue
            if line.startswith("[") and line.endswith("]"):
                current = line[1:-1]
                if current not in index:
                    index[current] = len(sections)
                    sections.append((current, []))
                continue
            if "=" not in line or current is None:
                continue
            key, value = line.split("=", 1)
            key = key.strip()
            name = sections[index[current]][0]
            entries = sections[index[current]][1]
            for i, (k, _v) in enumerate(entries):
                if k == key:
                    entries[i] = (key, value.strip())
                    break
            else:
                entries.append((key, value.strip()))

    # Apply the run's keys, creating a section only if it is genuinely missing.
    for key, value in values.items():
        wanted = SECTION_FOR[key]
        if wanted not in index:
            index[wanted] = len(sections)
            sections.append((wanted, []))
        entries = sections[index[wanted]][1]
        for i, (k, _v) in enumerate(entries):
            if k == key:
                entries[i] = (key, value)
                break
        else:
            entries.append((key, value))

    out = []
    for i, (name, entries) in enumerate(sections):
        if i:
            out.append("")
        out.append("[%s]" % name)
        for key, value in entries:
            out.append("%s=%s" % (key, value))
    io.open(path, "w", encoding="latin-1", newline="\r\n").write(
        "\n".join(out) + "\n")
    return path