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
                }

                return _isInitialized;
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
        }
    }
}
