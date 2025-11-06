#ifndef ENGINE_MANAGER_H
#define ENGINE_MANAGER_H

#include <memory>
#include <string>
#include "IGameEngine.hpp"

namespace SonicHybrid {
    enum class EngineType {
        RSDK_V4,
        RSDK_V3,
        OXYGEN,
        NONE
    };

    class EngineManager {
    public:
        static EngineManager& Instance() {
            static EngineManager instance;
            return instance;
        }

        void Initialize();
        void Update();
        void Shutdown();

        // Engine transitions
        void TransitionTo(EngineType engine);
        bool IsTransitioning() const;
        
        // Engine state
        EngineType GetCurrentEngine() const;
        IGameEngine* GetCurrentEngineInstance();

        // Specific engine operations
        void StartSonic2();
        void TransitionToSonic3();
        void HandleSonic2Completion();

    private:
        EngineManager() = default;
        ~EngineManager() = default;
        
        void InitializeRSDKV4();
        void InitializeOxygenEngine();
        void HandleTransitionComplete();

        EngineType currentEngine = EngineType::NONE;
        EngineType targetEngine = EngineType::NONE;
        std::unique_ptr<IGameEngine> rsdkEngine;
        std::unique_ptr<IGameEngine> oxygenEngine;
        bool transitioning = false;
    };
}

#endif // ENGINE_MANAGER_H
