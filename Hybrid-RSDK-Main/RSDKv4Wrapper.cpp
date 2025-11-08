// RSDKv4Wrapper.cpp - C wrapper for P/Invoke from C# Custom Client
#include "RSDKV4-Decompilation/RSDKv4/RetroEngine.hpp"
#include <string.h>

extern "C" {

// Global engine instance
RetroEngine* engineInstance = nullptr;

#ifdef _WIN32
    #define EXPORT __declspec(dllexport)
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

EXPORT int InitRSDKv4(const char* dataPath) {
    try {
        if (!engineInstance) {
            engineInstance = new RetroEngine();
        }
        
        // Set the game path
        if (dataPath) {
            strncpy(Engine.dataFile[0], dataPath, 0x80 - 1);
            Engine.dataFile[0][0x80 - 1] = '\0';
        }
        
        // Initialize the engine
        engineInstance->Init();
        
        return 1; // Success
    }
    catch (...) {
        return 0; // Failure
    }
}

EXPORT void UpdateRSDKv4() {
    if (engineInstance) {
        engineInstance->Run();
    }
}

EXPORT void CleanupRSDKv4() {
    if (engineInstance) {
        delete engineInstance;
        engineInstance = nullptr;
    }
}

} // extern "C"
