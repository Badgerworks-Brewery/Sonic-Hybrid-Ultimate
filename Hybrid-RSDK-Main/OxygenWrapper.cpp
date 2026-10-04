// OxygenWrapper.cpp - embedding Sonic 3 A.I.R. (Oxygen Engine) in Sonic Hybrid Ultimate
//
// The project decision is that all four games' compiled code lives in one executable. For
// Sonic 3 that means A.I.R.'s engine has to be *inside* this binary, not launched beside it.
//
// Provenance of the current state, because it is easy to mistake for more than it is:
//
//   - `oxygen` links and `OxygenEngine.dll` builds. That much is real.
//   - But the DLL was 16,384 bytes against a 51,549,744 byte oxygen.lib, because
//     `oxygen` is a static library and nothing here referenced any of its symbols. Not one
//     byte of A.I.R. was in the output. A green build containing none of the engine.
//   - So the symbol-collision question is still open. Two engines' file-scope globals, two
//     SDL copies and two audio stacks only collide once something actually pulls A.I.R.'s
//     objects in. Until then the link proves nothing about coexistence.
//
// This file is therefore a *probe*, not a finished wrapper. Its job is to name enough real
// A.I.R. types and functions that the linker is forced to resolve them, so that the
// collisions surface now - as link errors naming symbols - rather than later as a runtime
// mystery. Every symbol referenced here is one the wrapper will genuinely need.
//
// Once linking is clean, replace this with the real seam: implement
// EngineDelegateInterface so the host supplies the GUI backend and audio output, and drive
// EngineMain from the dispatcher that chooses between the four games. See docs/STATUS.md.

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>

// Export macro for this wrapper's own entry points. Deliberately defined without pulling in
// <windows.h>: see the ERROR-macro note below. Declspec is spelled out directly so no
// Windows header is needed at all.
#ifdef _WIN32
    #define EXPORT __declspec(dllexport)
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

// Real A.I.R. headers. EngineMain.h declares EngineDelegateInterface, EngineMain and
// shutdownProcess(); EngineDelegate.h is the concrete delegate the game supplies.
//
// Included BEFORE <windows.h>, deliberately. windows.h defines ERROR as a macro
// (`#define ERROR 0`), and A.I.R. has an enumerator of that name:
//
//   librmx/source/rmxbase/base/ErrorHandler.h:59-64
//     enum class ErrorSeverity { INFO, WARNING, ERROR };
//
// With <windows.h> first, that expands to `enum class ErrorSeverity { INFO, WARNING, 0 }`
// and the header fails to parse with a cascade of syntax errors starting at
// ErrorHandler.h(63,3) - none of which mention macros. This is the first genuine
// Windows-versus-A.I.R. collision this integration has hit, and it is a quiet one: the
// error points at AIR's source, not at the include order that caused it.
// NB: including rmxbase.h first does NOT help - tried, same two C3867 errors. Include order
// is eliminated alongside compile flags, SDL shadowing and /external:I. See docs/STATUS.md.
#include "rmxbase.h"

#include "oxygen/application/EngineMain.h"
#include "sonic3air/EngineDelegate.h"

extern "C" {

// Probe: report what the link actually resolved.
//
// The previous version of this file answered "is AIR available?" by checking for an external
// executable, which is why it could report stub mode while the build looked successful. The
// question is now answerable in-process, and the honest answer includes whether A.I.R. code
// is present at all - a wrapper that reports "available" while the DLL contains none of the
// engine is exactly the failure that has already happened once here.
EXPORT int OxygenProbe_HasEngineCode(void)
{
    // Taking the address of a real A.I.R. symbol forces the linker to resolve it. If this
    // compiles and links, coexistence works at the symbol level; that is a genuine result
    // and not something the previous stub could establish.
    static void* sAnchor = (void*)&EngineMain::shutdownProcess;
    return sAnchor != nullptr;
}

EXPORT const char* OxygenProbe_EngineName(void)
{
    return "Sonic 3 A.I.R. (Oxygen) - embedded";
}

} // extern "C"

namespace {

// Minimal delegate, present so the A.I.R. interface itself is compiled rather than merely
// declared. A real implementation supplies GuiBase&, AudioOutBase&, the script bindings and
// the frame callbacks; stubbing those here would defeat the purpose of the probe, so this
// deliberately does not pretend to be one. It exists to force the vtable and the interface's
// symbol requirements into the link.
class ProbeDelegate : public EngineDelegateInterface
{
public:
    const AppMetaData& getAppMetaData() override { return mMetaData; }
    GuiBase&  createGameApp() override  { return *reinterpret_cast<GuiBase*>(this); }
    AudioOutBase& createAudioOut() override { return *reinterpret_cast<AudioOutBase*>(this); }

    bool onEnginePreStartup() override { return false; }
    bool setupCustomGameProfile() override { return false; }
    void startupGame(EmulatorInterface&) override {}
    void shutdownGame() override {}
    void updateGame(float) override {}
    void registerScriptBindings(struct lemon::Module&) override {}
    void registerNativizedCode(struct lemon::Program&) override {}
    void onRuntimeInit(CodeExec&) override {}
    void onPreFrameUpdate() override {}
    void onPostFrameUpdate() override {}
    void onControlsUpdate() override {}
    void onPreSaveStateLoad() override {}
    bool mayLoadScriptMods() override { return false; }
    bool allowModdedData() override { return false; }
    bool useDeveloperFeatures() override { return false; }
    void onActiveModsChanged() override {}
    void onStartNetplayGame(bool) override {}
    void onStopNetplayGame(bool) override {}
    void serializeGameSettings(struct VectorBinarySerializer&) override {}
    void onGameRecordingHeaderLoaded(const std::string&, const struct std::vector<uint8>&) override {}
    void onGameRecordingHeaderSave(struct std::vector<uint8>&) override {}
    Font& getDebugFont(int) override { return *reinterpret_cast<Font*>(this); }
    void fillDebugVisualization(Bitmap&, int&) override {}

private:
    AppMetaData mMetaData;
};

} // namespace

extern "C" EXPORT int OxygenProbe_BuildInterface(void)
{
    // Instantiating forces the vtable to be emitted and referenced, which is what proves the
    // pure-virtual set is satisfiable and that its symbols resolve.
    ProbeDelegate d;
    return d.getAppMetaData().mBuildVersionNumber == 0 ? 1 : 1;
}