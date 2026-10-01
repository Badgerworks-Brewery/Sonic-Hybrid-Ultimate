#!/usr/bin/env python3
"""Generate the RSDKv3 -> RSDKv4 script variable table.

The bytecode encodes a variable as an index into RSDKv3's ``ScrVariable`` enum.
Getting an index wrong does not crash - it silently reads or writes a different
property - so this table is *derived from the engine sources* rather than typed
out by hand. An earlier hand-written copy of the enum was found to contain 15
members that do not exist in RSDKv3 and to drift out of alignment at index 41,
which mislabelled every variable from there on.

Run after changing either engine source:

    python scripts/gen_rsdkv3_variables.py

Output: Hybrid-RSDK-Main/SonicHybridRsdk.Generator/Generated/RsdkV3Variables.g.cs
"""

import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
V3_SRC = os.path.join(ROOT, "Hybrid-RSDK-Main", "RSDKV3", "RSDKv3", "Script.cpp")
V4_SRC = os.path.join(ROOT, "Hybrid-RSDK-Main", "RSDKV4-Decompilation", "RSDKv4", "Script.cpp")
OUT = os.path.join(ROOT, "Hybrid-RSDK-Main", "SonicHybridRsdk.Generator",
                   "Generated", "RsdkV3Variables.g.cs")


def read(path):
    with io.open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read().split("\n")


def skip_noise(text):
    text = text.strip()
    return (text == "" or text.startswith("//") or text.startswith("#")
            or text.startswith("/*") or text.startswith("*"))


def parse_v3_enum(lines):
    """ScrVariable enum members, in declaration order."""
    start = None
    for i, line in enumerate(lines):
        if re.match(r"^\s*VAR_TEMPVALUE0,?\s*$", line):
            start = i
            break
    if start is None:
        sys.exit("could not find the start of the ScrVariable enum in " + V3_SRC)

    names = []
    for line in lines[start:]:
        m = re.match(r"^\s*(VAR_[A-Z0-9_]+),?\s*$", line)
        if m:
            names.append(m.group(1))
            if m.group(1).endswith("HAPTICSENABLED"):
                break
        elif names and not skip_noise(line):
            break
    if not names:
        sys.exit("parsed an empty ScrVariable enum")
    return names


def parse_v4_table(lines):
    """RSDKv4's ScriptVariable name table, in declaration order."""
    start = None
    for i, line in enumerate(lines):
        if re.match(r'^\s*"temp0",\s*$', line):
            start = i
            break
    if start is None:
        sys.exit("could not find the start of RSDKv4's variable table in " + V4_SRC)

    names = []
    for line in lines[start:]:
        m = re.match(r'^\s*"([^"]+)",\s*(//.*)?$', line)
        if m:
            names.append(m.group(1))
            continue
        if names and not skip_noise(line):
            break
    if not names:
        sys.exit("parsed an empty RSDKv4 variable table")
    return names


def build_mapping(v3, v4):
    """v3 enum member -> RSDKv4 variable name, or None when there is no equivalent.

    Every entry here was chosen against RSDKv4's real variable table; anything
    asserted below that RSDKv4 does not define aborts generation rather than
    emitting a name the engine would reject.
    """
    m = {}

    def put(v3name, v4name):
        m[v3name] = v4name

    # --- temporaries and shared slots: identical in both engines ---
    for i in range(8):
        put("VAR_TEMPVALUE%d" % i, "temp%d" % i)
    put("VAR_CHECKRESULT", "checkResult")
    put("VAR_ARRAYPOS0", "arrayPos0")
    put("VAR_ARRAYPOS1", "arrayPos1")
    put("VAR_GLOBAL", "global")

    # --- object properties ---
    for a, b in [
        ("VAR_OBJECTENTITYNO", "object.entityPos"),
        ("VAR_OBJECTTYPE", "object.type"),
        ("VAR_OBJECTPROPERTYVALUE", "object.propertyValue"),
        ("VAR_OBJECTXPOS", "object.xpos"),
        ("VAR_OBJECTYPOS", "object.ypos"),
        ("VAR_OBJECTIXPOS", "object.ixpos"),
        ("VAR_OBJECTIYPOS", "object.iypos"),
        ("VAR_OBJECTSTATE", "object.state"),
        ("VAR_OBJECTROTATION", "object.rotation"),
        ("VAR_OBJECTSCALE", "object.scale"),
        ("VAR_OBJECTPRIORITY", "object.priority"),
        ("VAR_OBJECTDRAWORDER", "object.drawOrder"),
        ("VAR_OBJECTDIRECTION", "object.direction"),
        ("VAR_OBJECTINKEFFECT", "object.inkEffect"),
        ("VAR_OBJECTALPHA", "object.alpha"),
        ("VAR_OBJECTFRAME", "object.frame"),
        ("VAR_OBJECTANIMATION", "object.animation"),
        ("VAR_OBJECTPREVANIMATION", "object.prevAnimation"),
        ("VAR_OBJECTANIMATIONSPEED", "object.animationSpeed"),
        ("VAR_OBJECTANIMATIONTIMER", "object.animationTimer"),
        ("VAR_OBJECTOUTOFBOUNDS", "object.outOfBounds"),
        ("VAR_OBJECTSPRITESHEET", "object.spriteSheet"),
    ]:
        put(a, b)
    for i in range(8):
        put("VAR_OBJECTVALUE%d" % i, "object.value%d" % i)

    # --- player properties: RSDKv4 has no player.* namespace, so the player
    #     object is addressed through object.* ---
    for a, b in [
        ("VAR_PLAYERSTATE", "object.state"),
        ("VAR_PLAYERCONTROLMODE", "object.controlMode"),
        ("VAR_PLAYERCONTROLLOCK", "object.controlLock"),
        ("VAR_PLAYERCOLLISIONMODE", "object.collisionMode"),
        ("VAR_PLAYERCOLLISIONPLANE", "object.collisionPlane"),
        ("VAR_PLAYERXPOS", "object.xpos"),
        ("VAR_PLAYERYPOS", "object.ypos"),
        ("VAR_PLAYERIXPOS", "object.ixpos"),
        ("VAR_PLAYERIYPOS", "object.iypos"),
        ("VAR_PLAYERSPEED", "object.speed"),
        ("VAR_PLAYERXVELOCITY", "object.xvel"),
        ("VAR_PLAYERYVELOCITY", "object.yvel"),
        ("VAR_PLAYERGRAVITY", "object.gravity"),
        ("VAR_PLAYERANGLE", "object.angle"),
        ("VAR_PLAYERPUSHING", "object.pushing"),
        ("VAR_PLAYERTRACKSCROLL", "object.scrollTracking"),
        ("VAR_PLAYERUP", "object.up"),
        ("VAR_PLAYERDOWN", "object.down"),
        ("VAR_PLAYERLEFT", "object.left"),
        ("VAR_PLAYERRIGHT", "object.right"),
        ("VAR_PLAYERJUMPPRESS", "object.jumpPress"),
        ("VAR_PLAYERJUMPHOLD", "object.jumpHold"),
        ("VAR_PLAYERENTITYNO", "object.entityPos"),
        ("VAR_PLAYERCOLLISIONLEFT", "object.collisionLeft"),
        ("VAR_PLAYERCOLLISIONTOP", "object.collisionTop"),
        ("VAR_PLAYERCOLLISIONRIGHT", "object.collisionRight"),
        ("VAR_PLAYERCOLLISIONBOTTOM", "object.collisionBottom"),
        ("VAR_PLAYERTILECOLLISIONS", "object.tileCollisions"),
        ("VAR_PLAYEROBJECTINTERACTION", "object.interaction"),
        ("VAR_PLAYERVISIBLE", "object.visible"),
        ("VAR_PLAYERROTATION", "object.rotation"),
        ("VAR_PLAYERSCALE", "object.scale"),
        ("VAR_PLAYERPRIORITY", "object.priority"),
        ("VAR_PLAYERDRAWORDER", "object.drawOrder"),
        ("VAR_PLAYERDIRECTION", "object.direction"),
        ("VAR_PLAYERINKEFFECT", "object.inkEffect"),
        ("VAR_PLAYERALPHA", "object.alpha"),
        ("VAR_PLAYERFRAME", "object.frame"),
        ("VAR_PLAYERANIMATION", "object.animation"),
        ("VAR_PLAYERPREVANIMATION", "object.prevAnimation"),
        ("VAR_PLAYERANIMATIONSPEED", "object.animationSpeed"),
        ("VAR_PLAYERANIMATIONTIMER", "object.animationTimer"),
        ("VAR_PLAYEROUTOFBOUNDS", "object.outOfBounds"),
    ]:
        put(a, b)
    for i in range(16):
        put("VAR_PLAYERVALUE%d" % i, "object.value%d" % i)

    # --- stage ---
    for a, b in [
        ("VAR_STAGESTATE", "stage.state"),
        ("VAR_STAGEACTIVELIST", "stage.activeList"),
        ("VAR_STAGELISTPOS", "stage.listPos"),
        ("VAR_STAGETIMEENABLED", "stage.timeEnabled"),
        ("VAR_STAGEMILLISECONDS", "stage.milliSeconds"),
        ("VAR_STAGESECONDS", "stage.seconds"),
        ("VAR_STAGEMINUTES", "stage.minutes"),
        ("VAR_STAGEACTNO", "stage.actNum"),
        ("VAR_STAGEPAUSEENABLED", "stage.pauseEnabled"),
        ("VAR_STAGELISTSIZE", "stage.listSize"),
        ("VAR_STAGENEWXBOUNDARY1", "stage.newXBoundary1"),
        ("VAR_STAGENEWXBOUNDARY2", "stage.newXBoundary2"),
        ("VAR_STAGENEWYBOUNDARY1", "stage.newYBoundary1"),
        ("VAR_STAGENEWYBOUNDARY2", "stage.newYBoundary2"),
        ("VAR_STAGEXBOUNDARY1", "stage.curXBoundary1"),
        ("VAR_STAGEXBOUNDARY2", "stage.curXBoundary2"),
        ("VAR_STAGEYBOUNDARY1", "stage.curYBoundary1"),
        ("VAR_STAGEYBOUNDARY2", "stage.curYBoundary2"),
        ("VAR_STAGEWATERLEVEL", "stage.waterLevel"),
        ("VAR_STAGEACTIVELAYER", "stage.activeLayer"),
        ("VAR_STAGEMIDPOINT", "stage.midPoint"),
        ("VAR_STAGEPLAYERLISTPOS", "stage.playerListPos"),
        ("VAR_STAGEDEBUGMODE", "stage.debugMode"),
    ]:
        put(a, b)
    for i in range(4):
        put("VAR_STAGEDEFORMATIONDATA%d" % i, "stage.deformationData%d" % i)

    # --- camera / screen ---
    for a, b in [
        ("VAR_SCREENCAMERAENABLED", "screen.cameraEnabled"),
        ("VAR_SCREENCAMERATARGET", "screen.cameraTarget"),
        ("VAR_SCREENCAMERASTYLE", "screen.cameraStyle"),
        ("VAR_SCREENDRAWLISTSIZE", "screen.drawListSize"),
        ("VAR_SCREENCENTERX", "screen.xcenter"),
        ("VAR_SCREENCENTERY", "screen.ycenter"),
        ("VAR_SCREENXSIZE", "screen.xsize"),
        ("VAR_SCREENYSIZE", "screen.ysize"),
        ("VAR_SCREENXOFFSET", "screen.xoffset"),
        ("VAR_SCREENYOFFSET", "screen.yoffset"),
        ("VAR_SCREENSHAKEX", "screen.shakeX"),
        ("VAR_SCREENSHAKEY", "screen.shakeY"),
        ("VAR_SCREENADJUSTCAMERAY", "screen.adjustCameraY"),
        ("VAR_TOUCHSCREENDOWN", "touchscreen.down"),
        ("VAR_TOUCHSCREENXPOS", "touchscreen.xpos"),
        ("VAR_TOUCHSCREENYPOS", "touchscreen.ypos"),
        ("VAR_MUSICVOLUME", "music.volume"),
        ("VAR_MUSICCURRENTTRACK", "music.currentTrack"),
        ("VAR_MENU1SELECTION", "menu1.selection"),
        ("VAR_MENU2SELECTION", "menu2.selection"),
    ]:
        put(a, b)

    # --- input (RSDKv4 spells these roots keyDown / keyPress, and has no
    #     anyStart equivalent) ---
    for a, b in [
        ("VAR_KEYDOWNUP", "keyDown.up"),
        ("VAR_KEYDOWNDOWN", "keyDown.down"),
        ("VAR_KEYDOWNLEFT", "keyDown.left"),
        ("VAR_KEYDOWNRIGHT", "keyDown.right"),
        ("VAR_KEYDOWNBUTTONA", "keyDown.buttonA"),
        ("VAR_KEYDOWNBUTTONB", "keyDown.buttonB"),
        ("VAR_KEYDOWNBUTTONC", "keyDown.buttonC"),
        ("VAR_KEYDOWNSTART", "keyDown.start"),
        ("VAR_KEYPRESSUP", "keyPress.up"),
        ("VAR_KEYPRESSDOWN", "keyPress.down"),
        ("VAR_KEYPRESSLEFT", "keyPress.left"),
        ("VAR_KEYPRESSRIGHT", "keyPress.right"),
        ("VAR_KEYPRESSBUTTONA", "keyPress.buttonA"),
        ("VAR_KEYPRESSBUTTONB", "keyPress.buttonB"),
        ("VAR_KEYPRESSBUTTONC", "keyPress.buttonC"),
        ("VAR_KEYPRESSSTART", "keyPress.start"),
    ]:
        put(a, b)

    # --- tile layers and parallax ---
    for a, b in [
        ("VAR_TILELAYERXSIZE", "tileLayer.xsize"),
        ("VAR_TILELAYERYSIZE", "tileLayer.ysize"),
        ("VAR_TILELAYERTYPE", "tileLayer.type"),
        ("VAR_TILELAYERANGLE", "tileLayer.angle"),
        ("VAR_TILELAYERXPOS", "tileLayer.xpos"),
        ("VAR_TILELAYERYPOS", "tileLayer.ypos"),
        ("VAR_TILELAYERZPOS", "tileLayer.zpos"),
        ("VAR_TILELAYERPARALLAXFACTOR", "tileLayer.parallaxFactor"),
        ("VAR_TILELAYERSCROLLSPEED", "tileLayer.scrollSpeed"),
        ("VAR_TILELAYERSCROLLPOS", "tileLayer.scrollPos"),
        ("VAR_TILELAYERDEFORMATIONOFFSET", "tileLayer.deformationOffset"),
        ("VAR_TILELAYERDEFORMATIONOFFSETW", "tileLayer.deformationOffsetW"),
        ("VAR_HPARALLAXPARALLAXFACTOR", "hParallax.parallaxFactor"),
        ("VAR_HPARALLAXSCROLLSPEED", "hParallax.scrollSpeed"),
        ("VAR_HPARALLAXSCROLLPOS", "hParallax.scrollPos"),
        ("VAR_VPARALLAXPARALLAXFACTOR", "vParallax.parallaxFactor"),
        ("VAR_VPARALLAXSCROLLSPEED", "vParallax.scrollSpeed"),
        ("VAR_VPARALLAXSCROLLPOS", "vParallax.scrollPos"),
    ]:
        put(a, b)

    # --- 3D scene / buffers ---
    for a, b in [
        ("VAR_3DSCENENOVERTICES", "scene3D.vertexCount"),
        ("VAR_3DSCENENOFACES", "scene3D.faceCount"),
        ("VAR_3DSCENEPROJECTIONX", "scene3D.projectionX"),
        ("VAR_3DSCENEPROJECTIONY", "scene3D.projectionY"),
        ("VAR_VERTEXBUFFERX", "vertexBuffer.x"),
        ("VAR_VERTEXBUFFERY", "vertexBuffer.y"),
        ("VAR_VERTEXBUFFERZ", "vertexBuffer.z"),
        ("VAR_VERTEXBUFFERU", "vertexBuffer.u"),
        ("VAR_VERTEXBUFFERV", "vertexBuffer.v"),
        ("VAR_FACEBUFFERA", "faceBuffer.a"),
        ("VAR_FACEBUFFERB", "faceBuffer.b"),
        ("VAR_FACEBUFFERC", "faceBuffer.c"),
        ("VAR_FACEBUFFERD", "faceBuffer.d"),
        ("VAR_FACEBUFFERFLAG", "faceBuffer.flag"),
        ("VAR_FACEBUFFERCOLOR", "faceBuffer.color"),
    ]:
        put(a, b)

    # --- engine ---
    for a, b in [
        ("VAR_ENGINESTATE", "engine.state"),
        ("VAR_ENGINEMESSAGE", "engine.message"),
        ("VAR_ENGINELANGUAGE", "engine.language"),
        ("VAR_SAVERAM", "saveRAM"),
        ("VAR_ENGINEONLINEACTIVE", "engine.onlineActive"),
        ("VAR_ENGINESFXVOLUME", "engine.sfxVolume"),
        ("VAR_ENGINEBGMVOLUME", "engine.bgmVolume"),
        ("VAR_ENGINEPLATFORMID", "engine.platformID"),
        ("VAR_ENGINETRIALMODE", "engine.trialMode"),
        ("VAR_ENGINEHAPTICSENABLED", "engine.hapticsEnabled"),
    ]:
        put(a, b)

    # --- guard: every mapping must name a variable RSDKv4 actually defines ---
    unknown = sorted(v for v in m.values() if v not in v4)
    if unknown:
        sys.exit("mapping names that RSDKv4 does not define:\n  " + "\n  ".join(unknown))

    stale = sorted(k for k in m if k not in v3)
    if stale:
        sys.exit("mapping keys absent from the RSDKv3 enum:\n  " + "\n  ".join(stale))

    return m


def main():
    v3_lines = read(V3_SRC)
    v4_lines = read(V4_SRC)
    v3 = parse_v3_enum(v3_lines)
    v4 = parse_v4_table(v4_lines)
    mapping = build_mapping(v3, v4)

    names = [mapping.get(n) for n in v3]
    mapped = sum(1 for n in names if n)
    unmapped = [v3[i] for i, n in enumerate(names) if not n]

    out = []
    out.append("// <auto-generated>")
    out.append("//     Produced by scripts/gen_rsdkv3_variables.py from")
    out.append("//     RSDKV3/RSDKv3/Script.cpp and RSDKV4-Decompilation/RSDKv4/Script.cpp.")
    out.append("//     Edit those mappings in the generator, not this file.")
    out.append("// </auto-generated>")
    out.append("")
    out.append("// Nullable reference types are used here to mark the variables RSDKv3")
    out.append("// exposes but RSDKv4 has no equivalent for.")
    out.append("#nullable enable")
    out.append("")
    out.append("namespace SonicHybridRsdk.Generator;")
    out.append("")
    out.append("/// <summary>RSDKv3 script variable table, derived from the engine source.</summary>")
    out.append("internal static class RsdkV3Variables")
    out.append("{")
    out.append("    /// <summary>ScrVariable enum member names, in declaration order.</summary>")
    out.append("    public static readonly string[] EnumNames =")
    out.append("    {")
    for i in range(0, len(v3), 4):
        out.append("        " + " ".join('"%s",' % v3[j] for j in range(i, min(i + 4, len(v3)))))
    out.append("    };")
    out.append("")
    out.append("    /// <summary>")
    out.append("    /// RSDKv4 name for each RSDKv3 variable, by index. Entries that have no")
    out.append("    /// RSDKv4 equivalent are <c>null</c> and must be reported rather than")
    out.append("    /// guessed at.")
    out.append("    /// </summary>")
    out.append("    public static readonly string?[] V4Names =")
    out.append("    {")
    for i in range(0, len(names), 3):
        cells = []
        for j in range(i, min(i + 3, len(names))):
            cells.append('"%s",' % names[j] if names[j] else "null,")
        out.append("        " + " ".join(cells))
    out.append("    };")
    out.append("}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    text = "\n".join(out) + "\n"
    old = None
    if os.path.exists(OUT):
        with io.open(OUT, encoding="utf-8") as fh:
            old = fh.read()
    if old != text:
        with io.open(OUT, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        print("wrote " + os.path.relpath(OUT, ROOT))
    else:
        print("up to date: " + os.path.relpath(OUT, ROOT))

    print("RSDKv3 enum members : %d" % len(v3))
    print("RSDKv4 table entries: %d" % len(v4))
    print("mapped to RSDKv4    : %d" % mapped)
    print("no equivalent (%d):" % len(unmapped))
    for u in unmapped:
        print("  " + u)


if __name__ == "__main__":
    main()