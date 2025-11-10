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
                    _logger.LogError("OxygenEngine native library is not available. Please build the native libraries first.");
                    _logger.LogError("Run the build_native_libs.sh script or build the project with CMake to generate the required DLLs.");
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
                    _logger.LogError("This may be because:");
                    _logger.LogError("1. The Sonic 3 AIR executable is not found");
                    _logger.LogError("2. The ROM file path is invalid: {ScriptPath}", scriptPath);
                    _logger.LogError("3. Sonic 3 AIR is not properly installed");
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
