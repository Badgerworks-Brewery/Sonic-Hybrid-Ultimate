#include "RSDKBridge.hpp"
#include "../RSDKV4-Decompilation/RSDKv4/Scene.hpp"
#include <cstring>

// Import the actual RSDK global variables
extern int activeStageList;
extern int stageListPosition;
extern char currentStageFolder[0x100];
extern SceneInfo stageList[STAGELIST_MAX][0x100];

namespace RSDK {
    bool RSDKBridge::Initialize(RetroEngine* engine) {
        if (!engine) return false;
        rsdkEngine = engine;
        return true;
    }

    void RSDKBridge::Update() {
        if (!rsdkEngine) return;

        // Check Death Egg state using ACTUAL RSDK variables
        auto state = GetDeathEggState();
        if (state.isActive && state.robotDefeated && state.cutsceneStarted && state.creditsPlaying) {
            if (completionCallback) {
                completionCallback();
            }
        }
    }

    bool RSDKBridge::ReadMemory(uint32_t address, void* buffer, size_t size) {
        // Deprecated - use actual RSDK variables instead
        return false;
    }

    bool RSDKBridge::WriteMemory(uint32_t address, const void* buffer, size_t size) {
        // Deprecated - use actual RSDK variables instead
        return false;
    }

    RSDKBridge::DeathEggState RSDKBridge::GetDeathEggState() {
        DeathEggState state = {};
        
        if (!rsdkEngine) return state;

        // Check if we're in Death Egg Zone by examining the ACTUAL stage folder name
        // Sonic 2 Death Egg Zone folder is typically "DEZ" or "DeathEgg"
        if (currentStageFolder[0] != '\0') {
            // Check for Death Egg Zone - common names in Sonic 2
            state.isActive = (strstr(currentStageFolder, "DEZ") != nullptr ||
                            strstr(currentStageFolder, "DeathEgg") != nullptr ||
                            strstr(currentStageFolder, "Death") != nullptr);
            
            // Also check stage list position - Death Egg is typically the last zone in Sonic 2
            // In Sonic 2's stage list, Death Egg is usually stage 11 (0x0B)
            if (stageListPosition == 11 || stageListPosition == 0x0B) {
                state.isActive = true;
            }
        }

        // TODO: Implement actual boss defeat detection
        // This requires checking Object/Entity state in the scene
        // Would need to iterate through objectEntityList and check for boss object defeat state
        state.robotDefeated = false;  // Needs actual boss object state check
        state.cutsceneStarted = false;  // Needs cutscene state check
        state.creditsPlaying = false;   // Needs credits state check
        state.emeraldCount = 0;        // Needs global variable check

        return state;
    }

    void RSDKBridge::RegisterCompletionCallback(std::function<void()> callback) {
        completionCallback = callback;
    }
}
