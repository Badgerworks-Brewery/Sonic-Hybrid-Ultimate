using System;
using System.Runtime.InteropServices;

namespace SonicHybridUltimate.Engines
{
    public class RSDKEngine : IGameEngine
    {
        [DllImport("RSDKv4", CallingConvention = CallingConvention.Cdecl)]
        private static extern int InitRSDKv4(string dataPath);

        [DllImport("RSDKv4", CallingConvention = CallingConvention.Cdecl)]
        private static extern void UpdateRSDKv4();

        [DllImport("RSDKv4", CallingConvention = CallingConvention.Cdecl)]
        private static extern void CleanupRSDKv4();

        private bool isInitialized = false;
        private string currentGame = "";
        private readonly ILogger logger;

        public RSDKEngine(ILogger logger)
        {
            this.logger = logger;
        }

        public bool Initialize(string gamePath)
        {
            try
            {
                int result = InitRSDKv4(gamePath);
                isInitialized = result == 0;
                if (isInitialized)
                {
                    currentGame = gamePath;
                    logger.Log($"RSDK Engine initialized with game: {gamePath}");
                }
                else
                {
                    logger.LogError($"Failed to initialize RSDK Engine with game: {gamePath}");
                }
                return isInitialized;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error initializing RSDK Engine: {ex.Message}");
                return false;
            }
        }

        public void Update()
        {
            if (isInitialized)
            {
                try
                {
                    UpdateRSDKv4();
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error updating RSDK Engine: {ex.Message}");
                }
            }
        }

        public void Cleanup()
        {
            if (isInitialized)
            {
                try
                {
                    CleanupRSDKv4();
                    isInitialized = false;
                    currentGame = "";
                    logger.Log("RSDK Engine cleaned up successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error cleaning up RSDK Engine: {ex.Message}");
                }
            }
        }

        public bool IsRunning => isInitialized;
        public string CurrentGame => currentGame;
    }
}
