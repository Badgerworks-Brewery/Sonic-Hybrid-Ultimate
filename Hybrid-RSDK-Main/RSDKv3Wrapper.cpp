#include <cstring>

// Platform-specific export macros
#ifdef _WIN32
    #define EXPORT __declspec(dllexport)
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

// C API exports for P/Invoke from C#
extern "C" {
    // Initialize RSDKv3 with the game data path
    // Returns 1 on success, 0 on failure
    EXPORT int InitRSDKv3(const char* dataPath) {
        // TODO: Call actual RSDKv3 initialization when available
        // For now, return failure to indicate RSDKv3 isn't fully integrated yet
        return 0;
    }

    // Update RSDKv3 game loop (one frame)
    EXPORT void UpdateRSDKv3() {
        // TODO: Call actual RSDKv3 update when available
    }

    // Cleanup and shutdown RSDKv3
    EXPORT void CleanupRSDKv3() {
        // TODO: Call actual RSDKv3 cleanup when available
    }
}
