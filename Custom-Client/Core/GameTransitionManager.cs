using System;
using System.Threading.Tasks;
using SonicHybridUltimate.Engines;

namespace SonicHybridUltimate.Core
{
    public class GameTransitionManager
    {
        private readonly IGameEngine rsdkEngine;
        private readonly IGameEngine oxygenEngine;
        private readonly ILogger logger;
        private bool isTransitioning = false;

        public GameTransitionManager(IGameEngine rsdkEngine, IGameEngine oxygenEngine, ILogger logger)
        {
            this.rsdkEngine = rsdkEngine;
            this.oxygenEngine = oxygenEngine;
            this.logger = logger;
        }

        public async Task TransitionToSonic3()
        {
            if (isTransitioning)
            {
                logger.LogWarning("Transition already in progress");
                return;
            }

            try
            {
                isTransitioning = true;
                logger.Log("Starting transition to Sonic 3");

                // Start fading out RSDK game (should be implemented in RSDK)
                await Task.Delay(1000); // Give time for fade out

                // Clean up RSDK
                rsdkEngine.Cleanup();
                logger.Log("RSDK Engine cleaned up");

                // Initialize Oxygen Engine with Sonic 3
                string sonic3Path = "Sonic 3 AIR Main/sonic3.bin"; // Adjust path as needed
                if (oxygenEngine.Initialize(sonic3Path))
                {
                    logger.Log("Oxygen Engine initialized successfully");
                }
                else
                {
                    throw new Exception("Failed to initialize Oxygen Engine");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error during transition: {ex.Message}");
                // Try to recover by reinitializing RSDK
                TryRecoverRSDK();
            }
            finally
            {
                isTransitioning = false;
            }
        }

        private void TryRecoverRSDK()
        {
            try
            {
                string rsdkPath = "Hybrid-RSDK-Main/Data/sonic2.rsdk"; // Adjust path as needed
                if (rsdkEngine.Initialize(rsdkPath))
                {
                    logger.Log("Successfully recovered RSDK Engine");
                }
                else
                {
                    logger.LogError("Failed to recover RSDK Engine");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error recovering RSDK Engine: {ex.Message}");
            }
        }

        public bool IsTransitioning => isTransitioning;
    }
}
