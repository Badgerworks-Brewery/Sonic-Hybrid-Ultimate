using System;
using System.Runtime.InteropServices;

namespace SonicHybridUltimate.Engines
{
    public class OxygenEngine : IGameEngine
    {
        [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
        private static extern int InitOxygenEngine(string scriptPath);

        [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
        private static extern void UpdateOxygenEngine();

        [DllImport("OxygenEngine", CallingConvention = CallingConvention.Cdecl)]
        private static extern void CleanupOxygenEngine();

        private bool isInitialized = false;
        private string currentGame = "";
        private readonly ILogger logger;

        public OxygenEngine(ILogger logger)
        {
            this.logger = logger;
        }

        public bool Initialize(string scriptPath)
        {
            try
            {
                int result = InitOxygenEngine(scriptPath);
                isInitialized = result == 0;
                if (isInitialized)
                {
                    currentGame = scriptPath;
                    logger.Log($"Oxygen Engine initialized with script: {scriptPath}");
                }
                else
                {
                    logger.LogError($"Failed to initialize Oxygen Engine with script: {scriptPath}");
                }
                return isInitialized;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error initializing Oxygen Engine: {ex.Message}");
                return false;
            }
        }

        public void Update()
        {
            if (isInitialized)
            {
                try
                {
                    UpdateOxygenEngine();
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error updating Oxygen Engine: {ex.Message}");
                }
            }
        }

        public void Cleanup()
        {
            if (isInitialized)
            {
                try
                {
                    CleanupOxygenEngine();
                    isInitialized = false;
                    currentGame = "";
                    logger.Log("Oxygen Engine cleaned up successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error cleaning up Oxygen Engine: {ex.Message}");
                }
            }
        }

        public bool IsRunning => isInitialized;
        public string CurrentGame => currentGame;
    }
}
