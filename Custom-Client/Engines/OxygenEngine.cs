using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SonicHybridUltimate.Engines
{
    public class OxygenEngine : IGameEngine, IDisposable
    {
        private readonly ILogger<OxygenEngine> _logger;
        private bool _isInitialized;
        private string _currentScript = string.Empty;
        private bool _isDisposed;

        public bool IsRunning => _isInitialized;
        public string CurrentGame => _currentScript;

        public OxygenEngine(ILogger<OxygenEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Initialize(string scriptPath)
        {
            ThrowIfDisposed();

            try
            {
                _logger.LogInformation("Initializing Oxygen Engine with script: {ScriptPath}", scriptPath);

                if (_isInitialized)
                {
                    _logger.LogWarning("Oxygen Engine is already initialized. Cleaning up first...");
                    Cleanup();
                }

                // Check if the native library is available
                if (!IsNativeLibraryAvailable())
                {
                    _logger.LogError("OxygenEngine native library is not available.");
                    _logger.LogError("This means the OxygenEngine.dll was not built or is not in the correct location.");
                    _logger.LogError("");
                    _logger.LogError("To fix this:");
                    _logger.LogError("1. Run the build_native_libs.sh script (Linux/Mac) or build_native_libs.ps1 (Windows)");
                    _logger.LogError("2. Or build the project with CMake to generate the required DLLs");
                    _logger.LogError("3. Ensure the OxygenEngine.dll is in the same directory as this application");
                    return false;
                }

                // Validate ROM file exists before attempting to initialize
                if (!File.Exists(scriptPath))
                {
                    _logger.LogError("ROM file not found: {ScriptPath}", scriptPath);
                    _logger.LogError("Please ensure you have selected a valid Sonic 3 & Knuckles ROM file.");
                    _logger.LogError("The ROM file should be named something like 'sonic3.bin' or 'Sonic_Knuckles_Wii_VC.bin'");
                    return false;
                }

                var result = NativeMethods.InitOxygenEngine(scriptPath);
                _isInitialized = (result == 1);

                if (_isInitialized)
                {
                    _currentScript = scriptPath;
                    _logger.LogInformation("Oxygen Engine initialized successfully");
                }
                else
                {
                    _logger.LogError("Failed to initialize Oxygen Engine");
                    _logger.LogError("");
                    _logger.LogError("This is most likely because Sonic 3 AIR is not installed or not found.");
                    _logger.LogError("");
                    _logger.LogError("To fix this issue:");
                    _logger.LogError("1. Download Sonic 3 AIR from: https://sonic3air.org/");
                    _logger.LogError("2. Extract it to a 'Sonic 3 AIR Main' folder in your project directory");
                    _logger.LogError("3. Make sure the sonic3air.exe file is present");
                    _logger.LogError("4. Verify your ROM file is valid: {ScriptPath}", scriptPath);
                    _logger.LogError("");
                    _logger.LogError("Alternative locations for Sonic 3 AIR:");
                    _logger.LogError("- C:/Program Files/Sonic 3 AIR/");
                    _logger.LogError("- Same directory as this application");
                    _logger.LogError("");
                    _logger.LogError("Note: You need BOTH the ROM file AND the Sonic 3 AIR executable.");
                }

                return _isInitialized;
            }
            catch (DllNotFoundException ex)
            {
                _logger.LogError(ex, "OxygenEngine native library not found. Please build the native libraries first.");
                _logger.LogError("Run: ./build_native_libs.sh or build the CMake project to generate OxygenEngine.dll");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Oxygen Engine");
                return false;
            }
        }

        public void Update()
        {
            ThrowIfDisposed();

            if (!_isInitialized)
            {
                return;
            }

            try
            {
                NativeMethods.UpdateOxygenEngine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Oxygen Engine");
                throw;
            }
        }

        public void Cleanup()
        {
            if (!_isInitialized)
            {
                return;
            }

            try
            {
                _logger.LogInformation("Cleaning up Oxygen Engine");
                NativeMethods.CleanupOxygenEngine();
                _isInitialized = false;
                _currentScript = string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up Oxygen Engine");
                throw;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(OxygenEngine));
            }
        }

        private bool IsNativeLibraryAvailable()
        {
            try
            {
                // Try to load the library by attempting to get a function pointer
                // This is a safer way to check if the DLL exists and is loadable
                var handle = NativeMethods.LoadLibrary("OxygenEngine");
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(handle);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isInitialized)
            {
                Cleanup();
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        private static class NativeMethods
        {
            [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
            public static extern int InitOxygenEngine(string scriptPath);

            [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
            public static extern void UpdateOxygenEngine();

            [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
            public static extern void CleanupOxygenEngine();

            // Windows API functions for library loading checks
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool FreeLibrary(IntPtr hModule);
        }
    }
}
