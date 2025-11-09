// RSDKv4Wrapper.cpp - C wrapper for P/Invoke from C# Custom Client
#include "RSDKV4-Decompilation/RSDKv4/RetroEngine.hpp"
#include <SDL2/SDL.h>
#include <string.h>
#include <stdio.h>

// Declare external symbols from RSDKv4
extern RetroEngine Engine;
extern bool ProcessEvents();

extern "C" {

static bool engineInitialized = false;

#ifdef _WIN32
    #define EXPORT __declspec(dllexport)
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

EXPORT int InitRSDKv4(const char* dataPath) {
    try {
        if (engineInitialized) {
            return 1; // Already initialized
        }

        // Initialize the global Engine instance
        Engine.Init();
        
        engineInitialized = true;
        return 1; // Success
    }
    catch (const std::exception& e) {
        fprintf(stderr, "Exception in InitRSDKv4: %s\n", e.what());
        engineInitialized = false;
        return 0;
    }
    catch (...) {
        fprintf(stderr, "Unknown exception in InitRSDKv4\n");
        engineInitialized = false;
        return 0; // Failure
    }
}

EXPORT void UpdateRSDKv4() {
    if (!engineInitialized) {
        return;
    }

    try {
        // Process one frame of game logic using the global ProcessEvents function
        ProcessEvents();
    }
    catch (...) {
        // Silently handle exceptions to prevent crashes
    }
}

EXPORT int IsGameComplete() {
    if (!engineInitialized) {
        return 0;
    }

    // Check if game is in credits/ending state
    // This is a placeholder - actual completion detection would check game state
    return 0;
}

EXPORT void CleanupRSDKv4() {
    if (engineInitialized) {
        try {
            // Set running to false to stop the engine
            Engine.running = false;
        }
        catch (...) {
            // Ignore cleanup errors
        }
        engineInitialized = false;
    }
    
    // Don't quit SDL here - it might be shared with other engines
}

} // extern "C"
