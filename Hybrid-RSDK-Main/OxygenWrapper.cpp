// OxygenWrapper.cpp - P/Invoke wrapper for Sonic 3 AIR Oxygen Engine
// This wrapper provides a C interface to launch and control Sonic 3 AIR
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
    #include <windows.h>
    #include <process.h>
    #define EXPORT __declspec(dllexport)
#else
    #include <unistd.h>
    #include <sys/types.h>
    #include <sys/wait.h>
    #define EXPORT __attribute__((visibility("default")))
#endif

extern "C" {

static bool oxygenInitialized = false;
static char romPath[512] = "";

#ifdef _WIN32
static HANDLE processHandle = NULL;
static DWORD processId = 0;
#else
static pid_t processId = 0;
#endif

EXPORT int InitOxygenEngine(const char* scriptPath) {
    if (!scriptPath || strlen(scriptPath) == 0) {
        fprintf(stderr, "OxygenEngine: No ROM path provided\n");
        return 0;
    }

    fprintf(stderr, "OxygenEngine: Initializing with ROM: %s\n", scriptPath);
    
    // Validate ROM file exists and is readable
    FILE* romTest = fopen(scriptPath, "rb");
    if (!romTest) {
        fprintf(stderr, "OxygenEngine: ERROR - ROM file not found or not readable: %s\n", scriptPath);
        fprintf(stderr, "OxygenEngine: Please ensure you have selected a valid Sonic 3 & Knuckles ROM file.\n");
        fprintf(stderr, "OxygenEngine: The ROM should be named something like 'sonic3.bin' or 'Sonic_Knuckles_Wii_VC.bin'\n");
        return 0;
    }
    
    // Check ROM file size (basic validation)
    fseek(romTest, 0, SEEK_END);
    long romSize = ftell(romTest);
    fclose(romTest);
    
    if (romSize < 1024 * 1024) { // Less than 1MB is probably not a valid ROM
        fprintf(stderr, "OxygenEngine: WARNING - ROM file seems too small (%ld bytes). This might not be a valid Sonic 3 & Knuckles ROM.\n", romSize);
    } else {
        fprintf(stderr, "OxygenEngine: ROM file validated (%ld bytes)\n", romSize);
    }
    
    // Store the ROM path for later use
    strncpy(romPath, scriptPath, sizeof(romPath) - 1);
    romPath[sizeof(romPath) - 1] = '\0';
    
    // Find the Sonic 3 AIR executable
    // First check if user specified a custom path via environment variable
    const char* customPath = getenv("SONIC3AIR_PATH");
    const char* foundExe = NULL;
    
    if (customPath && strlen(customPath) > 0) {
        char customExePath[1024];
        snprintf(customExePath, sizeof(customExePath), "%s/sonic3air.exe", customPath);
        
        FILE* test = fopen(customExePath, "rb");
        if (test) {
            fclose(test);
            foundExe = customExePath;
            fprintf(stderr, "OxygenEngine: Found Sonic 3 AIR via SONIC3AIR_PATH: %s\n", foundExe);
        } else {
            // Try without .exe extension for Linux
            snprintf(customExePath, sizeof(customExePath), "%s/sonic3air", customPath);
            test = fopen(customExePath, "rb");
            if (test) {
                fclose(test);
                foundExe = customExePath;
                fprintf(stderr, "OxygenEngine: Found Sonic 3 AIR via SONIC3AIR_PATH: %s\n", foundExe);
            }
        }
    }
    
    // If not found via environment variable, search standard paths
    if (!foundExe) {
        const char* exePaths[] = {
            // Project-relative paths
            "../Sonic 3 AIR Main/Oxygen/sonic3air/bin/sonic3air.exe",
            "../Sonic 3 AIR Main/Oxygen/sonic3air/bin/sonic3air",
            "../Sonic 3 AIR Main/sonic3air.exe",
            "../Sonic 3 AIR Main/sonic3air",
            "Sonic 3 AIR Main/Oxygen/sonic3air/bin/sonic3air.exe",
            "Sonic 3 AIR Main/Oxygen/sonic3air/bin/sonic3air",
            "Sonic 3 AIR Main/sonic3air.exe",
            "Sonic 3 AIR Main/sonic3air",
            // Legacy paths for backward compatibility
            "../vendor/sonic3air/Oxygen/sonic3air/bin/sonic3air.exe",
            "../vendor/sonic3air/Oxygen/sonic3air/bin/sonic3air",
            "../sonic3air/sonic3air.exe",
            "../sonic3air/sonic3air",
            // Current directory
            "sonic3air.exe",
            "sonic3air",
            // Common installation paths
            "C:/Program Files/Sonic 3 AIR/sonic3air.exe",
            "C:/Program Files (x86)/Sonic 3 AIR/sonic3air.exe",
            "../../../Sonic 3 AIR/sonic3air.exe",
            "../../Sonic 3 AIR/sonic3air.exe"
        };
        
        for (size_t i = 0; i < sizeof(exePaths) / sizeof(exePaths[0]); i++) {
            FILE* test = fopen(exePaths[i], "rb");
            if (test) {
                fclose(test);
                foundExe = exePaths[i];
                fprintf(stderr, "OxygenEngine: Found Sonic 3 AIR at: %s\n", foundExe);
                break;
            }
        }
    }
    
    if (!foundExe) {
        fprintf(stderr, "OxygenEngine: ERROR - Sonic 3 AIR executable not found\n");
        fprintf(stderr, "OxygenEngine: \n");
        fprintf(stderr, "OxygenEngine: To fix this issue:\n");
        fprintf(stderr, "OxygenEngine: 1. Download Sonic 3 AIR from: https://sonic3air.org/\n");
        fprintf(stderr, "OxygenEngine: 2. Extract it to one of these locations:\n");
        fprintf(stderr, "OxygenEngine:    - 'Sonic 3 AIR Main' folder in your project directory\n");
        fprintf(stderr, "OxygenEngine:    - C:/Program Files/Sonic 3 AIR/\n");
        fprintf(stderr, "OxygenEngine:    - Same directory as this application\n");
        fprintf(stderr, "OxygenEngine: 3. Ensure the executable is named 'sonic3air.exe' (Windows) or 'sonic3air' (Linux)\n");
        fprintf(stderr, "OxygenEngine: 4. Alternatively, set SONIC3AIR_PATH environment variable to your custom installation\n");
        fprintf(stderr, "OxygenEngine: \n");
        fprintf(stderr, "OxygenEngine: Searched standard paths:\n");
        fprintf(stderr, "  - ../Sonic 3 AIR Main/sonic3air.exe\n");
        fprintf(stderr, "  - Sonic 3 AIR Main/sonic3air.exe\n");
        fprintf(stderr, "  - C:/Program Files/Sonic 3 AIR/sonic3air.exe\n");
        fprintf(stderr, "  - sonic3air.exe (current directory)\n");
        fprintf(stderr, "  - And several other common locations...\n");
        if (customPath) {
            fprintf(stderr, "OxygenEngine: Also checked SONIC3AIR_PATH: %s\n", customPath);
        }
        fprintf(stderr, "OxygenEngine: \n");
        fprintf(stderr, "OxygenEngine: Note: You need both the ROM file AND the Sonic 3 AIR executable for this to work.\n");
        return 0;
    }

#ifdef _WIN32
    // Launch Sonic 3 AIR as a child process on Windows
    char cmdLine[1024];
    snprintf(cmdLine, sizeof(cmdLine), "\"%s\" --rom=\"%s\"", foundExe, romPath);
    
    STARTUPINFOA si;
    PROCESS_INFORMATION pi;
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    ZeroMemory(&pi, sizeof(pi));
    
    if (!CreateProcessA(
        NULL,           // No module name (use command line)
        cmdLine,        // Command line
        NULL,           // Process handle not inheritable
        NULL,           // Thread handle not inheritable
        FALSE,          // Set handle inheritance to FALSE
        0,              // No creation flags
        NULL,           // Use parent's environment block
        NULL,           // Use parent's starting directory
        &si,            // Pointer to STARTUPINFO structure
        &pi)            // Pointer to PROCESS_INFORMATION structure
    ) {
        fprintf(stderr, "OxygenEngine: ERROR - CreateProcess failed (%lu)\n", GetLastError());
        return 0;
    }
    
    processHandle = pi.hProcess;
    processId = pi.dwProcessId;
    CloseHandle(pi.hThread); // We don't need the thread handle
    
    fprintf(stderr, "OxygenEngine: Launched Sonic 3 AIR (PID: %lu)\n", processId);
#else
    // Launch Sonic 3 AIR as a child process on Unix/Linux
    processId = fork();
    
    if (processId < 0) {
        fprintf(stderr, "OxygenEngine: ERROR - fork() failed\n");
        return 0;
    }
    
    if (processId == 0) {
        // Child process - execute Sonic 3 AIR
        char romArg[600];
        snprintf(romArg, sizeof(romArg), "--rom=%s", romPath);
        
        execl(foundExe, foundExe, romArg, (char*)NULL);
        
        // If execl returns, it failed
        fprintf(stderr, "OxygenEngine: ERROR - execl() failed\n");
        exit(1);
    }
    
    // Parent process
    fprintf(stderr, "OxygenEngine: Launched Sonic 3 AIR (PID: %d)\n", processId);
#endif
    
    oxygenInitialized = true;
    return 1; // Success
}

EXPORT void UpdateOxygenEngine() {
    if (!oxygenInitialized) {
        return;
    }
    
    // Check if the process is still running
#ifdef _WIN32
    if (processHandle) {
        DWORD exitCode;
        if (GetExitCodeProcess(processHandle, &exitCode)) {
            if (exitCode != STILL_ACTIVE) {
                fprintf(stderr, "OxygenEngine: Sonic 3 AIR process has exited\n");
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
            // Process has exited
            fprintf(stderr, "OxygenEngine: Sonic 3 AIR process has exited\n");
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
    
    fprintf(stderr, "OxygenEngine: Cleaning up\n");
    
#ifdef _WIN32
    if (processHandle) {
        // Try to terminate gracefully first
        if (TerminateProcess(processHandle, 0)) {
            WaitForSingleObject(processHandle, 5000); // Wait up to 5 seconds
        }
        CloseHandle(processHandle);
        processHandle = NULL;
    }
#else
    if (processId > 0) {
        // Send SIGTERM for graceful shutdown
        kill(processId, SIGTERM);
        
        // Wait for process to exit
        int status;
        waitpid(processId, &status, 0);
        processId = 0;
    }
#endif
    
    oxygenInitialized = false;
    romPath[0] = '\0';
}

} // extern "C"
