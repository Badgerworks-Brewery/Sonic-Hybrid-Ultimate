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
    
    // Store the ROM path for later use
    strncpy(romPath, scriptPath, sizeof(romPath) - 1);
    romPath[sizeof(romPath) - 1] = '\0';
    
    // Find the Sonic 3 AIR executable
    const char* exePaths[] = {
        "../vendor/sonic3air/Oxygen/sonic3air/bin/sonic3air.exe",
        "../vendor/sonic3air/Oxygen/sonic3air/bin/sonic3air",
        "sonic3air.exe",
        "sonic3air",
        "../sonic3air/sonic3air.exe",
        "../sonic3air/sonic3air"
    };
    
    const char* foundExe = NULL;
    for (size_t i = 0; i < sizeof(exePaths) / sizeof(exePaths[0]); i++) {
        FILE* test = fopen(exePaths[i], "rb");
        if (test) {
            fclose(test);
            foundExe = exePaths[i];
            fprintf(stderr, "OxygenEngine: Found Sonic 3 AIR at: %s\n", foundExe);
            break;
        }
    }
    
    if (!foundExe) {
        fprintf(stderr, "OxygenEngine: ERROR - Sonic 3 AIR executable not found\n");
        fprintf(stderr, "OxygenEngine: Searched paths:\n");
        for (size_t i = 0; i < sizeof(exePaths) / sizeof(exePaths[0]); i++) {
            fprintf(stderr, "  - %s\n", exePaths[i]);
        }
        fprintf(stderr, "OxygenEngine: Please build Sonic 3 AIR first or place it in the expected location\n");
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
