#include "EngineManager.hpp"
#include "../Core/Logger.hpp"
#include "RSDKEngine.hpp"
#include "OxygenEngine.hpp"
#include "../TransitionManager.hpp"

namespace SonicHybrid {
    void EngineManager::Initialize() {
        // Initialize engines
        InitializeRSDKV4();
        InitializeOxygenEngine();
        
        // Start with RSDK by default
        currentEngine = EngineType::RSDK_V4;
        targetEngine = EngineType::RSDK_V4;
    }

    void EngineManager::Update() {
        if (transitioning) {
            TransitionManager::UpdateTransition();
            if (!TransitionManager::IsTransitioning()) {
                HandleTransitionComplete();
            }
            return;
        }

        // Update current engine
        switch (currentEngine) {
            case EngineType::RSDK_V4:
                rsdkEngine->Update();
                break;
            case EngineType::OXYGEN:
                oxygenEngine->Update();
                break;
        }
    }

    void EngineManager::TransitionTo(EngineType engine) {
        if (transitioning || engine == currentEngine) return;

        targetEngine = engine;
        transitioning = true;
        TransitionManager::BeginTransition(TransitionType::FADE, 1.0f);
    }

    void EngineManager::HandleTransitionComplete() {
        // Switch engines
        currentEngine = targetEngine;
        transitioning = false;

        // Initialize target engine
        switch (currentEngine) {
            case EngineType::RSDK_V4:
                rsdkEngine->Initialize("Hybrid-RSDK Main/sonic2.rsdk");
                break;
            case EngineType::OXYGEN:
                oxygenEngine->Initialize("Sonic 3 AIR Main/sonic3.bin");
                break;
        }
    }

    void EngineManager::HandleSonic2Completion() {
        // Called when Sonic 2's ending is detected
        TransitionToSonic3();
    }

    void EngineManager::TransitionToSonic3() {
        TransitionTo(EngineType::OXYGEN);
    }

    void EngineManager::InitializeRSDKV4() {
        rsdkEngine = std::make_unique<RSDKEngine>();
        if (!rsdkEngine->Initialize("Hybrid-RSDK Main/sonic2.rsdk")) {
            Logger::LogError("Failed to initialize RSDK Engine");
        }
    }

    void EngineManager::InitializeOxygenEngine() {
        oxygenEngine = std::make_unique<OxygenEngine>();
        // Don't initialize Oxygen Engine yet - wait for transition
    }

    bool EngineManager::IsTransitioning() const {
        return transitioning;
    }

    EngineType EngineManager::GetCurrentEngine() const {
        return currentEngine;
    }

    IGameEngine* EngineManager::GetCurrentEngineInstance() {
        switch (currentEngine) {
            case EngineType::RSDK_V4:
                return rsdkEngine.get();
            case EngineType::OXYGEN:
                return oxygenEngine.get();
            default:
                return nullptr;
        }
    }
}
