// RSDKv4Wrapper.cpp - C wrapper for P/Invoke from C# Custom Client
#include "RSDKV4-Decompilation/RSDKv4/RetroEngine.hpp"
#include "RSDKV4-Decompilation/RSDKv4/Drawing.hpp"
#include "RSDKV4-Decompilation/RSDKv4/Audio.hpp"
#include "RSDKV4-Decompilation/RSDKv4/Scene.hpp"
#include <SDL2/SDL.h>
#include <string.h>
#include <stdio.h>

#ifdef _WIN32
#include <direct.h>  // For _getcwd, _chdir on Windows
#define getcwd _getcwd
#define chdir _chdir
#else
#include <unistd.h>  // For getcwd, chdir on Unix
#endif

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

// Forward declarations
EXPORT void CleanupRSDKv4();

EXPORT int InitRSDKv4(const char* dataPath) {
    try {
        if (engineInitialized) {
            fprintf(stderr, "RSDK already initialized, cleaning up first...\n");
            CleanupRSDKv4();
        }

        if (!dataPath || strlen(dataPath) == 0) {
            fprintf(stderr, "ERROR: No data path provided to InitRSDKv4\n");
            return 0;
        }

        fprintf(stderr, "InitRSDKv4: Loading game from %s\n", dataPath);

        // Extract directory and filename from dataPath
        char dataDir[512] = "";
        char dataFile[128] = "";
        
        const char* lastSlash = strrchr(dataPath, '/');
        const char* lastBackslash = strrchr(dataPath, '\\');
        const char* separator = lastSlash > lastBackslash ? lastSlash : lastBackslash;
        
        if (separator) {
            // Split into directory and filename
            size_t dirLen = separator - dataPath;
            strncpy(dataDir, dataPath, dirLen);
            dataDir[dirLen] = '\0';
            strncpy(dataFile, separator + 1, sizeof(dataFile) - 1);
            dataFile[sizeof(dataFile) - 1] = '\0';
        } else {
            // No directory separator, use current directory
            strcpy(dataFile, dataPath);
        }

        // Verify the file exists before proceeding
        FILE* testFile = fopen(dataPath, "rb");
        if (!testFile) {
            fprintf(stderr, "ERROR: Cannot open file: %s\n", dataPath);
            fprintf(stderr, "Please verify the .rsdk file exists and is accessible\n");
            return 0;
        }
        fclose(testFile);
        fprintf(stderr, "Verified .rsdk file exists: %s\n", dataPath);

        // Change to the data directory so Engine.Init() can find the file
        char originalDir[512];
        if (getcwd(originalDir, sizeof(originalDir)) == NULL) {
            fprintf(stderr, "ERROR: Failed to get current directory\n");
            return 0;
        }

        if (strlen(dataDir) > 0) {
            if (chdir(dataDir) != 0) {
                fprintf(stderr, "ERROR: Failed to change to directory: %s\n", dataDir);
                return 0;
            }
            fprintf(stderr, "Changed directory to: %s\n", dataDir);
        }

        // Verify file is accessible from new directory
        testFile = fopen(dataFile, "rb");
        if (!testFile) {
            fprintf(stderr, "ERROR: Cannot open file '%s' from directory '%s'\n", dataFile, dataDir);
            fprintf(stderr, "Current working directory after chdir: ");
            char cwd[512];
            if (getcwd(cwd, sizeof(cwd))) {
                fprintf(stderr, "%s\n", cwd);
            } else {
                fprintf(stderr, "(unknown)\n");
            }
            chdir(originalDir);
            return 0;
        }
        fclose(testFile);
        fprintf(stderr, "Verified file accessible as: %s\n", dataFile);

        // Set the data file name in Engine
        strncpy(Engine.dataFile[0], dataFile, sizeof(Engine.dataFile[0]) - 1);
        Engine.dataFile[0][sizeof(Engine.dataFile[0]) - 1] = '\0';
        fprintf(stderr, "Set Engine.dataFile[0] to: %s\n", Engine.dataFile[0]);

        // Initialize SDL first
        if (SDL_Init(SDL_INIT_EVERYTHING) < 0) {
            fprintf(stderr, "ERROR: SDL_Init failed: %s\n", SDL_GetError());
            chdir(originalDir);
            return 0;
        }
        fprintf(stderr, "SDL initialized successfully\n");

        // Initialize the global Engine instance
        // This calls CheckRSDKFile, LoadGameConfig, InitRenderDevice, InitAudioPlayback, InitFirstStage
        fprintf(stderr, "Calling Engine.Init()...\n");
        fprintf(stderr, "Engine.dataFile[0] = '%s'\n", Engine.dataFile[0]);
        fprintf(stderr, "Current working directory: %s\n", dataDir[0] ? dataDir : originalDir);
        
        Engine.Init();
        
        fprintf(stderr, "Engine.Init() completed. Engine.running = %d, Engine.initialised = %d\n", 
                Engine.running, Engine.initialised);
        fprintf(stderr, "Engine.usingDataFile = %d, Engine.usingBytecode = %d\n",
                Engine.usingDataFile, Engine.usingBytecode);
        
        // Don't restore directory - the engine needs to stay in the data directory
        // to access game resources (sprites, music, etc.)
        // if (chdir(originalDir) != 0) {
        //     fprintf(stderr, "WARNING: Failed to restore directory: %s\n", originalDir);
        // }

        if (!Engine.running) {
            fprintf(stderr, "ERROR: Engine failed to start - Engine.running is false\n");
            fprintf(stderr, "Possible causes:\n");
            fprintf(stderr, "  1. .rsdk file is invalid or corrupted\n");
            fprintf(stderr, "  2. .rsdk file is missing Data/Game/GameConfig.bin\n");
            fprintf(stderr, "  3. Failed to initialize render device (SDL window)\n");
            fprintf(stderr, "  4. Failed to initialize audio playback\n");
            fprintf(stderr, "Check the .rsdk file with a valid RSDK game data file\n");
            // Restore directory on failure
            chdir(originalDir);
            engineInitialized = false;
            return 0;
        }

        engineInitialized = true;
        fprintf(stderr, "InitRSDKv4: Successfully initialized and started engine\n");
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
