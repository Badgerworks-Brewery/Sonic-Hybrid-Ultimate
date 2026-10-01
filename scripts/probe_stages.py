import io, os, re, subprocess, time, sys

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
wd = os.path.join(root, "Hybrid-RSDK-Main", "sonic-hybrid")
exe = os.path.join(root, "build", "bin", "Release", "rsdkv4.exe")

# Stage indices in the merged StagesRegular list, from the GameConfig dump:
# StartingCategory must be 1 (STAGELIST_REGULAR) - 0 is the presentation list,
# and InitFirstStage treats 0 as "unset".
#   0  GREEN HILL ZONE 1        (Sonic 1)
#   18 FINAL ZONE               (Sonic 1, last)
#   19 PALMTREE PANIC 1 PRESENT (Sonic CD, first)
#   88 METALLIC MADNESS 3 BAD   (Sonic CD, last)
#   89 EMERALD HILL ZONE 1      (Sonic 2, first)
#   109 DEATH EGG ZONE          (Sonic 2, last)
PROBES = [
    (0,   "Sonic 1  Green Hill Act 1"),
    (18,  "Sonic 1  Final Zone"),
    (19,  "Sonic CD  Palmtree Panic A1 Present"),
    (49,  "Sonic CD  Collision Chaos A2 Past"),
    (88,  "Sonic CD  Metallic Madness A3 Bad Future"),
    (89,  "Sonic 2  Emerald Hill Act 1"),
    (109, "Sonic 2  Death Egg Zone"),
]

for f in ("log.txt", "settings.ini"):
    p = os.path.join(wd, f)
    if os.path.exists(p):
        os.remove(p)

print(f"{'idx':>4}  {'stage':<38} {'loaded':<7} objects  scripts")
print("-" * 78)

rows = []
for idx, label in PROBES:
    io.open(os.path.join(wd, "settings.ini"), "w", encoding="ascii").write(
        "[Dev]\nEngineDebugMode=true\nTxtScripts=false\n"
        f"StartingCategory=1\nStartingScene={idx}\nStartingSaveFile=255\n"
        "DataFile=Data.rsdk\n[Game]\nLanguage=0\nSkipStartMenu=true\n")

    log = os.path.join(wd, "log.txt")
    if os.path.exists(log):
        os.remove(log)

    proc = subprocess.Popen([exe], cwd=wd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(14)
    if proc.poll() is None:
        proc.kill()
    time.sleep(0.5)

    text = io.open(log, encoding="utf-8", errors="replace").read() if os.path.exists(log) else ""

    loaded = "YES" if f"Stages/" in text else "no"
    objects = len(re.findall(r"^Set Object", text, re.M))
    scripts = len(re.findall(r"Loaded Data File 'Data/Scripts", text))
    scene = next((l.strip() for l in text.splitlines() if "Loading Scene" in l), "")
    print(f"{idx:>4}  {label:<38} {loaded:<7} {objects:>5}  {scripts:>5}   {scene[:46]}")

    rows.append((idx, label, loaded, objects, scripts))

for f in ("log.txt", "settings.ini"):
    p = os.path.join(wd, f)
    if os.path.exists(p):
        os.remove(p)

ok = all(r[2] == "YES" and r[3] > 0 for r in rows)
print()
print("every probed stage loaded from the unified pack:", ok)
sys.exit(0 if ok else 1)
