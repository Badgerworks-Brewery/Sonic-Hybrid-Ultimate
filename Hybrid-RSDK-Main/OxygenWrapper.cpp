// OxygenWrapper.cpp - P/Invoke wrapper for Sonic 3 AIR Oxygen Engine
// This wrapper provides a C interface for Sonic 3 & Knuckles integration
// 
// ARCHITECTURE NOTE:
// The Sonic 3 AIR engine (Oxygen) is a complex game engine that requires significant
// infrastructure to run. This wrapper provides integration modes:
// 1. Embedded mode (future): Full engine compiled in
// 2. External mode: Launch external Sonic 3 AIR process
// 3. Stub mode: Provide helpful guidance when neither is available
//
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>

#ifdef _WIN32
    #include <windows.h>
    #include <process.h>
    #include <signal.h>
    #define EXPORT __declspec(dllexport)
#else
    #include <unistd.h>
    #include <sys/types.h>
    #include <sys/wait.h>
    #include <signal.h>
    #define EXPORT __attribute__((visibility("default")))
#endif

extern "C" {

// Engine state
static bool oxygenInitialized = false;
static bool oxygenUsingStubMode = false;
static char romPath[512] = "";

#ifdef _WIN32
static HANDLE processHandle = NULL;
static DWORD processId = 0;
#else
static pid_t processId = 0;
#endif

// Logging callback
typedef void (*OxygenLogCallback)(const char* message);
static OxygenLogCallback logCallback = nullptr;

// Helper function to log messages
static void LogMessage(const char* format, ...) {
    char buffer[1024];
    va_list args;
    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);
    
    // Always output to stderr for debugging
    fprintf(stderr, "%s", buffer);
    
    // Also call the callback if set
    if (logCallback) {
        logCallback(buffer);
    }
}

// Set logging callback
EXPORT void SetOxygenLogCallback(OxygenLogCallback callback) {
    logCallback = callback;
}

// Check if the Oxygen engine data files are available (for embedded mode)
static bool CheckEmbeddedDataAvailable() {
    // Check for essential Sonic 3 AIR data files that would indicate embedded mode
    const char* dataPaths[] = {
        "Sonic 3 AIR Main/data/",
        "Sonic 3 AIR Main/scripts/",
        "../Sonic 3 AIR Main/data/",
        "../Sonic 3 AIR Main/scripts/",
        "data/sonic3air/",
        "../data/sonic3air/"
    };
    
    for (size_t i = 0; i < sizeof(dataPaths) / sizeof(dataPaths[0]); i++) {
        // Check if directory exists
#ifdef _WIN32
        DWORD attr = GetFileAttributesA(dataPaths[i]);
        if (attr != INVALID_FILE_ATTRIBUTES && (attr & FILE_ATTRIBUTE_DIRECTORY)) {
            LogMessage("OxygenEngine: Found data directory: %s\n", dataPaths[i]);
            return true;
        }
#else
        if (access(dataPaths[i], F_OK) == 0) {
            LogMessage("OxygenEngine: Found data directory: %s\n", dataPaths[i]);
            return true;
        }
#endif
    }
    
    return false;
}

// Find an external Sonic 3 AIR executable
static const char* FindExternalExecutable() {
    static char foundPath[1024] = "";
    
    // Check environment variable first
    const char* customPath = getenv("SONIC3AIR_PATH");
    if (customPath && strlen(customPath) > 0) {
        snprintf(foundPath, sizeof(foundPath), "%s/sonic3air.exe", customPath);
        FILE* test = fopen(foundPath, "rb");
        if (test) {
            fclose(test);
            return foundPath;
        }
#ifndef _WIN32
        snprintf(foundPath, sizeof(foundPath), "%s/sonic3air", customPath);
        test = fopen(foundPath, "rb");
        if (test) {
            fclose(test);
            return foundPath;
        }
#endif
    }
    
    // Search common paths
    const char* searchPaths[] = {
        // Project-relative paths
        "../Sonic 3 AIR Main/sonic3air.exe",
        "../Sonic 3 AIR Main/sonic3air",
        "Sonic 3 AIR Main/sonic3air.exe",
        "Sonic 3 AIR Main/sonic3air",
        // Built executable paths
        "../Sonic 3 AIR Main/Oxygen/sonic3air/bin/sonic3air.exe",
        "../Sonic 3 AIR Main/Oxygen/sonic3air/bin/sonic3air",
        // Vendor paths
        "../vendor/sonic3air/Oxygen/sonic3air/bin/sonic3air.exe",
        "../vendor/sonic3air/Oxygen/sonic3air/bin/sonic3air",
        // Current directory
        "sonic3air.exe",
        "sonic3air",
        // Common installation paths
#ifdef _WIN32
        "C:/Program Files/Sonic 3 AIR/sonic3air.exe",
        "C:/Program Files (x86)/Sonic 3 AIR/sonic3air.exe",
#else
        "/usr/local/bin/sonic3air",
        "/opt/sonic3air/sonic3air",
#endif
    };
    
    for (size_t i = 0; i < sizeof(searchPaths) / sizeof(searchPaths[0]); i++) {
        FILE* test = fopen(searchPaths[i], "rb");
        if (test) {
            fclose(test);
            strncpy(foundPath, searchPaths[i], sizeof(foundPath) - 1);
            foundPath[sizeof(foundPath) - 1] = '\0';
            return foundPath;
        }
    }
    
    return NULL;
}

EXPORT int InitOxygenEngine(const char* scriptPath) {
    if (!scriptPath || strlen(scriptPath) == 0) {
        LogMessage("OxygenEngine: No ROM path provided\n");
        return 0;
    }

    LogMessage("OxygenEngine: Initializing Sonic 3 & Knuckles integration...\n");
    LogMessage("OxygenEngine: ROM path: %s\n", scriptPath);
    
    // Validate ROM file exists and is readable
    FILE* romTest = fopen(scriptPath, "rb");
    if (!romTest) {
        LogMessage("OxygenEngine: ERROR - ROM file not found: %s\n", scriptPath);
        LogMessage("OxygenEngine: Please provide a valid Sonic 3 & Knuckles ROM file.\n");
        return 0;
    }
    
    // Check ROM file size (basic validation)
    fseek(romTest, 0, SEEK_END);
    long romSize = ftell(romTest);
    fclose(romTest);
    
    // Sonic 3 & Knuckles ROM should be around 4MB
    if (romSize < 1024 * 1024) {
        LogMessage("OxygenEngine: WARNING - ROM file seems too small (%ld bytes).\n", romSize);
        LogMessage("OxygenEngine: Expected a Sonic 3 & Knuckles ROM (~4MB).\n");
    } else {
        LogMessage("OxygenEngine: ROM file validated (%ld bytes)\n", romSize);
    }
    
    // Store the ROM path
    strncpy(romPath, scriptPath, sizeof(romPath) - 1);
    romPath[sizeof(romPath) - 1] = '\0';
    
    // Check for embedded data availability
    bool hasEmbeddedData = CheckEmbeddedDataAvailable();
    
    // Try to find external executable
    const char* exePath = FindExternalExecutable();
    
    if (hasEmbeddedData) {
        // TODO: Future - implement embedded Oxygen engine mode
        // For now, we'll still need the external executable
        LogMessage("OxygenEngine: Found Sonic 3 AIR data files\n");
    }
    
    if (exePath) {
        LogMessage("OxygenEngine: Found Sonic 3 AIR at: %s\n", exePath);
        
#ifdef _WIN32
        // Launch Sonic 3 AIR as a child process on Windows
        char cmdLine[1024];
        snprintf(cmdLine, sizeof(cmdLine), "\"%s\" --rom=\"%s\"", exePath, romPath);
        
        STARTUPINFOA si;
        PROCESS_INFORMATION pi;
        ZeroMemory(&si, sizeof(si));
        si.cb = sizeof(si);
        ZeroMemory(&pi, sizeof(pi));
        
        if (!CreateProcessA(NULL, cmdLine, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) {
            LogMessage("OxygenEngine: ERROR - Failed to launch Sonic 3 AIR (error %lu)\n", GetLastError());
            return 0;
        }
        
        processHandle = pi.hProcess;
        processId = pi.dwProcessId;
        CloseHandle(pi.hThread);
        
        LogMessage("OxygenEngine: Launched Sonic 3 AIR (PID: %lu)\n", processId);
#else
        // Launch Sonic 3 AIR as a child process on Unix/Linux
        processId = fork();
        
        if (processId < 0) {
            LogMessage("OxygenEngine: ERROR - fork() failed\n");
            return 0;
        }
        
        if (processId == 0) {
            // Child process
            char romArg[600];
            snprintf(romArg, sizeof(romArg), "--rom=%s", romPath);
            execl(exePath, exePath, romArg, (char*)NULL);
            _exit(1);
        }
        
        LogMessage("OxygenEngine: Launched Sonic 3 AIR (PID: %d)\n", processId);
#endif
        
        oxygenInitialized = true;
        oxygenUsingStubMode = false;
        return 1;
    }
    
    // No external executable found - enter stub mode with helpful guidance
    LogMessage("OxygenEngine: ============================================\n");
    LogMessage("OxygenEngine: Sonic 3 AIR Integration Status\n");
    LogMessage("OxygenEngine: ============================================\n");
    LogMessage("OxygenEngine: \n");
    LogMessage("OxygenEngine: The Sonic 3 AIR engine is not currently available.\n");
    LogMessage("OxygenEngine: \n");
    LogMessage("OxygenEngine: OPTION 1 - Download Sonic 3 AIR:\n");
    LogMessage("OxygenEngine:   Visit: https://sonic3air.org/\n");
    LogMessage("OxygenEngine:   Extract to: 'Sonic 3 AIR Main' folder\n");
    LogMessage("OxygenEngine:   Ensure sonic3air.exe is present\n");
    LogMessage("OxygenEngine: \n");
    LogMessage("OxygenEngine: OPTION 2 - Set Environment Variable:\n");
    LogMessage("OxygenEngine:   Set SONIC3AIR_PATH to your installation\n");
    LogMessage("OxygenEngine: \n");
    LogMessage("OxygenEngine: ROM file is ready: %s\n", romPath);
    LogMessage("OxygenEngine: ============================================\n");
    
    // In stub mode, we mark as "initialized" so the UI works, but running will be limited
    oxygenInitialized = true;
    oxygenUsingStubMode = true;
    
    return 1; // Return success to allow graceful handling in the C# layer
}

EXPORT void UpdateOxygenEngine() {
    if (!oxygenInitialized) {
        return;
    }
    
    // In stub mode, there's nothing to update
    if (oxygenUsingStubMode) {
        return;
    }
    
    // Check if the process is still running
#ifdef _WIN32
    if (processHandle) {
        DWORD exitCode;
        if (GetExitCodeProcess(processHandle, &exitCode)) {
            if (exitCode != STILL_ACTIVE) {
                LogMessage("OxygenEngine: Sonic 3 AIR process has exited\n");
                CloseHandle(processHandle);
                processHandle = NULL;
                oxygenInitialized = false;
            }
        }
    }
#else
    if (processId > 0) {
        int status;
        pid_t result = waitpid(processId, &status, WNOHANG);
        if (result > 0) {
            LogMessage("OxygenEngine: Sonic 3 AIR process has exited\n");
            processId = 0;
            oxygenInitialized = false;
        }
    }
#endif
}

EXPORT void CleanupOxygenEngine() {
    if (!oxygenInitialized) {
        return;
    }
    
    LogMessage("OxygenEngine: Cleaning up\n");
    
    if (!oxygenUsingStubMode) {
#ifdef _WIN32
        if (processHandle) {
            if (TerminateProcess(processHandle, 0)) {
                WaitForSingleObject(processHandle, 5000);
            }
            CloseHandle(processHandle);
            processHandle = NULL;
        }
#else
        if (processId > 0) {
            kill(processId, SIGTERM);
            int status;
            waitpid(processId, &status, 0);
            processId = 0;
        }
#endif
    }
    
    oxygenInitialized = false;
    oxygenUsingStubMode = false;
    romPath[0] = '\0';
}

// Check if the engine is running in stub mode
EXPORT int IsOxygenStubMode() {
    return oxygenUsingStubMode ? 1 : 0;
}

// Check if the engine is fully operational
EXPORT int IsOxygenFullyOperational() {
    return (oxygenInitialized && !oxygenUsingStubMode) ? 1 : 0;
}

// Get the current ROM path
EXPORT const char* GetOxygenRomPath() {
    return romPath;
}

} // extern "C"
