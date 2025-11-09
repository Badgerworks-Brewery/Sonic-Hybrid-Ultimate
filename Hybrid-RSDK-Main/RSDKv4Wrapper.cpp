// RSDKv4Wrapper.cpp - C wrapper for P/Invoke from C# Custom Client
#include "sonic-hybrid/HybridEngine.hpp"
#include <string.h>
#include <SDL2/SDL.h>

using namespace SonicHybrid;

extern "C" {

// Global hybrid engine instance
static HybridEngine* hybridEngine = nullptr;
static bool sdlInitialized = false;

#ifdef _WIN32
    #define EXPORT __declspec(dllexport)
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

EXPORT int InitRSDKv4(const char* dataPath) {
    try {
        if (hybridEngine != nullptr) {
            return 1; // Already initialized
        }

        // Initialize SDL if not already initialized
        if (!sdlInitialized) {
            if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_EVENTS | SDL_INIT_GAMECONTROLLER) < 0) {
                return 0; // SDL initialization failed
            }
            sdlInitialized = true;
        }

        // Get hybrid engine singleton instance
        hybridEngine = &HybridEngine::Instance();
        
        // Initialize hybrid engine (initializes RetroEngine + state/transition managers)
        if (!hybridEngine->Initialize()) {
            hybridEngine = nullptr;
            return 0;
        }

        // Load game data
        if (!hybridEngine->LoadGame(std::string(dataPath))) {
            hybridEngine->Cleanup();
            hybridEngine = nullptr;
            return 0;
        }
        
        return 1; // Success
    }
    catch (...) {
        if (hybridEngine) {
            hybridEngine->Cleanup();
            hybridEngine = nullptr;
        }
        return 0; // Failure
    }
}

EXPORT void UpdateRSDKv4() {
    if (hybridEngine == nullptr) {
        return;
    }

    try {
        // Update hybrid engine (handles game logic, state management, transitions, completion detection)
        hybridEngine->Update();
    }
    catch (...) {
        // Silently handle exceptions to prevent crashes
    }
}

EXPORT int IsGameComplete() {
    if (hybridEngine == nullptr) {
        return 0;
    }

    return hybridEngine->IsGameComplete() ? 1 : 0;
}

EXPORT void CleanupRSDKv4() {
    if (hybridEngine != nullptr) {
        hybridEngine->Cleanup();
        hybridEngine = nullptr;
    }
    
    if (sdlInitialized) {
        SDL_Quit();
        sdlInitialized = false;
    }
}

} // extern "C"
