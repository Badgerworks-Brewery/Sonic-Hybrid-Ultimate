"""Build the engine, both targets, and report honestly.

Both targets are required. `rsdk_core` is the static library the executable links
against, and building only `rsdkv4` reuses whatever `rsdk_core.lib` was left from last
time - so editing Object.cpp and building only the exe silently tests stale code. That
has happened in this project and passed because the tests were run against a binary that
had not been rebuilt.

This checks the *exit code*, not the text of the output. An earlier version filtered on
`error C`, which matches compiler errors only, so two unresolved externals - reported as
`LNK2019` - went unnoticed, the filter printed nothing, and the script reported success
while the failed link had deleted rsdkv4.exe. Filtering log text for the word "error" is
the same idea with a wider net and is still the wrong idea: MSBuild can fail without ever
printing it. The exit code cannot lie in the same way.
"""
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

MSBUILD_CANDIDATES = [
    r"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    r"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    r"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    r"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
]

PROJECTS = [
    os.path.join("build", "Hybrid-RSDK-Main", "rsdk_core.vcxproj"),
    os.path.join("build", "Hybrid-RSDK-Main", "rsdkv4.vcxproj"),
]

EXE = os.path.join(ROOT, "build", "bin", "Release", "rsdkv4.exe")


def find_msbuild():
    for path in MSBUILD_CANDIDATES:
        if os.path.exists(path):
            return path
    return None


def main():
    msbuild = find_msbuild()
    if not msbuild:
        sys.stderr.write("MSBuild not found in the usual Visual Studio locations\n")
        return 2

    failures = 0
    for project in PROJECTS:
        absolute = os.path.join(ROOT, project)
        print("  building %s" % os.path.basename(project))
        result = subprocess.run(
            [msbuild, absolute, "/p:Configuration=Release", "/v:quiet", "/nologo"],
            cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, errors="replace")

        if result.returncode != 0:
            failures += 1
            # Only now is looking at the text useful: we know it failed, and we want
            # the first few lines to say why.
            for line in (result.stdout or "").splitlines():
                if "rror" in line:
                    print("    %s" % line.strip())
                    break
            print("    FAILED (exit %d)" % result.returncode)
        elif "rror" in (result.stdout or ""):
            # Belt and braces: an exit code of 0 with an error in the output is exactly
            # the case the old filter got wrong in the other direction.
            failures += 1
            for line in (result.stdout or "").splitlines():
                if "rror" in line:
                    print("    %s" % line.strip())
                    break
            print("    FAILED (errors in output despite exit 0)")
        else:
            print("    ok")

    if not os.path.exists(EXE):
        print("\nFAIL: %s does not exist after building" % EXE)
        return 1

    if failures:
        print("\nFAIL: %d of %d targets failed" % (failures, len(PROJECTS)))
        return 1

    print("\nOK: both targets built, %s present (%d bytes)"
          % (os.path.basename(EXE), os.path.getsize(EXE)))
    return 0


if __name__ == "__main__":
    sys.exit(main())