// OxygenWrapper.cpp - running Sonic 3 A.I.R. (Oxygen Engine) inside Sonic Hybrid Ultimate
//
// WHAT THIS FILE IS
//
// The project decision is that all four games' compiled code lives in one executable. For Sonic
// 3 that means A.I.R.'s engine is *inside* this binary, not launched beside it. This file is the
// seam: it starts and stops an A.I.R. session on demand, so the dispatcher above both engines
// can hand the window over from Sonic 1/2 to Sonic 3 and back.
//
// WHAT IT IS NOT
//
// It does not arbitrate the window, the audio device or the main loop. Both engines want all
// three, and that arbitration is the next piece of design work, not something to fake here. What
// this file does provide is the ability to start a real A.I.R. session and stop it cleanly, and
// to answer truthfully what is and is not present in the binary.
//
// PROVENANCE, because the previous version of this file was easy to mistake for more than it was
//
// It was a *probe*: it implemented EngineDelegateInterface by reinterpret_casting `this` to
// GuiBase* and AudioOutBase*, purely to force the vtable into the link so collisions would show
// up as link errors. It worked as a probe - it measured zero collisions, and that measurement is
// what the four `HYBRID_*_TARGET` variables exist for - but it was not runnable and never
// claimed to be. Those two casts are gone.
//
// The game layer those two functions needed is now a real library. `Oxygen/sonic3air/source/
// sonic3air/*.cpp` used to be compiled only into the Sonic3AIR executable, which meant
// EngineDelegate, GameApp and AudioOut - the actual implementations of the three interfaces -
// existed nowhere linkable. Patch 0013 compiles them into `sonic3air_game` as well. So this
// wrapper now instantiates A.I.R.'s own EngineDelegate instead of hand-rolling a worse one.
//
// WHY THE INCLUDES ARE IN THIS ORDER
//
// A.I.R.'s headers must come before <windows.h>, always. windows.h defines ERROR as a macro:
//
//   librmx/source/rmxbase/base/ErrorHandler.h:59-64
//     enum class ErrorSeverity { INFO, WARNING, ERROR };
//
// With <windows.h> first, that expands to `enum class ErrorSeverity { INFO, WARNING, 0 }` and the
// header fails to parse with a cascade of syntax errors starting at ErrorHandler.h(63,3) - none
// of which mention macros. The error points at A.I.R.'s source, not at the include order that
// caused it. This is the first genuine Windows-versus-A.I.R. collision this integration hit, and
// it is a quiet one.
//
// Consequently the EXPORT macro below is spelled out rather than taken from <windows.h>, so
// including <windows.h> is not needed at all.
//
// Do not add rmxbase.h. Including it instantiates STRING::endsWith in
// rmxbase/memory/StringImpl.h, whose body passed the member function str.data where a pointer is
// wanted (StringImpl.h:765). str is std::basic_string_view<CHAR> with CHAR a template parameter,
// so the expression is type-dependent and MSVC leaves it alone until instantiation. No A.I.R.
// translation unit instantiates it; this one did, and produced:
//
//   error C3867: std::basic_string_view<char,...>::data: non-standard syntax
//
// Patch 0006 fixes that. The explicit includes below are the same set A.I.R.'s own
// EngineDelegate.cpp uses, which is deliberate: declaring these types by hand is invalid, since
// an elaborated-type-specifier cannot carry a qualified name - `struct lemon::Program` is not
// legal C++, MSVC accepts it anyway, and the resulting never-defined type then fails deep inside
// a template with an error that mentions nothing useful.

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>

#ifdef _WIN32
    #define EXPORT __declspec(dllexport)
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

// A.I.R. engine interface and the concrete game-layer types it needs.
#include <lemon/program/Module.h>
#include <lemon/program/Program.h>
#include <lemon/runtime/RuntimeFunction.h>
#include "oxygen/simulation/EmulatorInterface.h"
#include "oxygen/application/EngineMain.h"

// The real implementations. These come from sonic3air_game, not from oxygen: oxygen provides
// the abstract interfaces, this directory provides the classes that satisfy them.
#include "sonic3air/EngineDelegate.h"
#include "sonic3air/GameArgumentsReader.h"

extern "C" {

// ---------------------------------------------------------------------------------------------
// Presence
//
// Kept from the probe version and still meaningful, because a wrapper that reports "A.I.R.
// available" while the binary contains none of the engine is a failure mode that has already
// happened here twice - once as a 16,384-byte DLL with no engine in it, and once as a
// coexistence link where rsdk_core contributed nothing.
// ---------------------------------------------------------------------------------------------

EXPORT int OxygenProbe_HasEngineCode(void)
{
    // Taking the address of a real A.I.R. symbol forces the linker to resolve it. If this
    // compiles and links, the engine is present at the symbol level.
    static void* sAnchor = (void*)&EngineMain::shutdownProcess;
    return sAnchor != nullptr;
}

EXPORT const char* OxygenProbe_EngineName(void)
{
    return "Sonic 3 A.I.R. (Oxygen) - embedded";
}

// ---------------------------------------------------------------------------------------------
// Session control
//
// The shape of a session is A.I.R.'s own, from Oxygen/sonic3air/source/sonic3air/main.cpp:
//
//     EngineMain::earlySetup();
//     GameArgumentsReader arguments;  arguments.read(argc, argv);
//     EngineDelegate myDelegate;
//     EngineMain myMain(myDelegate, arguments);
//     myMain.execute();
//     EngineMain::shutdownProcess();
//
// earlySetup and shutdownProcess are static and process-global. They are called once per
// process, not once per session - see patch 0001, which split EngineMain::shutdown() so a
// session can be torn down without destroying process-global state. That is what makes the
// Hybrid possible at all: leaving Sonic 3 and coming back needs the engine to be re-enterable,
// and before that patch it was strictly one-shot per process.
// ---------------------------------------------------------------------------------------------

namespace {

// One session's worth of A.I.R. state.
//
// EngineMain's constructor takes a reference to the delegate and to the arguments, and holds
// them, so both must outlive it. They are members here rather than locals for that reason - the
// ordering of members is load-bearing, and it is declared delegate-then-arguments-then-main to
// match the order in which they must be constructed and reverse-destroyed.
struct OxygenSession
{
    GameArgumentsReader   mArguments;
    EngineDelegate        mDelegate;
    EngineMain            mEngine;

    // EngineMain's constructor takes a delegate and an arguments reader and *holds* both, so
    // they have to be constructed first and outlive it. That makes declaration order
    // load-bearing: mArguments, then mDelegate, then mEngine. They are members rather than
    // locals in Oxygen_StartSession for exactly this reason.
    explicit OxygenSession()
        : mEngine(mDelegate, mArguments)
    {
    }

    // Non-copyable and non-movable. EngineMain is a SingleInstance, so two of them in one
    // process is a precondition violation rather than something the compiler would catch.
    OxygenSession(const OxygenSession&) = delete;
    OxygenSession& operator=(const OxygenSession&) = delete;
};

// Frame limit for a session. Static on EngineDelegate because A.I.R.'s own self-test has to set
// it from main() before the delegate exists (EngineDelegate.h:68). Read in
// EngineDelegate::onPostFrameUpdate, which stops the session when the count is reached - and
// only when it is greater than zero, so 0 means "run until told to stop", which is what the
// dispatcher wants.
//
// Set through this file rather than through the arguments object: the value lives on the
// delegate, not on the reader. Getting that backwards is a compile error rather than a silent
// no-op, which is a better failure than the alternative.
static int sFrameLimit = 0;

// Created on first use and destroyed by Oxygen_ShutdownProcess(), so a session can span many
// calls without the engine seeing start/stop per frame.
OxygenSession* gSession = nullptr;

} // namespace

// Number of arguments to synthesise, because A.I.R.'s ArgumentsReader::read() takes argc/argv
// and the Hybrid has no command line for it - the game is chosen by the dispatcher, not by a
// switch. argv[0] is the only entry that must be meaningful: it becomes
// ArgumentsReader::mExecutableCallPath, which A.I.R. uses to locate data relative to itself.
// Passing the Hybrid's own path is deliberate and is what makes A.I.R. find a data folder that
// sits alongside the Hybrid rather than one it expects to be installed into.
//
// The project path is left empty on purpose. ArgumentsReader::read() treats any argument that
// does not start with '-' as the project path, and passing a non-existent one produces a
// confusing "cannot open file" from deep inside the pack loader rather than a clear error.
EXPORT int Oxygen_StartSession(const char* hostExecutablePath, int frameLimit)
{
    if (gSession != nullptr)
    {
        // Already running. Returning failure rather than silently tearing down a live session:
        // the dispatcher is supposed to stop a game before starting another, and a silent
        // restart here would hide a bug in it.
        return 2;
    }

    // earlySetup() installs the process-global handlers and the log display. It is static and
    // idempotent in intent, but it is only called once per process - see Oxygen_Shutdown.
    EngineMain::earlySetup();

    gSession = new OxygenSession();
    if (gSession == nullptr)
    {
        return 3;
    }

    // A.I.R. keeps its own persistent data under %APPDATA%/Sonic3AIR, so it needs no path from
    // the host here. The one input that does matter is argv[0].
    char* fakeArgv[2] = { nullptr, nullptr };
    char  hostPathBuffer[1024];
    if (hostExecutablePath != nullptr && hostExecutablePath[0] != '\0')
    {
        strncpy_s(hostPathBuffer, sizeof(hostPathBuffer), hostExecutablePath,
                  _TRUNCATE);
    }
    else
    {
        strncpy_s(hostPathBuffer, sizeof(hostPathBuffer), "SonicHybridUltimate.exe", _TRUNCATE);
    }
    fakeArgv[0] = hostPathBuffer;

    gSession->mArguments.read(1, fakeArgv);

    // frameLimit is the same mechanism A.I.R.'s own restart self-test uses (patch 0002/0003)
    // to bound a session. A positive count makes the delegate stop the session by itself; zero
    // means "run until asked to stop", which is what the dispatcher wants.
    //
    // Must be set before execute(), because the delegate checks it from onPostFrameUpdate on
    // the very first frame - setting it afterwards would let the session run unbounded.
    sFrameLimit = (frameLimit > 0) ? frameLimit : 0;
    EngineDelegate::sSelfTestFrameLimit = sFrameLimit;

    // execute() blocks for the whole session - it owns the window, the audio device and the
    // frame loop until it returns or throws. That is the single most important fact about this
    // seam and the reason coexistence at run time is still open: this call does not return
    // while Sonic 3 is playing, so the dispatcher cannot run RSDKv4's loop in the meantime
    // without either threads or a fully nested loop. Deliberately left synchronous rather than
    // faked as asynchronous, because an async version that appeared to work would be worse
    // than an honest block.
    gSession->mEngine.execute();

    return 0;
}

// Stop a running session and release its state.
//
// Returns 1 if no session was running, 0 on success. shutdownProcess() is deliberately *not*
// called here: it is process-global, and calling it per session is what patch 0001 exists to
// avoid. It belongs in Oxygen_Shutdown, once per process.
EXPORT int Oxygen_StopSession(void)
{
    if (gSession == nullptr)
    {
        return 1;
    }

    // Kept until after the delete, so nothing observes a half-destroyed session, then cleared so
    // a callback asking whether a session is running during teardown gets "no" rather than a
    // pointer to an object mid-destruction.
    delete gSession;
    gSession = nullptr;
    sFrameLimit = 0;
    EngineDelegate::sSelfTestFrameLimit = 0;
    return 0;
}

// True when a session is live. Cheap and side-effect free, so a dispatcher can branch on it
// without triggering engine state.
EXPORT int Oxygen_SessionRunning(void)
{
    return gSession != nullptr ? 1 : 0;
}

// ---------------------------------------------------------------------------------------------
// Process lifetime
//
// Called once, from the Hybrid's own shutdown path, not once per game.
// ---------------------------------------------------------------------------------------------
EXPORT void Oxygen_ShutdownProcess(void)
{
    Oxygen_StopSession();
    EngineMain::shutdownProcess();
}

} // extern "C"