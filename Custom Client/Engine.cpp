#include "Engine.hpp"
#include "Logger.cs"
#include <algorithm>
#include <cstring>

namespace SonicHybrid {
    bool Engine::Initialize() {
        Logger::Log("Initializing Hybrid Engine...");
        engineRunning = true;
        InitializeSceneList();
        return true;
    }

    void Engine::InitializeSceneList() {
        // Define Sonic 2 completion detection points
        sceneList = {
            {
                "DEATH_EGG_ZONE_2",
                true,
                {
                    0x12345678,  // Death Egg Robot defeated
                    0x87654321,  // Super Sonic transformation complete
                    0x11223344   // End cutscene started
                }
            }
        };
    }

    void Engine::CheckSonic2Completion() {
        if (currentEngine != EngineType::RSDK_V4) return;

        // Read Death Egg completion state
        uint8_t robotState = 0, cutsceneState = 0, creditsState = 0, emeraldCount = 0;
        
        // Read memory values through RSDK API
        if (rsdkInstance) {
            robotState = *reinterpret_cast<uint8_t*>(ADDR_ROBOT_DEFEATED);
            cutsceneState = *reinterpret_cast<uint8_t*>(ADDR_CUTSCENE_START);
            creditsState = *reinterpret_cast<uint8_t*>(ADDR_CREDITS_PLAYING);
            emeraldCount = *reinterpret_cast<uint8_t*>(ADDR_EMERALDS_COUNT);
        }

        // Update state
        deathEggState.robotDefeated = (robotState != 0);
        deathEggState.cutsceneStarted = (cutsceneState != 0);
        deathEggState.creditsPlaying = (creditsState != 0);
        deathEggState.emeraldsCollected = (emeraldCount == 7);  // All emeralds for super ending

        // Check if game is complete (any ending)
        if (deathEggState.robotDefeated && deathEggState.cutsceneStarted && deathEggState.creditsPlaying) {
            Logger::Log("Sonic 2 completion detected!");
            if (sonic2CompleteCallback) {
                sonic2CompleteCallback();
            }
            TransitionToSonic3();
        }
    }

    void Engine::TransitionToSonic3() {
        if (transitioning) return;

        Logger::Log("Starting transition to Sonic 3...");
        transitioning = true;
        transitionProgress = 0.0f;
        SaveEngineState();
        
        // Initialize Oxygen engine in background
        InitializeOxygen();
    }

    void Engine::HandleTransition() {
        if (!transitioning) return;

        transitionProgress += 1.0f / 60.0f; // Assuming 60 FPS
        
        if (transitionProgress < 0.5f) {
            // First half: fade out RSDK
            float fadeOut = 1.0f - (transitionProgress * 2.0f);
            // TODO: Apply fade out to RSDK renderer
        } else {
            // Second half: fade in Oxygen
            float fadeIn = (transitionProgress - 0.5f) * 2.0f;
            // TODO: Apply fade in to Oxygen renderer
        }

        if (transitionProgress >= 1.0f) {
            transitioning = false;
            currentEngine = EngineType::OXYGEN;
            // Clean up RSDK
            if (rsdkInstance) {
                // TODO: Cleanup RSDK
                rsdkInstance = nullptr;
            }
        }
    }

    void Engine::Update() {
        if (!engineRunning) return;

        if (transitioning) {
            HandleTransition();
            return;
        }

        switch (currentEngine) {
            case EngineType::RSDK_V4:
                // Update RSDK
                CheckSonic2Completion();
                break;

            case EngineType::OXYGEN:
                // Update Oxygen/S3AIR
                break;

            default:
                break;
        }
    }

    void Engine::Render() {
        if (!engineRunning) return;

        if (transitioning) {
            // Handle transition rendering
            return;
        }

        switch (currentEngine) {
            case EngineType::RSDK_V4:
                // Render RSDK
                break;

            case EngineType::OXYGEN:
                // Render Oxygen/S3AIR
                break;

            default:
                break;
        }
    }

    bool Engine::LoadRSDKGame(const std::string& rsdkPath) {
        if (currentEngine != EngineType::NONE) return false;

        Logger::Log("Loading RSDK game: " + rsdkPath);
        currentRSDKFile = rsdkPath;
        InitializeRSDK();
        currentEngine = EngineType::RSDK_V4;
        return true;
    }

    bool Engine::LoadSonic3ROM(const std::string& romPath) {
        if (currentEngine != EngineType::NONE) return false;

        Logger::Log("Loading Sonic 3 ROM: " + romPath);
        currentROMFile = romPath;
        InitializeOxygen();
        currentEngine = EngineType::OXYGEN;
        return true;
    }

    void Engine::RegisterSonic2CompletionCallback(std::function<void()> callback) {
        sonic2CompleteCallback = callback;
    }

    bool Engine::IsSonic2Complete() const {
        if (currentEngine != EngineType::RSDK_V4) return false;
        return deathEggState.robotDefeated && 
               deathEggState.cutsceneStarted && 
               deathEggState.creditsPlaying;
    }

    std::string Engine::GetCurrentScene() const {
        switch (currentEngine) {
            case EngineType::RSDK_V4:
                // TODO: Get current scene from RSDK
                return "";
            case EngineType::OXYGEN:
                // TODO: Get current zone from S3AIR
                return "";
            default:
                return "";
        }
    }

    void Engine::SaveEngineState() {
        // TODO: Implement state saving
    }

    void Engine::RestoreEngineState() {
        // TODO: Implement state restoration
    }
}
