// RSDKv4Wrapper.cpp - C wrapper for P/Invoke from C# Custom Client
#include "RSDKV4-Decompilation/RSDKv4/RetroEngine.hpp"
#include <string.h>
#include <SDL2/SDL.h>

extern "C" {

// Global engine instance
static bool sdlInitialized = false;

#ifdef _WIN32
    #define EXPORT __declspec(dllexport)
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

EXPORT int InitRSDKv4(const char* dataPath) {
    try {
        // Initialize SDL if not already initialized
        if (!sdlInitialized) {
            if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_EVENTS | SDL_INIT_GAMECONTROLLER) < 0) {
                return 0; // SDL initialization failed
            }
            sdlInitialized = true;
        }
        
        // Set the game path
        if (dataPath) {
            strncpy(Engine.dataFile[0], dataPath, 0x80 - 1);
            Engine.dataFile[0][0x80 - 1] = '\0';
        }
        
        // Initialize the engine systems
        Engine.Init();
        Engine.LoadGameConfig("Data/Game/GameConfig.bin");
        InitFirstStage();
        
        return 1; // Success
    }
    catch (...) {
        return 0; // Failure
    }
}

EXPORT void UpdateRSDKv4() {
    // Process one frame of the game
    // Don't call Run() - that's the main loop that blocks
    ProcessEvents();
}

EXPORT void CleanupRSDKv4() {
    if (sdlInitialized) {
        SDL_Quit();
        sdlInitialized = false;
    }
}

} // extern "C"
