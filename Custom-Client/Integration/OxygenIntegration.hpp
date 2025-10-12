#ifndef OXYGEN_INTEGRATION_H
#define OXYGEN_INTEGRATION_H

#include <string>
#include "../Engines/IGameEngine.hpp"

namespace SonicHybrid {
    class OxygenIntegration {
    public:
        static bool Initialize(const std::string& romPath);
        static void Update();
        static void Cleanup();
        
        // Oxygen/Sonic 3 AIR specific functions
        static bool StartAngelIsland();
        static void SetupInitialState();
        static bool IsSceneReady();
        
        // Lemonscript integration
        static bool InitializeLemonscript();
        static void UpdateLemonscript();
        static void ExecuteLemonscriptFunction(const std::string& funcName);
        
        // State management
        static void SaveGameState();
        static void RestoreGameState();
        static void TranslateStateFromRSDK();
        
    private:
        static bool initialized;
        static bool lemonscriptInitialized;
    };
}

#endif // OXYGEN_INTEGRATION_H
