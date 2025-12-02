using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SonicHybridUltimate.Engines
{
    public class OxygenEngine : IGameEngine, IDisposable
    {
        private readonly ILogger<OxygenEngine> _logger;
        private bool _isInitialized;
        private bool _isStubMode;
        private string _currentScript = string.Empty;
        private bool _isDisposed;
        private static NativeMethods.LogCallback? _logCallbackDelegate;

        public bool IsRunning => _isInitialized && !_isStubMode;
        public bool IsStubMode => _isStubMode;
        public string CurrentGame => _currentScript;

        static OxygenEngine()
        {
            // Register the unified native library resolver
            NativeLibraryResolver.Register();
        }

        public OxygenEngine(ILogger<OxygenEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Set up native logging callback (keep delegate alive)
            try
            {
                _logCallbackDelegate = NativeLogCallback;
                NativeMethods.SetOxygenLogCallback(_logCallbackDelegate);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to set native logging callback - this is expected if native library is not yet loaded");
            }
        }

        private static void NativeLogCallback(string message)
        {
            // Remove trailing newlines
            message = message.TrimEnd('\n');
            
            // Write to console which is connected to the UI logger
            Console.WriteLine($"[OXYGEN] {message}");
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
                    _logger.LogWarning("OxygenEngine native library is not available.");
                    _logger.LogWarning("Sonic 3 & Knuckles will run in stub mode.");
                    _logger.LogInformation("");
                    _logger.LogInformation("To enable full Sonic 3 support:");
                    _logger.LogInformation("1. Build the native libraries: ./build_native_libs.sh");
                    _logger.LogInformation("2. Download Sonic 3 AIR from: https://sonic3air.org/");
                    _logger.LogInformation("3. Place sonic3air.exe in 'Sonic 3 AIR Main' folder");
                    
                    // Still validate the ROM exists
                    if (!File.Exists(scriptPath))
                    {
                        _logger.LogError("ROM file not found: {ScriptPath}", scriptPath);
                        return false;
                    }
                    
                    _isStubMode = true;
                    _isInitialized = true;
                    _currentScript = scriptPath;
                    return true;
                }

                // Validate ROM file exists before attempting to initialize
                if (!File.Exists(scriptPath))
                {
                    _logger.LogError("ROM file not found: {ScriptPath}", scriptPath);
                    _logger.LogError("Please ensure you have selected a valid Sonic 3 & Knuckles ROM file.");
                    return false;
                }

                var result = NativeMethods.InitOxygenEngine(scriptPath);
                _isInitialized = (result == 1);

                if (_isInitialized)
                {
                    // Check if we're in stub mode
                    try
                    {
                        _isStubMode = NativeMethods.IsOxygenStubMode() == 1;
                    }
                    catch (DllNotFoundException)
                    {
                        // Native library not available, assume stub mode
                        _isStubMode = false;
                    }
                    catch (EntryPointNotFoundException)
                    {
                        // Function not exported, assume non-stub mode
                        _isStubMode = false;
                    }
                    
                    _currentScript = scriptPath;
                    
                    if (_isStubMode)
                    {
                        _logger.LogWarning("Oxygen Engine initialized in stub mode");
                        _logger.LogWarning("Sonic 3 AIR executable was not found.");
                        _logger.LogInformation("");
                        _logger.LogInformation("To enable full Sonic 3 support:");
                        _logger.LogInformation("1. Download Sonic 3 AIR from: https://sonic3air.org/");
                        _logger.LogInformation("2. Extract to 'Sonic 3 AIR Main' folder");
                        _logger.LogInformation("3. Or set SONIC3AIR_PATH environment variable");
                    }
                    else
                    {
                        _logger.LogInformation("Oxygen Engine initialized successfully");
                    }
                }
                else
                {
                    _logger.LogError("Failed to initialize Oxygen Engine");
                }

                return _isInitialized;
            }
            catch (DllNotFoundException ex)
            {
                _logger.LogWarning(ex, "OxygenEngine native library not found - running in stub mode");
                _isStubMode = true;
                _isInitialized = true;
                _currentScript = scriptPath;
                return true;
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

            // In stub mode, there's nothing to update
            if (_isStubMode)
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
                
                if (!_isStubMode)
                {
                    NativeMethods.CleanupOxygenEngine();
                }
                
                _isInitialized = false;
                _isStubMode = false;
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
            return NativeLibraryResolver.IsLibraryAvailable("OxygenEngine");
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
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void LogCallback([MarshalAs(UnmanagedType.LPStr)] string message);
            
            [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
            public static extern void SetOxygenLogCallback(LogCallback callback);
            
            [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
            public static extern int InitOxygenEngine(string scriptPath);

            [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
            public static extern void UpdateOxygenEngine();

            [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
            public static extern void CleanupOxygenEngine();
            
            [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
            public static extern int IsOxygenStubMode();
            
            [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
            public static extern int IsOxygenFullyOperational();
        }
    }
}
