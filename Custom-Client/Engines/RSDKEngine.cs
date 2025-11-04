using System;
using System.Runtime.InteropServices;
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
