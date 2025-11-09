// RSDKv4Wrapper.cpp - C wrapper for P/Invoke from C# Custom Client
#include "RSDKV4-Decompilation/RSDKv4/RetroEngine.hpp"
#include <SDL2/SDL.h>
#include <string.h>
#include <stdio.h>

extern "C" {

// Global engine instance
static RetroEngine* engineInstance = nullptr;
static bool sdlInitialized = false;

#ifdef _WIN32
    #define EXPORT __declspec(dllexport)
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

EXPORT int InitRSDKv4(const char* dataPath) {
    try {
        if (engineInstance != nullptr) {
            return 1; // Already initialized
        }

        // Initialize SDL subsystems if not already done
        if (!sdlInitialized) {
            if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_AUDIO | SDL_INIT_GAMECONTROLLER) < 0) {
                fprintf(stderr, "SDL_Init failed: %s\n", SDL_GetError());
                return 0;
            }
            sdlInitialized = true;
        }

        // Create new RetroEngine instance
        engineInstance = new RetroEngine();
        
        // Initialize engine - this sets up the display and subsystems
        if (!engineInstance->Init()) {
            delete engineInstance;
            engineInstance = nullptr;
            fprintf(stderr, "RetroEngine::Init() failed\n");
            return 0;
        }

        // Load game config from the .rsdk file
        if (!engineInstance->LoadGameConfig(dataPath)) {
            engineInstance->Release();
            delete engineInstance;
            engineInstance = nullptr;
            fprintf(stderr, "LoadGameConfig(%s) failed\n", dataPath);
            return 0;
        }

        // Initialize first stage
        engineInstance->InitFirstStage();
        
        return 1; // Success
    }
    catch (const std::exception& e) {
        fprintf(stderr, "Exception in InitRSDKv4: %s\n", e.what());
        if (engineInstance) {
            delete engineInstance;
            engineInstance = nullptr;
        }
        return 0;
    }
    catch (...) {
        fprintf(stderr, "Unknown exception in InitRSDKv4\n");
        if (engineInstance) {
            delete engineInstance;
            engineInstance = nullptr;
        }
        return 0; // Failure
    }
}

EXPORT void UpdateRSDKv4() {
    if (engineInstance == nullptr) {
        return;
    }

    try {
        // Process one frame of game logic
        engineInstance->ProcessEvents();
    }
    catch (...) {
        // Silently handle exceptions to prevent crashes
    }
}

EXPORT int IsGameComplete() {
    if (engineInstance == nullptr) {
        return 0;
    }

    // Check if game is in credits/ending state
    // This is a placeholder - actual completion detection would check game state
    return 0;
}

EXPORT void CleanupRSDKv4() {
    if (engineInstance != nullptr) {
        try {
            engineInstance->Release();
        }
        catch (...) {
            // Ignore cleanup errors
        }
        delete engineInstance;
        engineInstance = nullptr;
    }
    
    // Don't quit SDL here - it might be shared with other engines
}

} // extern "C"
