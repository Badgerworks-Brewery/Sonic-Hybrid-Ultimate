using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace SonicHybridUltimate.Engines
{
    public class RSDKEngine : IGameEngine, IDisposable
    {
        private readonly ILogger<RSDKEngine> _logger;
        private bool _isInitialized;
        private string _currentGame = string.Empty;
        private bool _isDisposed;

        private const int ADDR_CURRENT_ZONE = 0x203A40;
        private const int ADDR_ROBOT_STATE = 0x203A44;
        private const int ADDR_EXPLOSION_TIMER = 0x203A48;
        private const byte DEATH_EGG_ZONE_ID = 0x0B;

        public bool IsRunning => _isInitialized;
        public string CurrentGame => _currentGame;

        static RSDKEngine()
        {
            // Set up DLL resolver to help find RSDKv4.dll
            NativeLibrary.SetDllImportResolver(typeof(RSDKEngine).Assembly, DllImportResolver);
        }

        private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != "RSDKv4")
                return IntPtr.Zero;

            // Try to load from various locations
            var searchPaths = new[]
            {
                // Same directory as the executable
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                // Current directory
                Directory.GetCurrentDirectory(),
                // Hybrid-RSDK-Main build output (relative to exe)
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "..", "Hybrid-RSDK-Main", "build", "lib"),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "..", "Hybrid-RSDK-Main", "build", "bin"),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "..", "Hybrid-RSDK-Main", "build", "bin", "Release"),
            };

            var dllNames = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[] { "RSDKv4.dll" }
                : new[] { "libRSDKv4.so", "RSDKv4.so" };

            foreach (var basePath in searchPaths)
            {
                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                    continue;

                foreach (var dllName in dllNames)
                {
                    var fullPath = Path.Combine(basePath, dllName);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            var handle = NativeLibrary.Load(fullPath);
                            Console.WriteLine($"Successfully loaded RSDKv4 from: {fullPath}");
                            return handle;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to load {fullPath}: {ex.Message}");
                        }
                    }
                }
            }

            Console.WriteLine($"Could not find RSDKv4 library in any of the search paths:");
            foreach (var path in searchPaths)
            {
                Console.WriteLine($"  - {path}");
            }

            return IntPtr.Zero;
        }

        public RSDKEngine(ILogger<RSDKEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Initialize(string gamePath)
        {
            ThrowIfDisposed();

            try
            {
                _logger.LogInformation("Initializing RSDK with game: {GamePath}", gamePath);

                if (_isInitialized)
                {
                    _logger.LogWarning("RSDK is already initialized. Cleaning up first...");
                    Cleanup();
                }

                var result = NativeMethods.InitRSDKv4(gamePath);
                _isInitialized = (result == 1);

                if (_isInitialized)
                {
                    _currentGame = gamePath;
                    _logger.LogInformation("RSDK initialized successfully");
                }
                else
                {
                    _logger.LogError("Failed to initialize RSDK");
                }

                return _isInitialized;
            }
            catch (DllNotFoundException ex)
            {
                _logger.LogError(ex, "RSDKv4 DLL not found. Make sure RSDKv4.dll (Windows) or libRSDKv4.so (Linux) is in the application directory.");
                _logger.LogError("Executable location: {Location}", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing RSDK");
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
                NativeMethods.UpdateRSDKv4();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating RSDK");
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
                _logger.LogInformation("Cleaning up RSDK");
                NativeMethods.CleanupRSDKv4();
                _isInitialized = false;
                _currentGame = string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up RSDK");
                throw;
            }
        }

        public bool IsDeathEggDefeated()
        {
            ThrowIfDisposed();

            if (!_isInitialized)
            {
                return false;
            }

            try
            {
                // TODO: This currently uses hardcoded memory addresses which DON'T WORK
                // Need to implement proper P/Invoke to RSDKBridge native methods
                // The native RSDKBridge.cpp now uses actual RSDK variables like:
                // - currentStageFolder (to detect "DEZ"/"DeathEgg")
                // - stageListPosition (to check if we're at stage 11/0x0B)
                // - Boss object state (to detect defeat)
                //
                // This needs to be exposed through C DLL exports that this C# code can call
                // For now, returning false until native bridge is properly connected
                return false;
                
                /*
                // STUB CODE - doesn't actually work:
                return IsInDeathEggZone() && 
                       GetDeathEggRobotState() == 1 && 
                       GetDeathEggExplosionTimer() > 0;
                */
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Death Egg state");
                return false;
            }
        }

        private bool IsInDeathEggZone()
        {
            var zoneId = Marshal.ReadByte(new IntPtr(ADDR_CURRENT_ZONE));
            return zoneId == DEATH_EGG_ZONE_ID;
        }

        private int GetDeathEggRobotState()
        {
            return Marshal.ReadByte(new IntPtr(ADDR_ROBOT_STATE));
        }

        private int GetDeathEggExplosionTimer()
        {
            return Marshal.ReadByte(new IntPtr(ADDR_EXPLOSION_TIMER));
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RSDKEngine));
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
            [DllImport("RSDKv4", CallingConvention = CallingConvention.Cdecl)]
            public static extern int InitRSDKv4(string dataPath);

            [DllImport("RSDKv4", CallingConvention = CallingConvention.Cdecl)]
            public static extern void UpdateRSDKv4();

            [DllImport("RSDKv4", CallingConvention = CallingConvention.Cdecl)]
            public static extern void CleanupRSDKv4();
        }
    }
}
