#ifndef RSDK_INTEGRATION_H
#define RSDK_INTEGRATION_H

#include <string>
#include "../Engines/IGameEngine.hpp"

namespace SonicHybrid {
    class RSDKIntegration {
    public:
        static bool Initialize(const std::string& dataPath);
        static void Update();
        static void Cleanup();
        
        // RSDK specific functions
        static bool LoadScene(const std::string& sceneName);
        static bool IsSceneComplete();
        static void RegisterSceneCompleteCallback(void (*callback)());
        
        // Entity state management
        static void SaveEntityStates();
        static void RestoreEntityStates();
        
        // Scene transition detection
        static bool IsSonic2Complete();
        static void OnSonic2Complete();
        
    private:
        static bool initialized;
        static void (*sceneCompleteCallback)();
    };
}

#endif // RSDK_INTEGRATION_H
